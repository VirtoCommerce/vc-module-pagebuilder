var moduleName = "virtoCommerce.pageBuilderModule";

angular.module(moduleName)
    .factory('virtoCommerce.pageBuilderModule.fileHandler', [
        'platformWebApp.bladeNavigationService',
        'virtoCommerce.pageBuilderModule.contentApi',
        function (bladeNavigationService, pageBuilderApi) {
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
            },
            duplicate: {
                descriptor: {
                    icon: 'list-ico fa fa-copy',
                    name: 'pageBuilder.blades.pageBuilderEditor.duplicate.title',
                    description: 'pageBuilder.blades.pageBuilderEditor.duplicate.description'
                },
                isMatch: isMatchForEdit,
                execute: duplicateFile
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

        // Duplicating a page kept in a content repository is a commit under a new name on the
        // copying editor's work branch. The blob copy the content module would otherwise do writes
        // a "-draft" file instead — the blob flow's way of saying "not published yet" — and on this
        // flow that is a file no storefront serves, no publish picks up and no deploy removes.
        //
        // A store still on blob storage gets 404 from the endpoint, and the caller's own copy takes
        // over: correct for that store, and correct too where this module is newer than the server.
        function duplicateFile(context, done, notApplicable) {
            pageBuilderApi.gitCopy({
                storeId: context.blade.storeId,
                type: context.blade.contentType,
                srcPath: context.file.relativeUrl
            }, {}, done, function (error) {
                if (error.status === 404) {
                    notApplicable();
                    return;
                }
                var message = error.data && error.data.error ? error.data.error : 'Error ' + error.status;
                bladeNavigationService.setError(message, context.blade);
            });
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

