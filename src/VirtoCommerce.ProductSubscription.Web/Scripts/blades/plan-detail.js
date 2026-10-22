angular.module('VirtoCommerce.ProductSubscription')
    .controller('VirtoCommerce.ProductSubscription.planDetailController', [
        '$scope', '$http', 'VirtoCommerce.ProductSubscription.webApi', 'VirtoCommerce.ProductSubscription.labels',
        'platformWebApp.bladeNavigationService', 'platformWebApp.dialogService', 'virtoCommerce.storeModule.stores',
        function ($scope, $http, api, labels, bladeNavigationService, dialogService, stores) {
            var blade = $scope.blade;
            blade.headIcon = 'fas fa-tags';
            blade.subtitle = 'product-subscription.blades.plan-detail.subtitle';
            $scope.labels = labels;
            $scope.names = {};

            stores.search({ take: 100 }, function (data) { $scope.stores = data.results; });

            blade.refresh = function () {
                if (blade.isNew) {
                    blade.origEntity = {
                        name: '',
                        isActive: true,
                        priority: 0,
                        discountType: 'Percent',
                        discountValue: 10,
                        intervals: [
                            { unit: 'Week', count: 2, isDefault: false },
                            { unit: 'Week', count: 4, isDefault: true },
                            { unit: 'Month', count: 2, isDefault: false },
                        ],
                        productIds: [],
                        categoryIds: [],
                    };
                    blade.title = 'product-subscription.blades.plan-detail.new-title';
                    initialize(blade.origEntity);
                    return;
                }

                blade.isLoading = true;
                api.getPlan({ id: blade.planId }, function (plan) {
                    blade.origEntity = plan;
                    blade.title = plan.name;
                    initialize(plan);
                    blade.isLoading = false;
                }, function (error) {
                    blade.isLoading = false;
                    bladeNavigationService.setError(error, blade);
                });
            };

            function initialize(plan) {
                blade.currentEntity = angular.copy(plan);
                if (!blade.currentEntity.storeId && $scope.stores && $scope.stores.length === 1) {
                    blade.currentEntity.storeId = $scope.stores[0].id;
                }
                loadNames();
            }

            function loadNames() {
                var e = blade.currentEntity;
                if (e.productIds && e.productIds.length) {
                    $http.post('api/catalog/products/plenty', e.productIds, { params: { respGroup: 'ItemInfo' } }).then(function (response) {
                        _.each(response.data, function (p) { $scope.names[p.id] = p.name + (p.code ? ' (' + p.code + ')' : ''); });
                    });
                }
                if (e.categoryIds && e.categoryIds.length) {
                    $http.post('api/catalog/categories/plenty', e.categoryIds, { params: { respGroup: 'Info' } }).then(function (response) {
                        _.each(response.data, function (c) { $scope.names[c.id] = c.name; });
                    });
                }
            }

            $scope.$watch('stores', function () {
                if (blade.currentEntity && !blade.currentEntity.storeId && $scope.stores && $scope.stores.length === 1) {
                    blade.currentEntity.storeId = $scope.stores[0].id;
                }
            });

            $scope.addInterval = function () {
                blade.currentEntity.intervals.push({ unit: 'Month', count: 1, isDefault: blade.currentEntity.intervals.length === 0 });
            };

            $scope.removeInterval = function (interval) {
                var list = blade.currentEntity.intervals;
                list.splice(list.indexOf(interval), 1);
                if (list.length && !_.some(list, function (x) { return x.isDefault; })) {
                    list[0].isDefault = true;
                }
            };

            $scope.setDefault = function (interval) {
                _.each(blade.currentEntity.intervals, function (x) { x.isDefault = x === interval; });
            };

            $scope.removeProduct = function (id) {
                blade.currentEntity.productIds = _.without(blade.currentEntity.productIds, id);
            };

            $scope.removeCategory = function (id) {
                blade.currentEntity.categoryIds = _.without(blade.currentEntity.categoryIds, id);
            };

            $scope.selectCatalogItems = function () {
                var selection = [];
                bladeNavigationService.showBlade({
                    id: 'CatalogItemsSelect',
                    title: 'product-subscription.blades.plan-detail.select-items',
                    controller: 'virtoCommerce.catalogModule.catalogItemSelectController',
                    template: 'Modules/$(VirtoCommerce.Catalog)/Scripts/blades/common/catalog-items-select.tpl.html',
                    options: {
                        allowCheckingCategory: true,
                        selectedItemIds: [],
                        checkItemFn: function (listItem, isSelected) {
                            if (isSelected) {
                                if (!_.find(selection, function (x) { return x.id === listItem.id; })) { selection.push(listItem); }
                            } else {
                                selection = _.reject(selection, function (x) { return x.id === listItem.id; });
                            }
                        }
                    },
                    breadcrumbs: [],
                    toolbarCommands: [{
                        name: 'platform.commands.confirm', icon: 'fa fa-check',
                        executeMethod: function (selectBlade) {
                            _.each(selection, function (item) {
                                $scope.names[item.id] = item.name + (item.code ? ' (' + item.code + ')' : '');
                                if (item.type === 'category') {
                                    if (blade.currentEntity.categoryIds.indexOf(item.id) < 0) { blade.currentEntity.categoryIds.push(item.id); }
                                } else if (blade.currentEntity.productIds.indexOf(item.id) < 0) {
                                    blade.currentEntity.productIds.push(item.id);
                                }
                            });
                            bladeNavigationService.closeBlade(selectBlade);
                        },
                        canExecuteMethod: function () { return selection.length > 0; }
                    }]
                }, blade);
            };

            function isDirty() {
                return !angular.equals(blade.currentEntity, blade.origEntity) && blade.hasUpdatePermission();
            }

            function canSave() {
                var e = blade.currentEntity;
                return e && e.name && e.storeId && e.intervals.length > 0 && (blade.isNew || isDirty());
            }

            function save() {
                blade.isLoading = true;
                api.savePlan(blade.currentEntity, function (plan) {
                    blade.isNew = false;
                    blade.planId = plan.id;
                    blade.origEntity = plan;
                    blade.title = plan.name;
                    initialize(plan);
                    blade.isLoading = false;
                    if (blade.parentRefresh) { blade.parentRefresh(); }
                }, function (error) {
                    blade.isLoading = false;
                    bladeNavigationService.setError(error, blade);
                });
            }

            blade.onClose = function (closeCallback) {
                bladeNavigationService.showConfirmationIfNeeded(isDirty(), canSave(), blade, save, closeCallback,
                    'product-subscription.dialogs.plan-save.title', 'product-subscription.dialogs.plan-save.message');
            };

            blade.toolbarCommands = [
                { name: 'platform.commands.save', icon: 'fas fa-save', executeMethod: save, canExecuteMethod: canSave, permission: 'product-subscription:plans:manage' },
                { name: 'platform.commands.reset', icon: 'fa fa-undo', executeMethod: function () { initialize(blade.origEntity); }, canExecuteMethod: isDirty },
                {
                    name: 'platform.commands.delete', icon: 'fas fa-trash-alt',
                    executeMethod: function () {
                        dialogService.showConfirmationDialog({
                            id: 'psDeletePlan',
                            title: 'product-subscription.dialogs.plan-delete.title',
                            message: 'product-subscription.dialogs.plan-delete.message',
                            callback: function (confirmed) {
                                if (!confirmed) { return; }
                                api.deletePlans({ ids: [blade.planId] }, function () {
                                    if (blade.parentRefresh) { blade.parentRefresh(); }
                                    bladeNavigationService.closeBlade(blade);
                                });
                            }
                        });
                    },
                    canExecuteMethod: function () { return !blade.isNew; },
                    permission: 'product-subscription:plans:manage'
                },
            ];

            blade.hasUpdatePermission = function () { return true; };
            blade.refresh();
        }]);
