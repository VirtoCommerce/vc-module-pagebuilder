angular.module('virtoCommerce.contentModule')
    .factory('virtoCommerce.pageBuilderModule.contentApi', ['$q', '$resource', 'virtoCommerce.pageBuilderModule.resourceNameService', function ($q, $resource, helper) {
        return $resource('api/content/:contentType/:storeId', null, {
            savePage: {
                method: 'POST',
                params: { draft: true },
                headers: { 'Content-Type': undefined },
                transformRequest: function (currentEntity) {
                    var blobname = currentEntity.name;
                    var fd = new FormData();
                    var permalink = currentEntity.settings.permalink;
                    if (permalink && permalink.length && permalink[0] !== '/') {
                        currentEntity.settings.permalink = '/' + permalink;
                    }
                    var content = { settings: currentEntity.settings, content: currentEntity.content };
                    if (currentEntity.version === 1) {
                        content = [content.settings].concat(content.content);
                    }
                    content = JSON.stringify(content, null, 4);
                    fd.append(blobname, content);
                    return fd;
                },
                isArray: true
            },
            get: {
                params: { draft: true },
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
