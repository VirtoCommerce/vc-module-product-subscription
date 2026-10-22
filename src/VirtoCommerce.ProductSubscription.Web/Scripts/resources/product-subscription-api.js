angular.module('VirtoCommerce.ProductSubscription')
    .factory('VirtoCommerce.ProductSubscription.webApi', ['$resource', function ($resource) {
        return $resource('api/product-subscription', {}, {
            searchPlans: { method: 'POST', url: 'api/product-subscription/plans/search' },
            getPlan: { method: 'GET', url: 'api/product-subscription/plans/:id' },
            savePlan: { method: 'POST', url: 'api/product-subscription/plans' },
            deletePlans: { method: 'DELETE', url: 'api/product-subscription/plans' },

            searchSubscriptions: { method: 'POST', url: 'api/product-subscription/subscriptions/search' },
            getSubscription: { method: 'GET', url: 'api/product-subscription/subscriptions/:id' },
            changeSubscription: { method: 'PUT', url: 'api/product-subscription/subscriptions/:id' },
            skipSubscription: { method: 'POST', url: 'api/product-subscription/subscriptions/:id/skip' },
            pauseSubscription: { method: 'POST', url: 'api/product-subscription/subscriptions/:id/pause' },
            resumeSubscription: { method: 'POST', url: 'api/product-subscription/subscriptions/:id/resume' },
            cancelSubscription: { method: 'POST', url: 'api/product-subscription/subscriptions/:id/cancel' },
            orderSubscriptionNow: { method: 'POST', url: 'api/product-subscription/subscriptions/:id/order-now' },
            deleteSubscriptions: { method: 'DELETE', url: 'api/product-subscription/subscriptions' },
            savedPaymentMethods: { method: 'GET', url: 'api/product-subscription/customers/:customerId/saved-payment-methods', isArray: true },
            getConsent: { method: 'GET', url: 'api/product-subscription/consents/:id' },

            searchRenewals: { method: 'POST', url: 'api/product-subscription/renewals/search' },
            getRenewal: { method: 'GET', url: 'api/product-subscription/renewals/:id' },
            processRenewal: { method: 'POST', url: 'api/product-subscription/renewals/:id/process' },
            skipRenewal: { method: 'POST', url: 'api/product-subscription/renewals/:id/skip' },
            acceptRenewal: { method: 'POST', url: 'api/product-subscription/renewals/:id/accept' },
            retryRenewalPayment: { method: 'POST', url: 'api/product-subscription/renewals/:id/retry-payment' },
            cancelRenewal: { method: 'POST', url: 'api/product-subscription/renewals/:id/cancel' },
            runScheduler: { method: 'POST', url: 'api/product-subscription/scheduler/run' },
        });
    }]);
