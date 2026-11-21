# Stored credential connector test kit

Contract tests for payment connectors that implement the stored credential (card on file) flow from
`VirtoCommerce.PaymentModule.Core`: `ISupportStoredCredentialFlow` with `StorePaymentMethodAsync`,
`ProcessStoredPaymentAsync` and `DeleteStoredPaymentMethodAsync`.

The subscription module charges saved cards with these methods (see `RenewalProcessor`). Every connector must behave the
same way for it to work: return the same outcomes for the same situations, reference the first transaction on every
merchant-initiated charge, and never charge twice for one idempotency key. The kit checks this with one set of tests.
The mock connector (`MockStoredCardPaymentMethod`) runs them in this repository
(`tests/VirtoCommerce.ProductSubscription.Tests/Data/Payments/MockConnectorContractTests.cs`). A real connector
(Authorize.Net, CyberSource, ...) runs the same tests against its sandbox.

This project is a class library, not a test project. It holds:

| Type | Purpose |
|---|---|
| `StoredCredentialConnectorContractTests` | Abstract xUnit v3 test class. Derive from it and pass a connector factory and a scenarios provider. |
| `IConnectorTestScenarios` / `ConnectorTestScenarios` | Supplies the inputs that make your sandbox produce each outcome. Every scenario except `Success` is optional. |
| `CardScenario`, `ChargeScenario`, `AuthenticationScenario`, `ProviderErrorScenario` | Scenario inputs: what to post to save a card, what amount to charge, what codes to expect. |
| `ConnectorCapabilities` | The optional features your connector supports (authorize-only, capture, delete...). |

## 1. Reference the kit

The connector's test project needs xUnit v3 (`xunit.v3`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`) and a
reference to the kit. Use one of these:

- **Project reference** from a sibling checkout of `vc-module-product-subscription`:

  ```xml
  <ProjectReference Include="..\..\..\vc-module-product-subscription\tests\VirtoCommerce.ProductSubscription.ConnectorTestKit\VirtoCommerce.ProductSubscription.ConnectorTestKit.csproj" />
  ```

- **Package** from a local feed until the kit is published:

  ```shell
  dotnet pack tests/VirtoCommerce.ProductSubscription.ConnectorTestKit -c Release -o ../.local-nuget -p:IsPackable=true
  ```

  ```xml
  <PackageReference Include="VirtoCommerce.ProductSubscription.ConnectorTestKit" Version="3.1000.0" />
  ```

The kit depends on `VirtoCommerce.PaymentModule.Core` 3.1011.0-alpha.315-vcst-6507 or later (the version that adds the stored
credential contract), `FluentAssertions` 7.2.0, `xunit.v3.assert` and `xunit.v3.extensibility.core` 3.2.2.

## 2. Implement the scenarios provider

Derive from `ConnectorTestScenarios` and override what your sandbox can simulate. Anything you leave null is
reported as skipped with the reason, so the test run shows what the sandbox didn't prove.

Read credentials from environment variables, never from committed files. Set `SkipReason` when they are missing, so
the tests skip on machines and CI jobs that don't have the secrets.

In the example, `SandboxTokenizer` and `SandboxChallenge` stand for the connector's own test helpers: the code that
gets a token for a test card the way the storefront's hosted fields would, and the code that completes the sandbox's
3-D Secure challenge.

```csharp
using System;
using System.Collections.Specialized;
using System.Threading.Tasks;
using VirtoCommerce.ProductSubscription.ConnectorTestKit;

public class AuthorizeNetSandboxScenarios : ConnectorTestScenarios
{
    public static string ApiLoginId => Environment.GetEnvironmentVariable("AUTHORIZENET_SANDBOX_API_LOGIN_ID");
    public static string TransactionKey => Environment.GetEnvironmentVariable("AUTHORIZENET_SANDBOX_TRANSACTION_KEY");

    public override string SkipReason => string.IsNullOrEmpty(ApiLoginId) || string.IsNullOrEmpty(TransactionKey)
        ? "Set AUTHORIZENET_SANDBOX_API_LOGIN_ID and AUTHORIZENET_SANDBOX_TRANSACTION_KEY to run the sandbox contract tests."
        : null;

    public override ChargeScenario Success => new()
    {
        Description = "Visa 4111 approved",
        Card = Visa("4111111111111111"),
        Amount = 12.00m,
    };

    // The sandbox declines by amount or by a magic ZIP code; use what your provider documents.
    public override ChargeScenario HardDecline => new()
    {
        Description = "decline triggered by ZIP 46282",
        Card = Visa("4111111111111111", zip: "46282"),
        Amount = 12.00m,
    };

    public override AuthenticationScenario ThreeDsOnStore => new()
    {
        Description = "3-D Secure challenge card",
        Card = Visa("4000000000001091"),
        // Do what the storefront does after the challenge, e.g. call the sandbox's simulated challenge endpoint,
        // and return the parameters of the second store request.
        CompleteAuthenticationAsync = async (request, requiresAction, cancellationToken) =>
        {
            var parameters = new NameValueCollection(request.Parameters);
            parameters["threeDsResult"] = await SandboxChallenge.CompleteAsync(requiresAction.RecoveryUrl, cancellationToken);
            return parameters;
        },
    };

    // Declare what the connector or sandbox can't do; the tests that need it are skipped, the rest still run.
    // Example: a sandbox that still charges deleted payment profiles.
    public override ConnectorCapabilities Capabilities { get; } = new() { RejectsDeletedToken = false };

    private static CardScenario Visa(string number, string zip = "98004") => new()
    {
        Description = $"Visa {number[^4..]}",
        StoreParameters = new NameValueCollection
        {
            // Whatever your connector expects from the storefront: an Accept.js opaque data token, a Flex token, ...
            ["opaqueDataValue"] = SandboxTokenizer.Tokenize(number, "12", "2030"),
            ["zip"] = zip,
        },
        ExpectedLast4 = number[^4..],
        ExpectedBrand = "Visa",
        CardNumber = number, // checked never to appear in the stored payment method
    };
}
```

Other members:

| Member | When to override |
|---|---|
| `SoftDecline`, `HardDeclineWithAdviceCode`, `RequiresActionOnMerchantInitiatedCharge`, `DeclinedOnStore`, `ProviderError` | When the sandbox can trigger the outcome. `ChargeScenario.AfterStore` changes the saved method before the charge, to simulate a card that expires or is cancelled after it was saved. `ExpectedRawCode`, `ExpectedAdviceCode` and `ExpectRetryAfter` pin the codes the sandbox returns. |
| `RequiresActionPublicParameters` | When a `RequiresAction` result tells the storefront what to do through `PublicParameters` (e.g. a client secret) instead of `RecoveryUrl`. List the documented keys. |
| `GetReferencedNetworkTransactionIdAsync` | Return the original network transaction id your connector sent on the wire, e.g. read from a recording `HttpMessageHandler` or the provider's transaction-details API. If you return null, the test that needs it is skipped. |
| `CreateCaptureRequest` | When your `CaptureProcessPaymentAsync` reads more than `OuterId` and `CaptureAmount`, e.g. a `PaymentIn` in `Payment`. |
| `KnownIssues` | Test name to reason. The test is reported as skipped with that reason. Start the reason with `BUG:` for a real defect, so it stays visible until it is fixed. |
| `StoreId`, `Currency` | The sandbox store and currency. |

## 3. Derive the test class

```csharp
using VirtoCommerce.ProductSubscription.ConnectorTestKit;
using Xunit;

[Trait("Category", "Sandbox")]
public class AuthorizeNetContractTests()
    : StoredCredentialConnectorContractTests(
        () => new AuthorizeNetPaymentMethod(/* sandbox settings built from AuthorizeNetSandboxScenarios.ApiLoginId ... */),
        new AuthorizeNetSandboxScenarios());
```

The factory runs once per test, and only for tests that aren't skipped, so it can't break a run without secrets.
Override `CreateStoreRequest` or `CreateChargeRequest` when your provider needs more on the request (an `Order`, a
`Payment`, an invoice number). Tests that store cards delete them afterwards when `SupportsDelete` is true, so the
sandbox account doesn't fill up with test profiles.

## 4. Run against the sandbox

```shell
# PowerShell
$env:AUTHORIZENET_SANDBOX_API_LOGIN_ID = "..."
$env:AUTHORIZENET_SANDBOX_TRANSACTION_KEY = "..."
dotnet test --filter "Category=Sandbox"
```

```shell
# bash
export AUTHORIZENET_SANDBOX_API_LOGIN_ID=...
export AUTHORIZENET_SANDBOX_TRANSACTION_KEY=...
dotnet test --filter "Category=Sandbox"
```

In CI, map repository secrets to the same environment variables, and run the sandbox job only where the secrets are
available (not on pull requests from forks). Don't put credentials in `appsettings*.json`, `launchSettings.json`,
`.runsettings` or anything else that gets committed.

Every scenario uses a new customer id and idempotency key, so runs don't interfere with each other. Sandboxes are
slow and rate-limited, so keep these tests out of the default unit test run.

## What the tests cover

The tests check what the connector controls. Retry schedules and limits are applied by the platform (`RenewalProcessor`:
retry schedule, `MaxRetriesPer30Days`, at most five provider errors per renewal). The connector's part is to classify
each result correctly and pass on the codes the platform needs to apply those limits.

### Card network stored credential framework checklist

The Visa Stored Credential Framework and the Mastercard Standards for Credential-on-File, Recurring and Installment
payments require the items below. Each row lists the tests that prove the connector's part. Card network rules change,
so check the current Visa Core Rules and Mastercard Transaction Processing Rules before a certification.

| Requirement | Network rule | Tests |
|---|---|---|
| **First storage is customer-initiated and the customer consented.** The consent record (terms, purpose) is linked to the stored credential. | Visa and Mastercard: get the cardholder's consent and disclose the terms before storing; the first transaction that stores the credential is customer-initiated (CIT) and flagged as the initial storage. | `Store_SuccessfulCard_KeepsConsentUsageAndConnectorCode` (ConsentId and Usage carried to the stored method) |
| **Storage is verified.** With no payment due, the card is verified with a $0 (or minimal) authorization; a declined verification stores nothing. | Visa and Mastercard: use an account verification when no amount is due at storage. | `Store_SuccessfulCard_ReturnsTokenCardDetailsAndInitialTransactionReference`, `Store_DeclinedCard_IsNotStored` |
| **The first transaction is authenticated where required.** 3-D Secure on the CIT that stores the card; the storefront gets what it needs to run the challenge. | PSD2 SCA / EMV 3-D Secure: authenticate the CIT; later MITs are out of scope of SCA because they reference it. | `Store_ThreeDsCard_RequiresActionThenSucceedsAfterAuthentication`, `Store_RequiresAction_GivesStorefrontARecoveryPath` |
| **The first transaction's network id is kept.** The stored method returns `NetworkTransactionId` (or `InitialTransactionRef`). | Visa: Transaction Identifier of the initial CIT. Mastercard: Trace ID / network reference of the initial CIT. | `Store_SuccessfulCard_ReturnsTokenCardDetailsAndInitialTransactionReference` |
| **Every MIT references the original transaction**, flagged as subsequent, not the previous charge. | Visa and Mastercard: merchant-initiated transactions carry the stored credential indicator and the original transaction id. | `Charge_Subsequent_ReferencesOriginalNetworkTransactionId` |
| **MITs carry the right reason.** Recurring, unscheduled and the industry-practice reasons (reauthorization, resubmission, delayed charge) are accepted. | Visa MIT types: recurring, unscheduled credential-on-file, reauthorization, resubmission, delayed charges. Mastercard: equivalent MIT categories. | `Charge_EveryMerchantInitiatedReason_IsAccepted` |
| **Authorize at renewal, capture at shipment; reauthorize when the authorization expires.** | Visa and Mastercard: authorization validity periods; a reauthorization MIT replaces an expired authorization. | `Charge_AuthorizeOnly_IsAuthorizedWithTransactionAndAmount`, `AuthorizeOnly_ThenCapture_IsPaid`, `Charge_AuthorizeAndCapture_IsPaid`, `Charge_EveryMerchantInitiatedReason_IsAccepted` (Reauthorization) |
| **Retry limits: permanent declines are never retried.** A hard decline is classified `HardDecline`, carries the raw code, and gives no `RetryAfter`. | Visa decline categories: category 1 ("issuer will never approve") must not be retried. Mastercard: Merchant Advice Codes 03 and 21 mean do not retry. | `Charge_HardDecline_IsClassifiedWithRawCodeAndNoRetry`, `Charge_HardDeclineWithAdviceCode_KeepsAdviceCode`, `Delete_StoredMethod_TokenCanNoLongerBeCharged` |
| **Retry limits: temporary declines are retried within limits and timing.** A soft decline is classified `SoftDecline` with its raw code, advice code and `RetryAfter` when the provider gives one. | Visa: categories 2 and 3 may be retried, at most 15 times in 30 days. Mastercard: Merchant Advice Codes 01, 02 and 24 to 30 say when (and whether) to retry; excessive retries of a declined card are charged. | `Charge_SoftDecline_IsClassifiedWithCodesAndRetryTiming` |
| **Retry limits: a replay is not a new attempt.** The same idempotency key never charges twice and replays the first result, declines included; a new key is a new charge. | Prevents duplicate charges and retries counted against the network limits. | `Charge_SameIdempotencyKey_NeverChargesTwice`, `Charge_SameIdempotencyKey_ReplaysDecline`, `Charge_DifferentIdempotencyKeys_ChargeIndependently` |
| **Retry limits: gateway errors aren't declines.** A timeout or processor error is `ProviderError` and can be retried with the same key. | A request that never reached the issuer isn't a declined attempt; retrying it must not create a second charge. | `Charge_ProviderError_IsClassifiedAndRetryableWithSameKey` |
| **An authentication request on an MIT brings the customer back.** `RequiresAction` is never treated as a decline or retried as an MIT, and gives the storefront a recovery path. | Visa response code 1A / Mastercard 65 (SCA required): the next attempt must be customer-initiated with authentication. | `Charge_RequiresAction_IsClassifiedAndNotReportedAsDecline`, `Charge_RequiresAction_GivesStorefrontARecoveryPath` |
| **Cancelled credentials are removed.** Deleting a stored method succeeds and its token can't be charged. | Visa and Mastercard: stop using a credential when the cardholder cancels the agreement or removes the card. | `Delete_StoredMethod_Succeeds`, `Delete_StoredMethod_TokenCanNoLongerBeCharged` |
| **No PAN or CVV in Virto.** Only the provider token, display data and network references are stored. | PCI DSS: don't store sensitive authentication data; keep the PAN out of the platform. | `Store_SuccessfulCard_ReturnsTokenCardDetailsAndInitialTransactionReference` (with `CardScenario.CardNumber`) |
| **A charge needs a stored credential.** No stored method or no token never succeeds. | Basic safety: an MIT without a stored credential is invalid. | `Charge_WithoutStoredPaymentMethod_NeverSucceeds`, `Charge_StoredMethodWithoutToken_NeverSucceeds` |

### Connector shape

| Requirement | Tests |
|---|---|
| The connector implements `ISupportStoredCredentialFlow` and overrides all three methods (the `PaymentMethod` defaults throw `NotImplementedException`). | `Connector_ImplementsStoredCredentialFlow` |
| A connector that captures declares `ISupportCaptureFlow`; one that can't capture doesn't, and doesn't claim authorize-only support. | `Connector_CaptureSupport_MatchesCaptureFlowDeclaration` |
| An unsupported feature fails explicitly and the same way everywhere: an unsuccessful result or `NotSupportedException`, never success or `NotImplementedException`. Prefer an unsuccessful result for deletion: an exception from `DeleteStoredPaymentMethodAsync` fails the storefront's remove-card command, so the card is never revoked in Virto. | `UnsupportedFeatures_FailExplicitlyAndConsistently` |
