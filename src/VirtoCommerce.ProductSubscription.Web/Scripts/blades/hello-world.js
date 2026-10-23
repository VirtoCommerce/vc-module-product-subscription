angular.module('VirtoCommerce.ProductSubscription')
    .controller('VirtoCommerce.ProductSubscription.helloWorldController', ['$scope', 'VirtoCommerce.ProductSubscription.webApi', function ($scope, api) {
        var blade = $scope.blade;
        blade.title = 'Product Subscription';

        blade.refresh = function () {
            api.get(function (data) {
                blade.title = 'product-subscription.blades.hello-world.title';
                blade.data = data.result;
                blade.isLoading = false;
            });
        };

        blade.refresh();
    }]);
