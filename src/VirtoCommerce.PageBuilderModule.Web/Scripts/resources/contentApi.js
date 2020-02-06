angular.module('virtoCommerce.contentModule')
.factory('virtoCommerce.pageBuilderModule.contentApi', ['$resource', function ($resource) {
    return $resource('api/content/:contentType/:storeId', null, {
        savePage: {
            method: 'POST',
            headers: { 'Content-Type': undefined },
            transformRequest: function (currentEntity) {
                var blobName = currentEntity.name;
                var blobNameExtension = '.page';
                var idx = blobName.lastIndexOf('.');
                if (idx >= 0) {
                    blobNameExtension = blobName.substring(idx);
                    blobName = blobName.substring(0, idx);
                    idx = blobName.lastIndexOf('.'); // language
                    if (idx >= 0) {
                        blobName = blobName.substring(0, idx); // cut language from name
                    }
                }

                if (currentEntity.language) {
                    blobName += '.' + currentEntity.language;
                }

                var fd = new FormData();
                fd.append(blobName + blobNameExtension, currentEntity.content);
                return fd;
            },
            isArray: true
        }
    });
}]);