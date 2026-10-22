angular.module('VirtoCommerce.ProductSubscription')
    .controller('VirtoCommerce.ProductSubscription.renewalDetailController', [
        '$scope', 'VirtoCommerce.ProductSubscription.webApi', 'VirtoCommerce.ProductSubscription.labels',
        'platformWebApp.bladeNavigationService', 'platformWebApp.dialogService', 'virtoCommerce.orderModule.knownOperations',
        function ($scope, api, labels, bladeNavigationService, dialogService, knownOperations) {
            var blade = $scope.blade;
            blade.headIcon = 'fas fa-truck';
            blade.subtitle = 'product-subscription.blades.renewal-detail.subtitle';
            $scope.labels = labels;

            var beforeOrder = ['Scheduled', 'Notified', 'ReviewRequired'];

            blade.refresh = function () {
                blade.isLoading = true;
                api.getRenewal({ id: blade.renewalId }, function (renewal) {
                    blade.currentEntity = renewal;
                    blade.title = renewal.number;
                    $scope.events = _.sortBy(renewal.events || [], function (e) { return e.eventDate; }).reverse();
                    blade.isLoading = false;
                }, function (error) {
                    blade.isLoading = false;
                    bladeNavigationService.setError(error, blade);
                });
            };

            function run(action, params) {
                blade.isLoading = true;
                api[action](angular.extend({ id: blade.renewalId }, params || {}), {}, function () {
                    blade.refresh();
                    if (blade.parentRefresh) { blade.parentRefresh(); }
                }, function (error) {
                    blade.isLoading = false;
                    bladeNavigationService.setError(error, blade);
                });
            }

            $scope.openOrder = function () {
                var r = blade.currentEntity;
                var foundTemplate = knownOperations.getOperation('CustomerOrder');
                if (!foundTemplate || !r.orderId) { return; }
                var newBlade = angular.copy(foundTemplate.detailBlade);
                newBlade.id = 'productSubscriptionRenewalOrder';
                newBlade.customerOrder = { id: r.orderId, operationType: 'CustomerOrder' };
                bladeNavigationService.showBlade(newBlade, blade);
            };

            $scope.openSubscription = function (subscriptionId) {
                bladeNavigationService.showBlade({
                    id: 'productSubscriptionDetailFromRenewal',
                    subscriptionId: subscriptionId,
                    controller: 'VirtoCommerce.ProductSubscription.subscriptionDetailController',
                    template: 'Modules/$(VirtoCommerce.ProductSubscription)/Scripts/blades/subscription-detail.html',
                }, blade);
            };

            function state() { return blade.currentEntity && blade.currentEntity.state; }

            blade.toolbarCommands = [
                {
                    name: 'product-subscription.commands.process-now', icon: 'fas fa-forward',
                    executeMethod: function () {
                        dialogService.showConfirmationDialog({
                            id: 'psProcessNow',
                            title: 'product-subscription.dialogs.process-now.title',
                            message: 'product-subscription.dialogs.process-now.message',
                            callback: function (confirmed) { if (confirmed) { run('processRenewal'); } }
                        });
                    },
                    canExecuteMethod: function () { return state() && ['Completed', 'Failed', 'Skipped', 'Cancelled'].indexOf(state()) < 0; },
                    permission: 'product-subscription:renewals:process'
                },
                { name: 'product-subscription.commands.accept-review', icon: 'fas fa-check', executeMethod: function () { run('acceptRenewal'); }, canExecuteMethod: function () { return state() === 'ReviewRequired'; }, permission: 'product-subscription:renewals:process' },
                { name: 'product-subscription.commands.retry-payment', icon: 'fas fa-redo', executeMethod: function () { run('retryRenewalPayment'); }, canExecuteMethod: function () { return ['Failed', 'RetryScheduled', 'PaymentActionRequired'].indexOf(state()) >= 0; }, permission: 'product-subscription:renewals:process' },
                { name: 'product-subscription.commands.skip', icon: 'fas fa-step-forward', executeMethod: function () { run('skipRenewal'); }, canExecuteMethod: function () { return beforeOrder.indexOf(state()) >= 0; }, permission: 'product-subscription:renewals:process' },
                { name: 'platform.commands.cancel', icon: 'fas fa-ban', executeMethod: function () { run('cancelRenewal', { reason: 'Cancelled by an administrator' }); }, canExecuteMethod: function () { return beforeOrder.indexOf(state()) >= 0; }, permission: 'product-subscription:renewals:process' },
                { name: 'product-subscription.commands.open-order', icon: 'fas fa-file-alt', executeMethod: $scope.openOrder, canExecuteMethod: function () { return blade.currentEntity && blade.currentEntity.orderId; } },
                { name: 'platform.commands.refresh', icon: 'fa fa-refresh', executeMethod: function () { blade.refresh(); }, canExecuteMethod: function () { return true; } },
            ];

            blade.refresh();
        }]);
