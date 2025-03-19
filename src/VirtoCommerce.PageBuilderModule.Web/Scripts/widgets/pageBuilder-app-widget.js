angular.module('virtoCommerce.pageBuilderModule')
    .controller('virtoCommerce.pageBuilderModule.pageBuilderAppWidgetController', ['$scope', function ($scope) {
        $scope.openPageBuilderApp = function () {
            var blade = $scope.blade;
            if (blade.currentEntityId) {
                var baseUrl = window.location.origin +
                    (window.location.pathname === '/' ? '' : window.location.pathname) +
                    '/apps/page-builder/';

                window.open(baseUrl + '?storeId=' + blade.currentEntityId, '_blank');

            }
        }
    }]);
