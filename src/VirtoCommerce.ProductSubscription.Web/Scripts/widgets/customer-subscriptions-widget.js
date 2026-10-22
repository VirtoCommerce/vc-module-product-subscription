angular.module('VirtoCommerce.ProductSubscription')
    .controller('VirtoCommerce.ProductSubscription.customerSubscriptionsWidgetController', [
        '$scope', 'VirtoCommerce.ProductSubscription.webApi', 'platformWebApp.bladeNavigationService',
        function ($scope, api, bladeNavigationService) {
            var blade = $scope.blade;
            $scope.count = '...';

            function customerUserIds() {
                var contact = blade.currentEntity || {};
                var ids = _.pluck(contact.securityAccounts || [], 'id');
                if (contact.id) { ids.push(contact.id); }
                return ids;
            }

            function refresh() {
                var ids = customerUserIds();
                if (!ids.length) { $scope.count = 0; return; }
                api.searchSubscriptions({ customerId: ids[0], take: 0 }, function (data) { $scope.count = data.totalCount; });
            }

            $scope.openBlade = function () {
                var ids = customerUserIds();
                bladeNavigationService.showBlade({
                    id: 'customerProductSubscriptions',
                    customerId: ids[0],
                    title: 'product-subscription.widgets.customer-subscriptions.blade-title',
                    controller: 'VirtoCommerce.ProductSubscription.subscriptionListController',
                    template: 'Modules/$(VirtoCommerce.ProductSubscription)/Scripts/blades/subscription-list.html',
                }, blade);
            };

            $scope.$watch('blade.currentEntity', function (entity) { if (entity) { refresh(); } });
        }]);
