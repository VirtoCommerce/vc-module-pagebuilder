angular.module('virtoCommerce.pageBuilderModule')
    .controller('virtoCommerce.pageBuilderModule.editPageController', ['$rootScope', '$scope',
        'platformWebApp.validators', 'virtoCommerce.contentModule.contentApi',
        'virtoCommerce.pageBuilderModule.contentApi', 'platformWebApp.bladeNavigationService', 'platformWebApp.dialogService',
        'platformWebApp.dynamicProperties.dictionaryItemsApi', 'platformWebApp.settings', 'virtoCommerce.pageBuilderModule.resourceNameService',
        function ($rootScope, $scope, validators, contentApi, pageBuilderApi, bladeNavigationService, dialogService, dictionaryItemsApi, settings, nameHelper) {
            var blade = $scope.blade;
            blade.updatePermission = 'content:update';
            blade.designerUrl = null;
            $scope.validators = validators;

            blade.initialize = function () {
                blade.designerUrl = window.location.origin +
                    (window.location.pathname === '/' ? '' : window.location.pathname) +
                    '/Modules/$(VirtoCommerce.PageBuilderModule)/Content/builder/index.html';
                if (blade.isNew) {
                    blade.isLoading = false;

                    fillMetadata();

                    $scope.blade.currentEntity.blocks = [{ type: 'settings', title: '', permalink: '', layout: $scope.options[0].value, noindex: false }];
                    $scope.blade.currentEntity.settings = $scope.blade.currentEntity.blocks[0];
                    $scope.blade.currentEntity.content = JSON.stringify($scope.blade.currentEntity.blocks);
                } else {
                    contentApi.get({
                        contentType: blade.contentType,
                        storeId: blade.storeId,
                        relativeUrl: blade.currentEntity.relativeUrl
                    }, function (data) {
                        blade.isLoading = false;
                        var content = JSON.parse(data.data);
                        var entity = $scope.blade.currentEntity;
                        entity.settings = content[0];
                        entity.blocks = content;
                        entity.content = data.data;
                        fillMetadata();
                        blade.origEntity = angular.copy(blade.currentEntity);
                    }, function (error) {
                        bladeNavigationService.setError('Error ' + error.status, $scope.blade);
                        blade.isLoading = false;
                    });
                }
            };

            $scope.saveChanges = function () {
                var newFileName = $scope.blade.currentEntity.name;
                if (!newFileName.endsWith('.page')) {
                    newFileName += '.page';
                }

                var originFileName = null;

                if (!blade.isNew) {
                    originFileName = blade.origEntity.name;
                    if (!originFileName.endsWith('.page')) {
                        originFileName += '.page';
                    }
                }
                reloadPageAndSave(newFileName, originFileName);
            };

            if (!blade.isNew) {
                blade.toolbarCommands = [
                    {
                        name: "platform.commands.save", icon: 'fa fa-save',
                        executeMethod: $scope.saveChanges,
                        canExecuteMethod: canSave,
                        permission: blade.updatePermission
                    },
                    {
                        name: "platform.commands.reset", icon: 'fa fa-undo',
                        executeMethod: function () {
                            angular.copy(blade.origEntity, blade.currentEntity);
                        },
                        canExecuteMethod: isDirty,
                        permission: blade.updatePermission
                    },
                    {
                        name: "content.commands.preview-page", icon: 'fa fa-eye',
                        executeMethod: function () {
                            blade.isLoading = true;
                            var showPreview = function(storeUrl) {
                                storeUrl = (storeUrl || blade.storeUrl).replace(/\/$/, '');
                                if (storeUrl) {
                                    var path = generatePath();
                                    window.open(storeUrl + path, '_blank');
                                } else {
                                    var dialog = {
                                        id: "noUrlInStore",
                                        title: "content.dialogs.set-store-url.title",
                                        message: "content.dialogs.set-store-url.message"
                                    };
                                    dialogService.showNotificationDialog(dialog);
                                }
                            };

							// this api was removed once time
							
                            // pageBuilderApi.getStoreUrl({ storeId: blade.storeId }, function(response) {
                            //     blade.isLoading = false;
                            //     var storeUrl = response.data;
                            //     showPreview(storeUrl);
                            // }, function (error) {
                            //     bladeNavigationService.setError('Error ' + error.status, $scope.blade);
                            //     blade.isLoading = false;
                            // });
							
							// therefore open with default store url (that exists in the current blade)
							
							showPreview();
						
                        },
                        canExecuteMethod: function () { return true; }
                    },
                    {
                        name: "pageBuilder.commands.open-designer", icon: 'fa fa-crop',
                        executeMethod: function () {
                            runDesigner();
                        },
                        canExecuteMethod: function () { return true; }
                    }
                ];
            }

            blade.toolbarCommands = blade.toolbarCommands || [];

            function fillMetadata() {
                var blobName = blade.currentEntity.name || '';
                var idx = blobName.lastIndexOf('.');
                if (idx >= 0) {
                    blobName = blobName.substring(0, idx);
                    idx = blobName.lastIndexOf('.'); // language
                    if (idx >= 0) {
                        blade.currentEntity.language = blobName.substring(idx + 1);
                    }
                }
            }

            function isDirty() {
                return !angular.equals(blade.currentEntity, blade.origEntity) && blade.hasUpdatePermission();
            }

            function canSave() {
                return isDirty() && formScope && formScope.$valid;
            }

            function generatePath() {
                // need to return path relative to the root folder
                return blade.currentEntity.settings.permalink
                    ? '/' + blade.currentEntity.settings.permalink
                    : blade.currentEntity.relativeUrl;
            }

            function runDesigner() {
                if (blade.designerUrl) {
                    // /Modules/$(VirtoCommerce.PageBuilderModule)/Content/builder/
                    var path = blade.currentEntity.relativeUrl;
                    window.open(blade.designerUrl + '?path=' + path + '&storeId=' + blade.storeId + '&contentType=' + blade.contentType, '_blank');
                } else {
                    var dialog = {
                        id: "noUrlInStore",
                        title: "content.dialogs.set-designer-url.title",
                        message: "content.dialogs.set-designer-url.message"
                    };
                    dialogService.showNotificationDialog(dialog);
                }
            }

            function reloadPageAndSave(newFileName, originFileName) {
                blade.isLoading = true;
                if (blade.isNew) {
                    savePage(newFileName, null);
                    return;
                }
                contentApi.get({
                    contentType: $scope.blade.contentType,
                    storeId: $scope.blade.storeId,
                    relativeUrl: $scope.blade.currentEntity.relativeUrl
                }, function (data) {
                    var page = JSON.parse(data.data);
                    page[0] = $scope.blade.currentEntity.settings;
                    $scope.blade.currentEntity.blocks = page;
                    savePage(newFileName, originFileName);
                }, function (error) {
                    var dialog = { id: "errorDetails" };
                    dialog.message = error.message;
                    dialogService.showDialog(dialog, '$(Platform)/Scripts/app/modularity/dialogs/errorDetails-dialog.tpl.html', 'platformWebApp.confirmDialogController');
                });
            }

            function savePage(newFileName, originFileName) {
                $scope.blade.currentEntity.name = originFileName || newFileName;
                $scope.blade.currentEntity.relativeUrl = ($scope.blade.parentBlade.currentEntity.relativeUrl || '') + '/' + newFileName;
                $scope.blade.currentEntity.relativeUrl = nameHelper.prepareRelativeUrl($scope.blade.currentEntity);

                $scope.blade.currentEntity.content = JSON.stringify($scope.blade.currentEntity.blocks, null, 4);
                pageBuilderApi.savePage({
                    contentType: blade.contentType,
                    storeId: blade.storeId,
                    folderUrl: blade.folderUrl || ''
                },
                    $scope.blade.currentEntity,
                    function () {
                        blade.isLoading = false;

                        if (newFileName !== originFileName && !!originFileName) {
                            $scope.blade.currentEntity.name = newFileName;
                            var url = blade.currentEntity.url;
                            contentApi.move({
                                contentType: blade.contentType,
                                storeId: blade.storeId,
                                oldUrl: url,
                                newUrl: url.substring(0, url.length - originFileName.length) + newFileName
                            }, saveSuccess, saveError);
                        } else {
                            saveSuccess();
                        }
                    },
                    saveError);
            }

            function saveSuccess() {
                blade.origEntity = angular.copy(blade.currentEntity);
                if (blade.isNew) {
                    $scope.bladeClose();
                    $rootScope.$broadcast("cms-statistics-changed", blade.storeId);
                }

                blade.parentBlade.refresh();
                if (blade.isNew) {
                    runDesigner();
                }
            }

            function saveError(error) {
                bladeNavigationService.setError('Error ' + error.status, blade);
            }

            blade.onClose = function (closeCallback) {
                bladeNavigationService.showConfirmationIfNeeded(isDirty(), canSave(), blade, $scope.saveChanges, closeCallback, "content.dialogs.page-save.title", "content.dialogs.page-save.message");
            };

            var formScope;
            $scope.setForm = function (form) { $scope.formScope = formScope = form; };

            $scope.getDictionaryValues = function (property, callback) {
                dictionaryItemsApi.query({ id: property.objectType, propertyId: property.id }, callback);
            };

            $scope.languages = settings.getValues({ id: 'VirtoCommerce.Core.General.Languages' });
            $scope.options = [{ label: "Theme", value: "theme" }, { label: "Empty", value: "empty" }, { label: "Glossary", value: "glossary" }];

            blade.headIcon = 'fa fa-inbox';

            blade.initialize();
        }]);

