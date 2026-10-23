// Call this to register your module to main application
var moduleName = 'VirtoCommerce.ProductSubscription';

if (AppDependencies !== undefined) {
    AppDependencies.push(moduleName);
}

angular.module(moduleName, [])
    .config(['$stateProvider',
        function ($stateProvider) {
            $stateProvider
                .state('workspace.ProductSubscriptionState', {
                    url: '/product-subscription',
                    templateUrl: '$(Platform)/Scripts/common/templates/home.tpl.html',
                    controller: [
                        'platformWebApp.bladeNavigationService',
                        function (bladeNavigationService) {
                            var newBlade = {
                                id: 'blade1',
                                controller: 'VirtoCommerce.ProductSubscription.helloWorldController',
                                template: 'Modules/$(VirtoCommerce.ProductSubscription)/Scripts/blades/hello-world.html',
                                isClosingDisabled: true,
                            };
                            bladeNavigationService.showBlade(newBlade);
                        }
                    ]
                });
        }
    ])
    .run(['platformWebApp.mainMenuService', '$state',
        function (mainMenuService, $state) {
            //Register module in main menu
            var menuItem = {
                path: 'browse/product-subscription',
                icon: 'fa fa-cube',
                title: 'Product Subscription',
                priority: 100,
                action: function () { $state.go('workspace.ProductSubscriptionState'); },
                permission: 'product-subscription:access',
            };
            mainMenuService.addMenuItem(menuItem);
        }
    ]);
