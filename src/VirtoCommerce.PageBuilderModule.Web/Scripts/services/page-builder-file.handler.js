var moduleName = "virtoCommerce.pageBuilderModule";

angular.module(moduleName)
    .factory('virtoCommerce.pageBuilderModule.fileHandler', ['platformWebApp.bladeNavigationService', function (bladeNavigationService) {
        var handler = {
            edit: {
                descriptor: {
                    icon: 'list-ico fa fa-crop',
                    name: 'pageBuilder.blades.pageBuilderEditor.edit.title',
                    description: 'pageBuilder.blades.pageBuilderEditor.edit.description'
                },
                isMatch: isMatchForEdit,
                execute: editFile
            },
            create: {
                descriptor: {
                    icon: 'list-ico fa fa-crop',
                    name: 'pageBuilder.blades.pageBuilderEditor.create.title',
                    description: 'pageBuilder.blades.pageBuilderEditor.create.description'
                },
                isMatch: function () { return true; },
                execute: createFile
            }
        };

        function isMatchForEdit(file, operation) {
            return file && file.name && (file.name.endsWith('.page') || file.name.endsWith('.page-draft'));
        }

        function createFile(blade, parentBlade) {
            var newBlade = {
                id: 'designPage',
                contentType: blade.contentType,
                storeId: blade.storeId,
                storeUrl: blade.storeUrl,
                languages: blade.languages,
                folderUrl: blade.folderUrl,
                isNew: true,
                currentEntity: {},
                title: 'pageBuilder.blades.edit-page.title-new',
                subtitle: 'pageBuilder.blades.edit-page.subtitle-new',
                controller: 'virtoCommerce.pageBuilderModule.editPageController',
                template: 'Modules/$(VirtoCommerce.PageBuilderModule)/Scripts/blades/pages/edit-page.tpl.html'
            };
            bladeNavigationService.showBlade(newBlade, parentBlade);
        }

        function editFile(blade, parentBlade) {
            var newBlade = {
                id: 'designPage',
                contentType: blade.contentType,
                storeId: blade.storeId,
                storeUrl: blade.storeUrl,
                languages: blade.languages,
                folderUrl: blade.folderUrl,
                currentEntity: angular.copy(blade.currentEntity),
                isNew: false,
                title: 'pageBuilder.blades.edit-page.title-new',
                subtitle: 'pageBuilder.blades.edit-page.subtitle-new',
                controller: 'virtoCommerce.pageBuilderModule.editPageController',
                template: 'Modules/$(VirtoCommerce.PageBuilderModule)/Scripts/blades/pages/edit-page.tpl.html'
            };

            bladeNavigationService.showBlade(newBlade, parentBlade);
        }

        return handler;

    }]);

