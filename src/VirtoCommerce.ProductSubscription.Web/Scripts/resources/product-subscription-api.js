angular.module('VirtoCommerce.ProductSubscription')
    .factory('VirtoCommerce.ProductSubscription.webApi', ['$resource', function ($resource) {
        return $resource('api/product-subscription');
    }]);
