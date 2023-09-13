angular.module('virtoCommerce.contentModule')
    .config(['$httpProvider', function ($httpProvider) {
        $httpProvider.interceptors.push('virtoCommerce.pageBuilderModule.contentApiInterceptor');
    }])
    // This interceptor is required because API exists only in Demo Solution Features Module
    // If it's not installed, then just return null and work as earlier
    .factory('virtoCommerce.pageBuilderModule.contentApiInterceptor', ['$q', function ($q) {
        return {
            responseError: function(response) {
                if (/^api\/stores\/url\/.+/.test(response.config.url) && response.status === 404) {
                    response.status = 200;
                    response.statusText = 'OK';
                    response.data = null;
                    return $q.resolve(response);
                }

                return $q.reject(response);
            }
        };
    }])
    .factory('virtoCommerce.pageBuilderModule.contentApi', ['$q', '$resource', 'virtoCommerce.pageBuilderModule.resourceNameService', function ($q, $resource, helper) {
        return $resource('api/content/:contentType/:storeId', null, {
            savePage: {
                method: 'POST',
                headers: { 'Content-Type': undefined },
                transformRequest: function (currentEntity) {
                    var blobname = helper.prepareFilename(currentEntity);
                    var fd = new FormData();
                    fd.append(blobname, currentEntity.content);
                    return fd;
                },
                isArray: true
            },
            get: {
                // using transformResponse to:
                // 1. avoid automatic response result string converting to array;
                transformResponse: function (rawData) { return { data: rawData }; }
            }
			// ,
            // getStoreUrl: {
            //    url: 'api/stores/url/:storeId',
            //    method: 'GET',
            //    transformResponse: function(rawData) {
            //        return { data: rawData };
            //    }
            // }
    });
}]);
