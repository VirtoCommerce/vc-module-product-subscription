angular.module('VirtoCommerce.ProductSubscription')
    .controller('VirtoCommerce.ProductSubscription.renewalListController', [
        '$scope', 'VirtoCommerce.ProductSubscription.webApi', 'VirtoCommerce.ProductSubscription.labels',
        'platformWebApp.bladeNavigationService', 'platformWebApp.uiGridHelper', 'platformWebApp.bladeUtils',
        function ($scope, api, labels, bladeNavigationService, uiGridHelper, bladeUtils) {
            var blade = $scope.blade;
            blade.title = 'product-subscription.blades.renewal-list.title';
            blade.headIcon = 'fas fa-truck';
            $scope.uiGridConstants = uiGridHelper.uiGridConstants;
            $scope.labels = labels;

            var filter = blade.filter = $scope.filter = { state: null, keyword: null, onlyOpen: false };

            blade.refresh = function () {
                blade.isLoading = true;
                api.searchRenewals({
                    keyword: filter.keyword,
                    states: filter.state ? [filter.state] : null,
                    onlyOpen: filter.onlyOpen || null,
                    sort: uiGridHelper.getSortExpression($scope),
                    skip: ($scope.pageSettings.currentPage - 1) * $scope.pageSettings.itemsPerPageCount,
                    take: $scope.pageSettings.itemsPerPageCount,
                }, function (data) {
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
                    id: 'productSubscriptionRenewalDetail',
                    renewalId: entity.id,
                    title: entity.number,
                    controller: 'VirtoCommerce.ProductSubscription.renewalDetailController',
                    template: 'Modules/$(VirtoCommerce.ProductSubscription)/Scripts/blades/renewal-detail.html',
                    parentRefresh: blade.refresh,
                }, blade);
            };

            blade.toolbarCommands = [
                { name: 'platform.commands.refresh', icon: 'fa fa-refresh', executeMethod: function () { blade.refresh(); }, canExecuteMethod: function () { return true; } },
            ];

            $scope.setGridOptions = function (gridOptions) {
                uiGridHelper.initialize($scope, gridOptions, function () {
                    uiGridHelper.bindRefreshOnSortChanged($scope);
                });
                bladeUtils.initializePagination($scope);
            };
        }]);
