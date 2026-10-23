using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.PaymentModule.Core.Model;
using VirtoCommerce.PaymentModule.Core.Model.StoredCredentials;
using VirtoCommerce.PaymentModule.Core.Services;
using VirtoCommerce.PaymentModule.Model.Requests;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.ProductSubscription.Core;

namespace VirtoCommerce.ProductSubscription.Data.Payments;

/// <summary>
/// A stored-card payment method with no real gateway behind it. Test card numbers trigger every outcome, so the
/// subscription engine, xAPI, frontend and payment-failure flows can be built and tested before any sandbox exists.
/// Only registered when <c>ProductSubscription:EnableMockPaymentMethod</c> is true; never enable it in production.
/// </summary>
public class MockStoredCardPaymentMethod : PaymentMethod, ISupportStoredCredentialFlow, ISupportCaptureFlow, ISupportRefundFlow
{
    private readonly IStoredPaymentMethodService _storedPaymentMethodService;
    private readonly IStoredPaymentMethodSearchService _storedPaymentMethodSearchService;

    public MockStoredCardPaymentMethod(
        IStoredPaymentMethodService storedPaymentMethodService,
        IStoredPaymentMethodSearchService storedPaymentMethodSearchService)
        : base(ModuleConstants.MockPaymentMethodCode)
    {
        _storedPaymentMethodService = storedPaymentMethodService;
        _storedPaymentMethodSearchService = storedPaymentMethodSearchService;

        // Shown to customers when the store sets no localized name.
        Name = "Credit card (test)";
    }

    public static class TestCards
    {
        /// <summary>Always succeeds.</summary>
        public const string Success = "4242";
        /// <summary>The bank asks for 3-D Secure on the first (customer-present) payment.</summary>
        public const string ThreeDsOnSave = "3220";
        /// <summary>Saves fine; renewals require authentication (3-D Secure) again.</summary>
        public const string ThreeDsOnRenewal = "3184";
        /// <summary>Renewals get a temporary decline (insufficient funds).</summary>
        public const string InsufficientFunds = "9995";
        /// <summary>Renewals get a permanent decline (expired card).</summary>
        public const string Expired = "0069";
        /// <summary>Renewals get a permanent decline with Mastercard advice code 21 (recurring payments cancelled).</summary>
        public const string RecurringCancelled = "0021";
    }

    public const string ProviderErrorAmountSuffix = ".13";

    // Replays of the same idempotency key return the same result, like real providers.
    private static readonly ConcurrentDictionary<string, ProcessStoredPaymentRequestResult> _idempotencyCache = new();
    private static readonly ConcurrentDictionary<string, bool> _providerErrorsGiven = new();

    public override PaymentMethodType PaymentMethodType => PaymentMethodType.Standard;

    public override PaymentMethodGroupType PaymentMethodGroupType => PaymentMethodGroupType.BankCard;

    public override bool AllowCartPayment => false;

    public override Task<ProcessPaymentRequestResult> ProcessPaymentAsync(ProcessPaymentRequest request, CancellationToken cancellationToken = default)
    {
        var result = new ProcessPaymentRequestResult
        {
            IsSuccess = true,
            NewPaymentStatus = PaymentStatus.Pending,
            PublicParameters = new()
            {
                ["mode"] = "mock",
                ["testCards"] = string.Join(",", TestCards.Success, TestCards.ThreeDsOnSave, TestCards.ThreeDsOnRenewal, TestCards.InsufficientFunds, TestCards.Expired, TestCards.RecurringCancelled),
            },
        };

        return Task.FromResult(result);
    }

    public override Task<ValidatePostProcessRequestResult> ValidatePostProcessRequestAsync(System.Collections.Specialized.NameValueCollection queryString, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ValidatePostProcessRequestResult { IsSuccess = true });
    }

    /// <summary>
    /// The customer-present payment (checkout or order payment page). Parameters come from the storefront's mock card form:
    /// last4, brand, expMonth, expYear, cardholderName, storeForFutureUse, consentId, threeDsCompleted, storedPaymentMethodId.
    /// </summary>
    public override async Task<PostProcessPaymentRequestResult> PostProcessPaymentAsync(PostProcessPaymentRequest request, CancellationToken cancellationToken = default)
    {
        var parameters = request.Parameters ?? new System.Collections.Specialized.NameValueCollection();
        var payment = request.Payment as PaymentIn;
        var order = request.Order as CustomerOrder;

        var storedPaymentMethodId = parameters["storedPaymentMethodId"];
        StoredPaymentMethod storedMethod = null;
        if (!string.IsNullOrEmpty(storedPaymentMethodId))
        {
            storedMethod = await _storedPaymentMethodService.GetByIdAsync(storedPaymentMethodId);
            if (storedMethod == null || order == null || storedMethod.CustomerId != order.CustomerId || storedMethod.Status != StoredPaymentMethodStatus.Active)
            {
                return Fail("This saved card can't be used.");
            }
        }

        var last4 = storedMethod?.Last4 ?? new string((parameters["last4"] ?? string.Empty).Where(char.IsDigit).ToArray());
        if (last4.Length != 4)
        {
            return Fail("Enter a valid test card number.");
        }

        var threeDsCompleted = string.Equals(parameters["threeDsCompleted"], "true", StringComparison.OrdinalIgnoreCase);
        var needs3Ds = last4 == TestCards.ThreeDsOnSave || (storedMethod != null && last4 == TestCards.ThreeDsOnRenewal);
        if (needs3Ds && !threeDsCompleted)
        {
            return Fail("Your bank needs you to confirm this payment (3-D Secure).", "authentication_required");
        }

        if (last4 == TestCards.Expired)
        {
            return Fail("Your card has expired. Try another card.");
        }

        if (last4 == TestCards.RecurringCancelled && storedMethod != null)
        {
            return Fail("Your card was declined. Try another card.");
        }

        var transactionId = $"mock_txn_{Guid.NewGuid():N}";
        var networkTransactionId = $"mock_ntid_{Guid.NewGuid():N}";

        if (payment != null)
        {
            payment.OuterId = transactionId;
            payment.AuthorizedDate = DateTime.UtcNow;
            payment.IsApproved = true;
            payment.PaymentStatus = PaymentStatus.Authorized;
        }

        var storeForFutureUse = string.Equals(parameters["storeForFutureUse"], "true", StringComparison.OrdinalIgnoreCase);
        if (storeForFutureUse && storedMethod == null && order != null && !string.IsNullOrEmpty(order.CustomerId))
        {
            var newMethod = AbstractTypeFactory<StoredPaymentMethod>.TryCreateInstance();
            newMethod.StoreId = order.StoreId;
            newMethod.CustomerId = order.CustomerId;
            newMethod.OrganizationId = order.OrganizationId;
            newMethod.PaymentMethodCode = Code;
            newMethod.ProviderCustomerRef = $"mock_cus_{order.CustomerId}";
            newMethod.Token = $"mock_tok_{Guid.NewGuid():N}";
            newMethod.Brand = string.IsNullOrEmpty(parameters["brand"]) ? GuessBrand(parameters["firstDigit"]) : parameters["brand"];
            newMethod.Last4 = last4;
            newMethod.ExpirationMonth = int.TryParse(parameters["expMonth"], out var month) ? month : null;
            newMethod.ExpirationYear = NormalizeYear(parameters["expYear"]);
            newMethod.CardholderName = parameters["cardholderName"];
            newMethod.Fingerprint = $"mock_fp_{last4}";
            newMethod.NetworkTransactionId = networkTransactionId;
            newMethod.InitialTransactionRef = transactionId;
            newMethod.InitialOrderId = order.Id;
            newMethod.InitialPaymentId = payment?.Id;
            newMethod.Usage = StoredCredentialUsage.Recurring;
            newMethod.IsAuthenticated = threeDsCompleted;
            newMethod.ConsentId = parameters["consentId"];
            newMethod.IsDefault = !await HasStoredMethodsAsync(order.CustomerId, order.StoreId);

            await _storedPaymentMethodService.SaveChangesAsync([newMethod]);
        }
        else if (storedMethod != null)
        {
            storedMethod.LastUsedDate = DateTime.UtcNow;
            await _storedPaymentMethodService.SaveChangesAsync([storedMethod]);
        }

        return new PostProcessPaymentRequestResult
        {
            IsSuccess = true,
            NewPaymentStatus = PaymentStatus.Authorized,
            OuterId = transactionId,
            PublicParameters = new() { ["transactionId"] = transactionId },
        };
    }

    public override Task<StorePaymentMethodRequestResult> StorePaymentMethodAsync(StorePaymentMethodRequest request, CancellationToken cancellationToken = default)
    {
        var parameters = request.Parameters ?? new System.Collections.Specialized.NameValueCollection();
        var last4 = new string((parameters["last4"] ?? string.Empty).Where(char.IsDigit).ToArray());

        if (last4.Length != 4)
        {
            return Task.FromResult(new StorePaymentMethodRequestResult { Outcome = ChargeOutcome.HardDecline, IsSuccess = false, ErrorMessage = "Enter a valid test card number." });
        }

        if (last4 == TestCards.Expired)
        {
            return Task.FromResult(new StorePaymentMethodRequestResult { Outcome = ChargeOutcome.HardDecline, IsSuccess = false, ErrorMessage = "Your card has expired.", RawCode = "54" });
        }

        var threeDsCompleted = string.Equals(parameters["threeDsCompleted"], "true", StringComparison.OrdinalIgnoreCase);
        if (last4 == TestCards.ThreeDsOnSave && !threeDsCompleted)
        {
            return Task.FromResult(new StorePaymentMethodRequestResult { Outcome = ChargeOutcome.RequiresAction, IsSuccess = false, ErrorMessage = "Your bank needs you to confirm this card (3-D Secure).", RawCode = "authentication_required", PublicParameters = new() { ["code"] = "authentication_required" } });
        }

        var method = AbstractTypeFactory<StoredPaymentMethod>.TryCreateInstance();
        method.StoreId = request.StoreId;
        method.CustomerId = request.CustomerId;
        method.OrganizationId = request.OrganizationId;
        method.PaymentMethodCode = Code;
        method.ProviderCustomerRef = $"mock_cus_{request.CustomerId}";
        method.Token = $"mock_tok_{Guid.NewGuid():N}";
        method.Brand = string.IsNullOrEmpty(parameters["brand"]) ? GuessBrand(parameters["firstDigit"]) : parameters["brand"];
        method.Last4 = last4;
        method.ExpirationMonth = int.TryParse(parameters["expMonth"], out var month) ? month : null;
        method.ExpirationYear = NormalizeYear(parameters["expYear"]);
        method.CardholderName = parameters["cardholderName"];
        method.Fingerprint = $"mock_fp_{last4}";
        method.NetworkTransactionId = $"mock_ntid_{Guid.NewGuid():N}";
        method.Usage = request.Usage;
        method.IsAuthenticated = threeDsCompleted;
        method.ConsentId = request.ConsentId;

        return Task.FromResult(new StorePaymentMethodRequestResult { Outcome = ChargeOutcome.Succeeded, IsSuccess = true, StoredPaymentMethod = method });
    }

    public override Task<ProcessStoredPaymentRequestResult> ProcessStoredPaymentAsync(ProcessStoredPaymentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!string.IsNullOrEmpty(request.IdempotencyKey) && _idempotencyCache.TryGetValue(request.IdempotencyKey, out var cached))
        {
            return Task.FromResult(cached);
        }

        var result = Evaluate(request);

        // A provider error means "unknown": a retry with the same key must be allowed to succeed.
        if (!string.IsNullOrEmpty(request.IdempotencyKey) && result.Outcome != ChargeOutcome.ProviderError)
        {
            _idempotencyCache[request.IdempotencyKey] = result;
        }

        return Task.FromResult(result);
    }

    protected virtual ProcessStoredPaymentRequestResult Evaluate(ProcessStoredPaymentRequest request)
    {
        // Like a real gateway: there is nothing to charge without a stored card and its provider token.
        if (request.StoredPaymentMethod == null || string.IsNullOrEmpty(request.StoredPaymentMethod.Token))
        {
            return new ProcessStoredPaymentRequestResult { Outcome = ChargeOutcome.HardDecline, IsSuccess = false, RawCode = "invalid_token", RawMessage = "No stored card token", ErrorMessage = "There is no saved card to charge." };
        }

        var last4 = request.StoredPaymentMethod.Last4;
        var amountText = request.Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

        if (amountText.EndsWith(ProviderErrorAmountSuffix) && _providerErrorsGiven.TryAdd(request.IdempotencyKey ?? Guid.NewGuid().ToString(), true))
        {
            return new ProcessStoredPaymentRequestResult { Outcome = ChargeOutcome.ProviderError, IsSuccess = false, RawCode = "timeout", RawMessage = "Simulated gateway timeout" };
        }

        switch (last4)
        {
            case TestCards.ThreeDsOnRenewal when request.Reason != MerchantInitiatedReason.Reauthorization:
                return new ProcessStoredPaymentRequestResult { Outcome = ChargeOutcome.RequiresAction, IsSuccess = false, RawCode = "1A", RawMessage = "Authentication required", PublicParameters = new() { ["code"] = "authentication_required" } };

            case TestCards.InsufficientFunds:
                return new ProcessStoredPaymentRequestResult { Outcome = ChargeOutcome.SoftDecline, IsSuccess = false, RawCode = "51", AdviceCode = "02", RetryAfter = TimeSpan.FromHours(24), RawMessage = "Insufficient funds" };

            case TestCards.Expired:
                return new ProcessStoredPaymentRequestResult { Outcome = ChargeOutcome.HardDecline, IsSuccess = false, RawCode = "54", RawMessage = "Expired card" };

            case TestCards.RecurringCancelled:
                return new ProcessStoredPaymentRequestResult { Outcome = ChargeOutcome.HardDecline, IsSuccess = false, RawCode = "05", AdviceCode = "21", RawMessage = "Recurring payments cancelled by cardholder" };
        }

        var captured = request.CaptureMode == StoredPaymentCaptureMode.AuthorizeAndCapture;
        return new ProcessStoredPaymentRequestResult
        {
            Outcome = ChargeOutcome.Succeeded,
            IsSuccess = true,
            NewPaymentStatus = captured ? PaymentStatus.Paid : PaymentStatus.Authorized,
            TransactionId = $"mock_txn_{Guid.NewGuid():N}",
            NetworkTransactionId = request.StoredPaymentMethod?.NetworkTransactionId,
            AuthorizationCode = "MOCK" + Random.Shared.Next(100000, 999999),
            Amount = request.Amount,
        };
    }

    public override Task<CapturePaymentRequestResult> CaptureProcessPaymentAsync(CapturePaymentRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Payment is PaymentIn payment)
        {
            payment.CapturedDate = DateTime.UtcNow;
            payment.IsApproved = true;
        }

        return Task.FromResult(new CapturePaymentRequestResult { IsSuccess = true, NewPaymentStatus = PaymentStatus.Paid, OuterId = $"mock_cap_{Guid.NewGuid():N}" });
    }

    public override Task<RefundPaymentRequestResult> RefundProcessPaymentAsync(RefundPaymentRequest request, CancellationToken cancellationToken = default)
    {
        var status = request.IsPartialRefund ? PaymentStatus.PartiallyRefunded : PaymentStatus.Refunded;
        return Task.FromResult(new RefundPaymentRequestResult { IsSuccess = true, NewPaymentStatus = status, NewRefundStatus = RefundStatus.Processed, OuterId = $"mock_ref_{Guid.NewGuid():N}" });
    }

    public override Task<VoidPaymentRequestResult> VoidProcessPaymentAsync(VoidPaymentRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Payment is PaymentIn payment)
        {
            payment.VoidedDate = DateTime.UtcNow;
        }

        return Task.FromResult(new VoidPaymentRequestResult { IsSuccess = true, NewPaymentStatus = PaymentStatus.Voided });
    }

    public override Task<DeleteStoredPaymentMethodRequestResult> DeleteStoredPaymentMethodAsync(DeleteStoredPaymentMethodRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new DeleteStoredPaymentMethodRequestResult { IsSuccess = true });
    }

    private async Task<bool> HasStoredMethodsAsync(string customerId, string storeId)
    {
        var criteria = AbstractTypeFactory<StoredPaymentMethodSearchCriteria>.TryCreateInstance();
        criteria.CustomerId = customerId;
        criteria.StoreId = storeId;
        criteria.Statuses = [StoredPaymentMethodStatus.Active];
        criteria.Take = 0;
        return (await _storedPaymentMethodSearchService.SearchAsync(criteria)).TotalCount > 0;
    }

    private static PostProcessPaymentRequestResult Fail(string message, string code = null)
    {
        return new PostProcessPaymentRequestResult
        {
            IsSuccess = false,
            NewPaymentStatus = PaymentStatus.Declined,
            ErrorMessage = message,
            PublicParameters = code == null ? null : new() { ["code"] = code },
        };
    }

    private static string GuessBrand(string firstDigit)
    {
        return firstDigit switch
        {
            "4" => "Visa",
            "5" or "2" => "Mastercard",
            "3" => "American Express",
            "6" => "Discover",
            _ => "Card",
        };
    }

    private static int? NormalizeYear(string value)
    {
        if (!int.TryParse(value, out var year))
        {
            return null;
        }

        return year < 100 ? 2000 + year : year;
    }
}
