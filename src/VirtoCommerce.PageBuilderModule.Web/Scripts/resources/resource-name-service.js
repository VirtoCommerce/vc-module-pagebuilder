angular.module('virtoCommerce.pageBuilderModule')
    .factory('virtoCommerce.pageBuilderModule.resourceNameService', [function () {

        function insertLang(sourceName, lang, isDraft) {
            var filename = sourceName;
            var fileExtension = isDraft ? '.page-draft' : '.page';
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
                var result = insertLang(entity.name, entity.language, entity.isDraft);
                return result;
            },
            prepareRelativeUrl: function (entity) {
                var result = insertLang(entity.relativeUrl, entity.language, entity.isDraft);
                return result;
            }
        };
    }]);