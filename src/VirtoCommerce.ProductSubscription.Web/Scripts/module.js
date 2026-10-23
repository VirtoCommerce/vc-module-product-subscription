var moduleName = 'VirtoCommerce.ProductSubscription';

if (AppDependencies !== undefined) {
    AppDependencies.push(moduleName);
}

angular.module(moduleName, [])
    .config(['$stateProvider',
        function ($stateProvider) {
            $stateProvider
                .state('workspace.productSubscriptionState', {
                    url: '/product-subscriptions',
                    templateUrl: '$(Platform)/Scripts/common/templates/home.tpl.html',
                    controller: [
                        'platformWebApp.bladeNavigationService',
                        function (bladeNavigationService) {
                            bladeNavigationService.showBlade({
                                id: 'productSubscriptionList',
                                controller: 'VirtoCommerce.ProductSubscription.subscriptionListController',
                                template: 'Modules/$(VirtoCommerce.ProductSubscription)/Scripts/blades/subscription-list.html',
                                isExpandable: true,
                                isClosingDisabled: true,
                            });
                        }
                    ]
                });
        }
    ])
    .run(['platformWebApp.mainMenuService', '$state', 'platformWebApp.widgetService',
        function (mainMenuService, $state, widgetService) {
            mainMenuService.addMenuItem({
                path: 'browse/product-subscriptions',
                icon: 'fas fa-sync-alt',
                title: 'product-subscription.main-menu-title',
                priority: 95,
                action: function () { $state.go('workspace.productSubscriptionState'); },
                permission: 'product-subscription:access',
            });

            // Subscriptions of a customer, on the customer detail blade.
            widgetService.registerWidget({
                controller: 'VirtoCommerce.ProductSubscription.customerSubscriptionsWidgetController',
                template: 'Modules/$(VirtoCommerce.ProductSubscription)/Scripts/widgets/customer-subscriptions-widget.html',
                isVisible: function (blade) { return !blade.isNew; },
            }, 'customerDetail1');
        }
    ])
    .factory('VirtoCommerce.ProductSubscription.labels', [function () {
        var statusClasses = {
            PendingActivation: 'ps-pill __pending',
            Active: 'ps-pill __active',
            Paused: 'ps-pill __paused',
            PastDue: 'ps-pill __danger',
            Cancelled: 'ps-pill __muted',
            Scheduled: 'ps-pill __pending',
            Notified: 'ps-pill __pending',
            ReviewRequired: 'ps-pill __warning',
            Ordered: 'ps-pill __pending',
            PaymentActionRequired: 'ps-pill __warning',
            RetryScheduled: 'ps-pill __warning',
            Authorized: 'ps-pill __active',
            Completed: 'ps-pill __active',
            Failed: 'ps-pill __danger',
            Skipped: 'ps-pill __muted',
        };

        return {
            statusClass: function (status) { return statusClasses[status] || 'ps-pill'; },
            interval: function (unit, count) {
                if (!unit) { return ''; }
                var name = unit.toLowerCase();
                return count === 1 ? 'every ' + name : 'every ' + count + ' ' + name + 's';
            },
            discount: function (type, value) {
                if (!value || type === 'None') { return '—'; }
                return type === 'Percent' ? value + '%' : value;
            },
            subscriptionStatuses: ['PendingActivation', 'Active', 'Paused', 'PastDue', 'Cancelled'],
            renewalStates: ['Scheduled', 'Notified', 'ReviewRequired', 'Ordered', 'PaymentActionRequired', 'RetryScheduled', 'Authorized', 'Completed', 'Failed', 'Skipped', 'Cancelled'],
            intervalUnits: ['Day', 'Week', 'Month', 'Year'],
            discountTypes: ['Percent', 'FixedAmount', 'None'],
        };
    }]);
