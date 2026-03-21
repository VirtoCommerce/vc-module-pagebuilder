angular.module('virtoCommerce.pageBuilderModule')
    .controller('virtoCommerce.pageBuilderModule.pageBuilderAppWidgetController', ['$scope', function ($scope) {
        $scope.openPageBuilderApp = function () {
            var blade = $scope.blade;
            if (blade.currentEntityId) {
                var pathPrefix = window.location.pathname === '/' ? '' : window.location.pathname;
                var baseUrl = `${window.location.origin}${pathPrefix}/apps/page-builder-shell/`;

                window.open(`${baseUrl}?storeId=${blade.currentEntityId}`, '_blank');

            }
        }
    }]);
