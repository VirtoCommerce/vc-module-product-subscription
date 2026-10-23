angular.module('VirtoCommerce.ProductSubscription')
    .controller('VirtoCommerce.ProductSubscription.subscriptionListController', [
        '$scope', 'VirtoCommerce.ProductSubscription.webApi', 'VirtoCommerce.ProductSubscription.labels',
        'platformWebApp.bladeNavigationService', 'platformWebApp.uiGridHelper', 'platformWebApp.bladeUtils', 'platformWebApp.dialogService',
        function ($scope, api, labels, bladeNavigationService, uiGridHelper, bladeUtils, dialogService) {
            var blade = $scope.blade;
            blade.title = blade.title || 'product-subscription.blades.subscription-list.title';
            blade.headIcon = 'fas fa-sync-alt';
            $scope.uiGridConstants = uiGridHelper.uiGridConstants;
            $scope.labels = labels;

            var filter = blade.filter = $scope.filter = blade.filter || { status: null, keyword: null };

            blade.refresh = function () {
                blade.isLoading = true;
                var criteria = {
                    keyword: filter.keyword,
                    statuses: filter.status ? [filter.status] : null,
                    customerId: blade.customerId,
                    sort: uiGridHelper.getSortExpression($scope),
                    skip: ($scope.pageSettings.currentPage - 1) * $scope.pageSettings.itemsPerPageCount,
                    take: $scope.pageSettings.itemsPerPageCount,
                };

                api.searchSubscriptions(criteria, function (data) {
                    $scope.pageSettings.totalItems = data.totalCount;
                    blade.currentEntities = data.results;
                    blade.isLoading = false;
                }, function (error) {
                    blade.isLoading = false;
                    bladeNavigationService.setError(error, blade);
                });
            };

            filter.criteriaChanged = function () {
                if ($scope.pageSettings.currentPage > 1) {
                    $scope.pageSettings.currentPage = 1;
                } else {
                    blade.refresh();
                }
            };

            $scope.openDetailsBlade = function (entity) {
                $scope.selectedNodeId = entity.id;
                bladeNavigationService.showBlade({
                    id: 'productSubscriptionDetail',
                    subscriptionId: entity.id,
                    title: entity.number,
                    controller: 'VirtoCommerce.ProductSubscription.subscriptionDetailController',
                    template: 'Modules/$(VirtoCommerce.ProductSubscription)/Scripts/blades/subscription-detail.html',
                    parentRefresh: blade.refresh,
                }, blade);
            };

            function openRenewals() {
                bladeNavigationService.showBlade({
                    id: 'productSubscriptionRenewals',
                    controller: 'VirtoCommerce.ProductSubscription.renewalListController',
                    template: 'Modules/$(VirtoCommerce.ProductSubscription)/Scripts/blades/renewal-list.html',
                    isExpandable: true,
                }, blade);
            }

            function openPlans() {
                bladeNavigationService.showBlade({
                    id: 'productSubscriptionPlans',
                    controller: 'VirtoCommerce.ProductSubscription.planListController',
                    template: 'Modules/$(VirtoCommerce.ProductSubscription)/Scripts/blades/plan-list.html',
                    isExpandable: true,
                }, blade);
            }

            blade.toolbarCommands = [
                { name: 'platform.commands.refresh', icon: 'fa fa-refresh', executeMethod: function () { blade.refresh(); }, canExecuteMethod: function () { return true; } },
                { name: 'product-subscription.commands.renewals', icon: 'fas fa-truck', executeMethod: openRenewals, canExecuteMethod: function () { return true; }, permission: 'product-subscription:read' },
                { name: 'product-subscription.commands.plans', icon: 'fas fa-tags', executeMethod: openPlans, canExecuteMethod: function () { return true; }, permission: 'product-subscription:plans:manage' },
                {
                    name: 'product-subscription.commands.run-scheduler', icon: 'fas fa-play',
                    executeMethod: function () {
                        blade.isLoading = true;
                        api.runScheduler({}, function (result) {
                            blade.isLoading = false;
                            dialogService.showNotificationDialog({ id: 'schedulerRun', title: 'product-subscription.dialogs.scheduler.title', message: 'Processed ' + result.processed + ' renewal(s).' });
                            blade.refresh();
                        }, function (error) { blade.isLoading = false; bladeNavigationService.setError(error, blade); });
                    },
                    canExecuteMethod: function () { return true; },
                    permission: 'product-subscription:renewals:process'
                },
            ];

            $scope.setGridOptions = function (gridOptions) {
                uiGridHelper.initialize($scope, gridOptions, function () {
                    uiGridHelper.bindRefreshOnSortChanged($scope);
                });
                bladeUtils.initializePagination($scope);
            };
        }]);
