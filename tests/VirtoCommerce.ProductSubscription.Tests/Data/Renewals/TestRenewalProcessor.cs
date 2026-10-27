using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VirtoCommerce.OrdersModule.Core.Services;
using VirtoCommerce.PaymentModule.Core.Services;
using VirtoCommerce.Platform.Core.DistributedLock;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.ProductSubscription.Data.Renewals;
using VirtoCommerce.ProductSubscription.Data.Services;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.ProductSubscription.Tests.Data.Renewals;

/// <summary>The renewal processor on a clock the test moves.</summary>
public class TestRenewalProcessor(
    ISubscriptionService subscriptionService,
    ISubscriptionSearchService subscriptionSearchService,
    IRenewalService renewalService,
    IRenewalSearchService renewalSearchService,
    IRenewalCartBuilder cartBuilder,
    ICustomerOrderService customerOrderService,
    IPaymentFlowService paymentFlowService,
    IPaymentMethodsSearchService paymentMethodsSearchService,
    IStoredPaymentMethodService storedPaymentMethodService,
    IStoreService storeService,
    IStoreSearchService storeSearchService,
    ISettingsManager settingsManager,
    ISubscriptionNotificationService notificationService,
    IDistributedLock distributedLock,
    ILogger<RenewalProcessor> logger)
    : RenewalProcessor(subscriptionService, subscriptionSearchService, renewalService, renewalSearchService, cartBuilder, customerOrderService,
        paymentFlowService, paymentMethodsSearchService, storedPaymentMethodService, storeService, storeSearchService, settingsManager,
        notificationService, distributedLock, logger)
{
    public DateTime Now { get; set; } = DateTime.UtcNow;

    protected override DateTime UtcNow => Now;

    protected override Task<int> GetAuthorizationLifetimeMinutesAsync() => Task.FromResult(7 * 24 * 60);
}
