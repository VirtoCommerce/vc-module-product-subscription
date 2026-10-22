angular.module('VirtoCommerce.ProductSubscription')
    .controller('VirtoCommerce.ProductSubscription.planListController', [
        '$scope', 'VirtoCommerce.ProductSubscription.webApi', 'VirtoCommerce.ProductSubscription.labels',
        'platformWebApp.bladeNavigationService', 'platformWebApp.dialogService',
        function ($scope, api, labels, bladeNavigationService, dialogService) {
            var blade = $scope.blade;
            blade.title = 'product-subscription.blades.plan-list.title';
            blade.headIcon = 'fas fa-tags';
            $scope.labels = labels;

            blade.refresh = function () {
                blade.isLoading = true;
                api.searchPlans({ take: 200 }, function (data) {
                    blade.currentEntities = data.results;
                    blade.isLoading = false;
                }, function (error) {
                    blade.isLoading = false;
                    bladeNavigationService.setError(error, blade);
                });
            };

            $scope.intervalsText = function (plan) {
                return _.map(plan.intervals, function (i) { return labels.interval(i.unit, i.count); }).join(', ');
            };

            $scope.openPlan = function (plan) {
                $scope.selectedNodeId = plan ? plan.id : null;
                bladeNavigationService.showBlade({
                    id: 'productSubscriptionPlanDetail',
                    planId: plan ? plan.id : null,
                    isNew: !plan,
                    controller: 'VirtoCommerce.ProductSubscription.planDetailController',
                    template: 'Modules/$(VirtoCommerce.ProductSubscription)/Scripts/blades/plan-detail.html',
                    parentRefresh: blade.refresh,
                }, blade);
            };

            blade.toolbarCommands = [
                { name: 'platform.commands.add', icon: 'fas fa-plus', executeMethod: function () { $scope.openPlan(null); }, canExecuteMethod: function () { return true; }, permission: 'product-subscription:plans:manage' },
                { name: 'platform.commands.refresh', icon: 'fa fa-refresh', executeMethod: function () { blade.refresh(); }, canExecuteMethod: function () { return true; } },
            ];

            blade.refresh();
        }]);
