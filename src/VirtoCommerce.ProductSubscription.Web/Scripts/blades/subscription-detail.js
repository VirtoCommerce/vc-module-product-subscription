angular.module('VirtoCommerce.ProductSubscription')
    .controller('VirtoCommerce.ProductSubscription.subscriptionDetailController', [
        '$scope', 'VirtoCommerce.ProductSubscription.webApi', 'VirtoCommerce.ProductSubscription.labels',
        'platformWebApp.bladeNavigationService', 'platformWebApp.dialogService', 'virtoCommerce.orderModule.knownOperations',
        function ($scope, api, labels, bladeNavigationService, dialogService, knownOperations) {
            var blade = $scope.blade;
            blade.headIcon = 'fas fa-sync-alt';
            blade.subtitle = 'product-subscription.blades.subscription-detail.subtitle';
            $scope.labels = labels;
            $scope.edit = {};

            blade.refresh = function () {
                blade.isLoading = true;
                api.getSubscription({ id: blade.subscriptionId }, function (subscription) {
                    blade.currentEntity = subscription;
                    blade.title = subscription.number;
                    resetEdit();
                    loadRelated(subscription);
                    blade.isLoading = false;
                }, function (error) {
                    blade.isLoading = false;
                    bladeNavigationService.setError(error, blade);
                });
            };

            function resetEdit() {
                var s = blade.currentEntity;
                $scope.edit = {
                    quantity: s.quantity,
                    interval: s.intervalUnit + ':' + s.intervalCount,
                    nextOrderDate: s.nextOrderDate ? new Date(s.nextOrderDate) : null,
                    storedPaymentMethodId: s.storedPaymentMethodId,
                };
            }

            function loadRelated(subscription) {
                $scope.intervals = [{ unit: subscription.intervalUnit, count: subscription.intervalCount }];
                if (subscription.planId) {
                    api.getPlan({ id: subscription.planId }, function (plan) {
                        $scope.plan = plan;
                        $scope.intervals = plan.intervals.slice();
                        if (!_.some($scope.intervals, function (i) { return i.unit === subscription.intervalUnit && i.count === subscription.intervalCount; })) {
                            $scope.intervals.unshift({ unit: subscription.intervalUnit, count: subscription.intervalCount });
                        }
                    });
                }

                api.savedPaymentMethods({ customerId: subscription.customerId, storeId: subscription.storeId }, function (methods) {
                    $scope.paymentMethods = methods;
                });

                api.searchRenewals({ subscriptionId: subscription.id, take: 20, sort: 'ScheduledDate:desc' }, function (data) {
                    $scope.renewals = data.results;
                });

                $scope.consent = null;
                if (subscription.consentId) {
                    api.getConsent({ id: subscription.consentId }, function (consent) { $scope.consent = consent; });
                }
            }

            $scope.intervalLabel = function (i) { return labels.interval(i.unit, i.count); };

            $scope.formatAddress = function (a) {
                if (!a) {
                    return '';
                }
                var name = [a.firstName, a.lastName].filter(Boolean).join(' ');
                var region = [a.regionName || a.regionId, a.postalCode].filter(Boolean).join(' ');
                return [name, a.line1, a.line2, a.city, region, a.countryCode].filter(Boolean).join(', ');
            };

            $scope.isDirty = function () {
                var s = blade.currentEntity;
                if (!s) { return false; }
                var e = $scope.edit;
                return e.quantity !== s.quantity
                    || e.interval !== s.intervalUnit + ':' + s.intervalCount
                    || (e.nextOrderDate && new Date(s.nextOrderDate).toDateString() !== e.nextOrderDate.toDateString())
                    || e.storedPaymentMethodId !== s.storedPaymentMethodId;
            };

            function run(action, params, body) {
                blade.isLoading = true;
                api[action](angular.extend({ id: blade.subscriptionId }, params || {}), body || {}, function () {
                    blade.refresh();
                    if (blade.parentRefresh) { blade.parentRefresh(); }
                }, function (error) {
                    blade.isLoading = false;
                    bladeNavigationService.setError(error, blade);
                });
            }

            function save() {
                var s = blade.currentEntity;
                var e = $scope.edit;
                var parts = e.interval.split(':');
                var change = {};
                if (e.quantity !== s.quantity) { change.quantity = e.quantity; }
                if (parts[0] !== s.intervalUnit || parseInt(parts[1], 10) !== s.intervalCount) {
                    change.intervalUnit = parts[0];
                    change.intervalCount = parseInt(parts[1], 10);
                }
                if (e.nextOrderDate && new Date(s.nextOrderDate).toDateString() !== e.nextOrderDate.toDateString()) {
                    change.nextOrderDate = e.nextOrderDate.toISOString();
                }
                if (e.storedPaymentMethodId !== s.storedPaymentMethodId) { change.storedPaymentMethodId = e.storedPaymentMethodId; }
                run('changeSubscription', null, change);
            }

            function confirm(id, titleKey, messageKey, callback) {
                dialogService.showConfirmationDialog({
                    id: id,
                    title: titleKey,
                    message: messageKey,
                    callback: function (confirmed) { if (confirmed) { callback(); } }
                });
            }

            $scope.openOrder = function (orderId) {
                var foundTemplate = knownOperations.getOperation('CustomerOrder');
                if (!foundTemplate || !orderId) { return; }
                var newBlade = angular.copy(foundTemplate.detailBlade);
                newBlade.id = 'productSubscriptionOrder';
                newBlade.customerOrder = { id: orderId, operationType: 'CustomerOrder' };
                bladeNavigationService.showBlade(newBlade, blade);
            };

            $scope.openRenewal = function (renewal) {
                bladeNavigationService.showBlade({
                    id: 'productSubscriptionRenewalDetail',
                    renewalId: renewal.id,
                    title: renewal.number,
                    controller: 'VirtoCommerce.ProductSubscription.renewalDetailController',
                    template: 'Modules/$(VirtoCommerce.ProductSubscription)/Scripts/blades/renewal-detail.html',
                    parentRefresh: blade.refresh,
                }, blade);
            };

            function status() { return blade.currentEntity && blade.currentEntity.status; }

            blade.toolbarCommands = [
                { name: 'platform.commands.save', icon: 'fas fa-save', executeMethod: save, canExecuteMethod: function () { return $scope.isDirty() && status() !== 'Cancelled'; }, permission: 'product-subscription:update' },
                { name: 'platform.commands.reset', icon: 'fa fa-undo', executeMethod: resetEdit, canExecuteMethod: $scope.isDirty },
                { name: 'product-subscription.commands.order-now', icon: 'fas fa-bolt', executeMethod: function () { confirm('psOrderNow', 'product-subscription.dialogs.order-now.title', 'product-subscription.dialogs.order-now.message', function () { run('orderSubscriptionNow'); }); }, canExecuteMethod: function () { return status() === 'Active'; }, permission: 'product-subscription:renewals:process' },
                { name: 'product-subscription.commands.skip', icon: 'fas fa-forward', executeMethod: function () { run('skipSubscription'); }, canExecuteMethod: function () { return status() === 'Active' || status() === 'PastDue'; }, permission: 'product-subscription:update' },
                { name: 'product-subscription.commands.pause', icon: 'fas fa-pause', executeMethod: function () { run('pauseSubscription'); }, canExecuteMethod: function () { return status() === 'Active' || status() === 'PastDue'; }, permission: 'product-subscription:update' },
                { name: 'product-subscription.commands.resume', icon: 'fas fa-play', executeMethod: function () { run('resumeSubscription'); }, canExecuteMethod: function () { return status() === 'Paused'; }, permission: 'product-subscription:update' },
                { name: 'product-subscription.commands.cancel', icon: 'fas fa-ban', executeMethod: function () { confirm('psCancel', 'product-subscription.dialogs.cancel.title', 'product-subscription.dialogs.cancel.message', function () { run('cancelSubscription', { reason: 'Cancelled by an administrator' }); }); }, canExecuteMethod: function () { return status() && status() !== 'Cancelled'; }, permission: 'product-subscription:update' },
                { name: 'platform.commands.refresh', icon: 'fa fa-refresh', executeMethod: function () { blade.refresh(); }, canExecuteMethod: function () { return true; } },
            ];

            blade.refresh();
        }]);
