angular.module('virtoCommerce.pageBuilderModule')
    .factory('virtoCommerce.pageBuilderModule.resourceNameService', [function () {

        function insertLang(sourceName, lang, defaultExtension) {
            var filename = sourceName;
            var fileExtension = defaultExtension;
            var idx = filename.lastIndexOf('.');
            if (idx >= 0) {
                fileExtension = filename.substring(idx);
                filename = filename.substring(0, idx);
                idx = filename.lastIndexOf('.'); // language
                if (idx >= 0) {
                    filename = filename.substring(0, idx); // cut language from name
                }
            }

            if (lang) {
                filename += '.' + lang;
            }
            var result = filename + fileExtension;
            return result;
        }

        return {
            prepareFilename: function (entity) {
                var result = insertLang(entity.name, entity.language, '.page');
                return result;
            },
            prepareRelativeUrl: function (entity) {
                var result = insertLang(entity.relativeUrl, entity.language, '.page');
                return result;
            }
        };
    }]);