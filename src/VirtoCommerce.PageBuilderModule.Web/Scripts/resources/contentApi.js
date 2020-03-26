angular.module('virtoCommerce.contentModule')
    .factory('virtoCommerce.pageBuilderModule.contentApi', ['$resource', 'virtoCommerce.pageBuilderModule.resourceNameService', function ($resource, helper) {
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
        }
    });
}]);