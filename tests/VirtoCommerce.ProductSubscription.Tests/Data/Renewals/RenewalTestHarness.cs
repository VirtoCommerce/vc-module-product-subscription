using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VirtoCommerce.CatalogModule.Core.Services;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.OrdersModule.Core.Services;
using VirtoCommerce.PaymentModule.Core.Model;
using VirtoCommerce.PaymentModule.Core.Model.Search;
using VirtoCommerce.PaymentModule.Core.Model.StoredCredentials;
using VirtoCommerce.PaymentModule.Core.Services;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.ProductSubscription.Core;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.ProductSubscription.Data.Renewals;
using VirtoCommerce.ProductSubscription.Data.Services;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Model.Search;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.ProductSubscription.Tests.Data.Renewals;

/// <summary>
/// The renewal processor and the subscription manager on in-memory data that behaves like the database: every read
/// returns a copy, searches filter, sort and page like the search services, and saves replace the stored copy.
/// </summary>
public class RenewalTestHarness
{
    public const string StoreId = "store-1";

    private readonly object _sync = new();
    private readonly Dictionary<string, Subscription> _subscriptions = [];
    private readonly Dictionary<string, Renewal> _renewals = [];
    private readonly Dictionary<string, CustomerOrder> _orders = [];
    private int _numbers;

    public RenewalTestHarness()
    {
        Store = new Store
        {
            Id = StoreId,
            Settings =
            [
                new ObjectSettingEntry { Name = ModuleConstants.Settings.Store.ReminderLeadDays.Name, Value = 3 },
                new ObjectSettingEntry { Name = ModuleConstants.Settings.Store.CutoffHours.Name, Value = 0 },
                new ObjectSettingEntry { Name = ModuleConstants.Settings.Store.AnnualReminderEnabled.Name, Value = true },
            ],
        };

        Card = new StoredPaymentMethod
        {
            Id = "card-1",
            StoreId = StoreId,
            PaymentMethodCode = PaymentMethod.Code,
            Status = StoredPaymentMethodStatus.Active,
        };

        var subscriptionService = new Mock<ISubscriptionService>();
        subscriptionService
            .Setup(x => x.GetAsync(It.IsAny<IList<string>>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync((IList<string> ids, string _, bool _) => Read(() => ids.Where(_subscriptions.ContainsKey).Select(id => Copy(_subscriptions[id])).ToList()));
        subscriptionService
            .Setup(x => x.SaveChangesAsync(It.IsAny<IList<Subscription>>()))
            .Returns((IList<Subscription> models) => Write(() =>
            {
                foreach (var model in models)
                {
                    model.Id ??= Guid.NewGuid().ToString("N");
                    _subscriptions[model.Id] = Copy(model);
                }
            }));

        var subscriptionSearchService = new Mock<ISubscriptionSearchService>();
        subscriptionSearchService
            .Setup(x => x.SearchAsync(It.IsAny<SubscriptionSearchCriteria>(), It.IsAny<bool>()))
            .ReturnsAsync((SubscriptionSearchCriteria criteria, bool _) => Read(() => SearchSubscriptions(criteria)));

        var renewalService = new Mock<IRenewalService>();
        renewalService
            .Setup(x => x.GetAsync(It.IsAny<IList<string>>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync((IList<string> ids, string _, bool _) => Read(() => ids.Where(_renewals.ContainsKey).Select(id => Copy(_renewals[id])).ToList()));
        renewalService
            .Setup(x => x.SaveChangesAsync(It.IsAny<IList<Renewal>>()))
            .Returns((IList<Renewal> models) => Write(() =>
            {
                foreach (var model in models)
                {
                    model.Id ??= Guid.NewGuid().ToString("N");
                    model.Number ??= $"REN-{++_numbers}";
                    model.IdempotencyKey ??= Guid.NewGuid().ToString("N");
                    _renewals[model.Id] = Copy(model);
                }
            }));

        var renewalSearchService = new Mock<IRenewalSearchService>();
        renewalSearchService
            .Setup(x => x.SearchAsync(It.IsAny<RenewalSearchCriteria>(), It.IsAny<bool>()))
            .ReturnsAsync((RenewalSearchCriteria criteria, bool _) => Read(() => SearchRenewals(criteria)));

        CartBuilder
            .Setup(x => x.BuildAsync(It.IsAny<Renewal>(), It.IsAny<IList<Subscription>>()))
            .ReturnsAsync((Renewal renewal, IList<Subscription> subscriptions) => BuildCart(renewal, subscriptions));
        CartBuilder
            .Setup(x => x.PlaceOrderAsync(It.IsAny<Renewal>(), It.IsAny<RenewalCartResult>()))
            .Returns((Renewal renewal, RenewalCartResult cart) => PlaceOrderAsync(renewal, cart));

        var customerOrderService = new Mock<ICustomerOrderService>();
        customerOrderService
            .Setup(x => x.GetAsync(It.IsAny<IList<string>>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync((IList<string> ids, string _, bool _) => Read(() => ids.Where(x => x != null && _orders.ContainsKey(x)).Select(id => _orders[id]).ToList()));
        customerOrderService
            .Setup(x => x.SaveChangesAsync(It.IsAny<IList<CustomerOrder>>()))
            .Returns((IList<CustomerOrder> models) => Write(() =>
            {
                foreach (var model in models)
                {
                    _orders[model.Id] = model;
                }
            }));

        var paymentMethodsSearchService = new Mock<IPaymentMethodsSearchService>();
        paymentMethodsSearchService
            .Setup(x => x.SearchAsync(It.IsAny<PaymentMethodsSearchCriteria>(), It.IsAny<bool>()))
            .ReturnsAsync(new PaymentMethodsSearchResult { Results = [PaymentMethod], TotalCount = 1 });

        var storedPaymentMethodService = new Mock<IStoredPaymentMethodService>();
        storedPaymentMethodService
            .Setup(x => x.GetAsync(It.IsAny<IList<string>>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync((IList<string> ids, string _, bool _) => ids.Contains(Card.Id) ? [Card] : []);

        var storeService = new Mock<IStoreService>();
        storeService
            .Setup(x => x.GetAsync(It.IsAny<IList<string>>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync((IList<string> ids, string _, bool _) => ids.Contains(StoreId) ? [Store] : []);

        var storeSearchService = new Mock<IStoreSearchService>();
        storeSearchService
            .Setup(x => x.SearchAsync(It.IsAny<StoreSearchCriteria>(), It.IsAny<bool>()))
            .ReturnsAsync((StoreSearchCriteria criteria, bool _) => new StoreSearchResult { Results = criteria.Skip == 0 ? [Store] : [], TotalCount = 1 });

        Processor = new TestRenewalProcessor(
            subscriptionService.Object,
            subscriptionSearchService.Object,
            renewalService.Object,
            renewalSearchService.Object,
            CartBuilder.Object,
            customerOrderService.Object,
            Mock.Of<IPaymentFlowService>(),
            paymentMethodsSearchService.Object,
            storedPaymentMethodService.Object,
            storeService.Object,
            storeSearchService.Object,
            Mock.Of<ISettingsManager>(),
            Notifications.Object,
            Lock,
            NullLogger<RenewalProcessor>.Instance);

        Manager = new SubscriptionManager(
            subscriptionService.Object,
            subscriptionSearchService.Object,
            Mock.Of<ISubscriptionPlanService>(),
            Mock.Of<ISubscriptionCartItemService>(),
            Mock.Of<ISubscriptionCartItemSearchService>(),
            renewalService.Object,
            storedPaymentMethodService.Object,
            Mock.Of<IStoredPaymentMethodSearchService>(),
            Mock.Of<IPaymentConsentSearchService>(),
            Notifications.Object,
            Mock.Of<IItemService>(),
            Lock,
            NullLogger<SubscriptionManager>.Instance);
    }

    public Store Store { get; }

    public StoredPaymentMethod Card { get; }

    public ScriptedStoredCardPaymentMethod PaymentMethod { get; } = new();

    public InProcessDistributedLock Lock { get; } = new();

    public Mock<IRenewalCartBuilder> CartBuilder { get; } = new();

    public Mock<ISubscriptionNotificationService> Notifications { get; } = new();

    public TestRenewalProcessor Processor { get; }

    public SubscriptionManager Manager { get; }

    /// <summary>Runs while an order is being placed, before it exists: lets a test act in the middle of order placement.</summary>
    public Func<Renewal, Task> OnPlaceOrder { get; set; }

    public DateTime Now
    {
        get => Processor.Now;
        set => Processor.Now = value;
    }

    public Subscription AddSubscription(string customerId, DateTime nextOrderDate, Action<Subscription> configure = null)
    {
        var subscription = new Subscription
        {
            Id = Guid.NewGuid().ToString("N"),
            Number = $"SUB-{Guid.NewGuid():N}"[..12],
            StoreId = StoreId,
            CustomerId = customerId,
            ProductId = "product-1",
            ProductName = "Coffee",
            Quantity = 1,
            Status = SubscriptionStatus.Active,
            StartDate = Now.AddMonths(-1),
            CreatedDate = Now.AddMonths(-1),
            NextOrderDate = nextOrderDate,
            AnchorDay = nextOrderDate.Day,
            LastAnnualReminderDate = Now.AddMonths(-1),
            Currency = "USD",
            PaymentMethodCode = PaymentMethod.Code,
            StoredPaymentMethodId = Card.Id,
        };

        configure?.Invoke(subscription);
        Write(() => _subscriptions[subscription.Id] = Copy(subscription)).GetAwaiter().GetResult();
        return subscription;
    }

    public void AddRenewal(Renewal renewal)
    {
        Write(() => _renewals[renewal.Id] = Copy(renewal)).GetAwaiter().GetResult();
    }

    public Subscription GetSubscription(string id) => Read(() => Copy(_subscriptions[id]));

    public Renewal GetRenewal(string id) => Read(() => Copy(_renewals[id]));

    public IList<Renewal> GetRenewals() => Read(() => _renewals.Values.Select(Copy).ToList());

    public IList<Renewal> GetRenewalsOf(string subscriptionId) => GetRenewals().Where(x => x.Items.Any(i => i.SubscriptionId == subscriptionId)).ToList();

    public CustomerOrder GetOrder(string id) => Read(() => _orders[id]);

    private SubscriptionSearchResult SearchSubscriptions(SubscriptionSearchCriteria criteria)
    {
        IEnumerable<Subscription> query = _subscriptions.Values;

        if (criteria.ObjectIds?.Count > 0)
        {
            query = query.Where(x => criteria.ObjectIds.Contains(x.Id));
        }

        if (!string.IsNullOrEmpty(criteria.StoreId))
        {
            query = query.Where(x => x.StoreId == criteria.StoreId);
        }

        if (!string.IsNullOrEmpty(criteria.CustomerId))
        {
            query = query.Where(x => x.CustomerId == criteria.CustomerId);
        }

        if (criteria.Statuses?.Count > 0)
        {
            query = query.Where(x => criteria.Statuses.Contains(x.Status));
        }

        if (!string.IsNullOrEmpty(criteria.InitialOrderId))
        {
            query = query.Where(x => x.InitialOrderId == criteria.InitialOrderId);
        }

        if (criteria.NextOrderDateBefore != null)
        {
            query = query.Where(x => x.NextOrderDate <= criteria.NextOrderDateBefore);
        }

        if (criteria.WithoutOpenRenewal == true)
        {
            query = query.Where(x => x.CurrentRenewalId == null
                || !(_renewals.TryGetValue(x.CurrentRenewalId, out var renewal) && renewal.IsOpen && renewal.Items.Any(i => i.SubscriptionId == x.Id)));
        }

        if (!string.IsNullOrEmpty(criteria.CurrentRenewalId))
        {
            query = query.Where(x => x.CurrentRenewalId == criteria.CurrentRenewalId);
        }

        if (criteria.PausedUntilBefore != null)
        {
            query = query.Where(x => x.PausedUntil != null && x.PausedUntil <= criteria.PausedUntilBefore);
        }

        if (criteria.AnnualReminderDueBefore != null)
        {
            query = query.Where(x => (x.LastAnnualReminderDate ?? x.StartDate) <= criteria.AnnualReminderDueBefore);
        }

        // Like SubscriptionSearchService: newest first unless a sort is given.
        var sortColumn = criteria.Sort?.Split(';')[0];
        var sorted = sortColumn switch
        {
            nameof(Subscription.NextOrderDate) => query.OrderBy(x => x.NextOrderDate),
            nameof(Subscription.PausedUntil) => query.OrderBy(x => x.PausedUntil),
            nameof(Subscription.StartDate) => query.OrderBy(x => x.StartDate),
            null or "" => query.OrderByDescending(x => x.CreatedDate),
            _ => throw new NotSupportedException($"Sort by {criteria.Sort}"),
        };

        var all = sorted.ThenBy(x => x.Id, StringComparer.Ordinal).ToList();
        return new SubscriptionSearchResult
        {
            TotalCount = all.Count,
            Results = all.Skip(criteria.Skip).Take(criteria.Take).Select(Copy).ToList(),
        };
    }

    private RenewalSearchResult SearchRenewals(RenewalSearchCriteria criteria)
    {
        IEnumerable<Renewal> query = _renewals.Values;

        if (!string.IsNullOrEmpty(criteria.OrderId))
        {
            query = query.Where(x => x.OrderId == criteria.OrderId);
        }

        if (!string.IsNullOrEmpty(criteria.SubscriptionId))
        {
            query = query.Where(x => x.Items.Any(i => i.SubscriptionId == criteria.SubscriptionId));
        }

        if (criteria.States?.Count > 0)
        {
            query = query.Where(x => criteria.States.Contains(x.State));
        }

        if (criteria.OnlyOpen == true)
        {
            query = query.Where(x => x.IsOpen);
        }

        if (criteria.DueBefore != null)
        {
            query = query.Where(x => x.NextProcessingDate != null && x.NextProcessingDate <= criteria.DueBefore);
        }

        var all = query.OrderBy(x => x.NextProcessingDate).ThenBy(x => x.Id, StringComparer.Ordinal).ToList();
        return new RenewalSearchResult
        {
            TotalCount = all.Count,
            Results = all.Skip(criteria.Skip).Take(criteria.Take).Select(Copy).ToList(),
        };
    }

    private static RenewalCartResult BuildCart(Renewal renewal, IList<Subscription> subscriptions)
    {
        var result = new RenewalCartResult();
        foreach (var item in renewal.Items)
        {
            var copy = (RenewalItem)item.Clone();
            var subscription = subscriptions.FirstOrDefault(x => x.Id == item.SubscriptionId);
            copy.IsAvailable = true;
            copy.Quantity = subscription?.Quantity ?? item.Quantity;
            copy.ListPrice = 10;
            copy.Total = copy.IsExcluded ? 0 : 10 * copy.Quantity;
            result.Items.Add(copy);
        }

        result.Total = result.Items.Sum(x => x.Total);
        return result;
    }

    private async Task<CustomerOrder> PlaceOrderAsync(Renewal renewal, RenewalCartResult cart)
    {
        if (OnPlaceOrder != null)
        {
            await OnPlaceOrder(renewal);
        }

        var order = new CustomerOrder
        {
            Id = Guid.NewGuid().ToString("N"),
            Number = $"CO-{Guid.NewGuid():N}"[..10],
            StoreId = renewal.StoreId,
            CustomerId = renewal.CustomerId,
            Currency = renewal.Currency,
            Total = cart.Total,
            InPayments =
            [
                new PaymentIn
                {
                    Id = Guid.NewGuid().ToString("N"),
                    GatewayCode = PaymentMethod.Code,
                    Sum = cart.Total,
                    PaymentStatus = PaymentStatus.New,
                },
            ],
        };

        await Write(() => _orders[order.Id] = order);
        return order;
    }

    private T Read<T>(Func<T> read)
    {
        lock (_sync)
        {
            return read();
        }
    }

    private Task Write(Action write)
    {
        lock (_sync)
        {
            write();
        }

        return Task.CompletedTask;
    }

    private static Subscription Copy(Subscription subscription) => (Subscription)subscription.Clone();

    private static Renewal Copy(Renewal renewal) => (Renewal)renewal.Clone();
}
