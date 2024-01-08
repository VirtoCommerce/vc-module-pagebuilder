angular.module('virtoCommerce.pageBuilderModule')
    .controller('virtoCommerce.pageBuilderModule.editPageController', ['$rootScope', '$scope', "$q",
        'platformWebApp.validators', 'virtoCommerce.contentModule.contentApi',
        'virtoCommerce.pageBuilderModule.contentApi', 'platformWebApp.bladeNavigationService', 'platformWebApp.dialogService',
        'platformWebApp.dynamicProperties.dictionaryItemsApi', 'platformWebApp.settings',
        'virtoCommerce.pageBuilderModule.resourceNameService', 'virtoCommerce.searchModule.searchIndexation', "moment",
        function ($rootScope, $scope, $q, validators, contentApi, pageBuilderApi, bladeNavigationService, dialogService, dictionaryItemsApi, settings, nameHelper, searchApi, moment) {

            var momentFormat = "YYYYMMDDHHmmss";

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
                    updateToolbarCommands();
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
                        blade.hasChanges = entity.hasChanges;
                        blade.published = entity.published;
                        updateToolbarCommands();
                        fillMetadata();
                        blade.origEntity = angular.copy(blade.currentEntity);
                    }, function (error) {
                        bladeNavigationService.setError('Error ' + error.status, $scope.blade);
                        blade.isLoading = false;
                    });
                    loadSearchIndex();
                }
            };

            $scope.permalinkDuplicates = [];

            $scope.validatePermalink = function (value) {
                if (!value) {
                    $scope.permalinkDuplicates = [];
                    return $q.resolve();
                }
                return contentApi.search(
                    {
                        contentType: null,
                        storeId: blade.storeId,
                        keyword: value,
                        folderUrl: null
                    },
                    function (data) {
                        var permalinks = _.filter(data, function (x) {
                            try {
                                var permalink = JSON.parse(x.content)[0].permalink;
                                return permalink == value && x.name != blade.currentEntity.name;
                            } catch { }
                            return false;
                        });
                        $scope.permalinkDuplicates = permalinks;
                        if (permalinks.length > 0) {
                            return $q.reject();
                        }
                        return $q.resolve();
                    }, function (error) {
                        $scope.permalinkDuplicates = [];
                        return $q.resolve();
                    });
            };

            $scope.saveChanges = function () {
                var newFileName = addExtension($scope.blade.currentEntity.name);
                var originFileName = null;

                if (!blade.isNew) {
                    originFileName = blade.origEntity.name;
                }
                reloadPageAndSave(newFileName, originFileName);
            };

            function addExtension(sourceName) {
                var result = sourceName;
                if (!result.endsWith('.page')) {
                    result += '.page';
                }
                return result;
            }

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
                            // blade.isLoading = true;
                            var showPreview = function (storeUrl) {
                                storeUrl = (storeUrl || blade.storeUrl).replace(/\/$/, '');
                                if (storeUrl) {
                                    var path = generatePath();
                                    window.open(storeUrl + '/pages?path=' + path, '_blank');
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

            var publishCommand = {
                name: "pageBuilder.commands.publish", icon: 'fa fa-file',
                executeMethod: function () {
                    contentApi.publish({
                        contentType: blade.contentType,
                        storeId: blade.storeId,
                        relativeUrl: blade.currentEntity.relativeUrl
                    }, function () {
                        blade.hasChanges = false;
                        blade.published = true;
                        blade.parentBlade.refresh();
                        updateToolbarCommands();
                    });
                },
                canExecuteMethod: function () { return true; }
            };
            var unpublishCommand = {
                name: "pageBuilder.commands.unpublish", icon: 'fa fa-file-alt',
                executeMethod: function () {
                    contentApi.unpublish({
                        contentType: blade.contentType,
                        storeId: blade.storeId,
                        relativeUrl: blade.currentEntity.relativeUrl
                    }, function () {
                        blade.hasChanges = true;
                        blade.published = false;
                        blade.parentBlade.refresh();
                        updateToolbarCommands();
                    });
                },
                canExecuteMethod: function () { return true; }
            };

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

            // #region search

            function addIndexToolbarButton() {
                blade.toolbarCommands.push({
                    name: "content.commands.preview-index",
                    icon: 'fa fa-file-alt',
                    executeMethod: function () {
                        getDocumentIndex(function (data) {
                            var doc = getSearchDocumentInfo();
                            const searchBlade = {
                                id: 'sesarchDetails',
                                currentEntityId: doc.documentId,
                                currentEntity: blade.currentEntity,
                                data: $scope.index,
                                indexDate: $scope.indexDate,
                                documentType: doc.documentType,
                                controller: 'virtoCommerce.searchModule.indexDetailController',
                                template: 'Modules/$(VirtoCommerce.Search)/Scripts/blades/index-detail.tpl.html'
                            };

                            bladeNavigationService.showBlade(searchBlade, blade);
                        });
                    },
                    canExecuteMethod: function () { return true; }
                });
            }

            function loadSearchIndex() {
                if (blade.isNew) {
                    return;
                }
                contentApi.indexedSearchEnabled({}, function (data) {
                    $scope.searchEnabled = data.result;
                    getDocumentIndex(addIndexToolbarButton);
                });
            }

            function updateIndexStatus(data, doc) {
                if (_.any(data)) {
                    $scope.index = data[0];
                    $scope.indexDate = moment.utc($scope.index.indexationdate, momentFormat);
                }
            }

            function updateSearchIndex() {
                var doc = getSearchDocumentInfo();
                doc.documentIds = [doc.documentId];

                searchApi.index([doc], function (data) {
                    getDocumentIndex();
                });
            }

            function getSearchDocumentInfo() {
                var documentId = btoa(`${blade.storeId}::${blade.contentType}::${blade.currentEntity.relativeUrl}`).replaceAll('=', '-');
                var documentType = 'ContentFile';
                return { documentType: documentType, documentId: documentId };
            }

            function getDocumentIndex(callback) {
                if ($scope.searchEnabled) {
                    var doc = getSearchDocumentInfo();
                    searchApi.getDocIndex(doc, function (data) {
                        updateIndexStatus(data, doc);
                        callback && _.any(data) && callback();
                    });
                }
            }

            // #endregion

            function isDirty() {
                return !angular.equals(blade.currentEntity, blade.origEntity) && blade.hasUpdatePermission();
            }

            function canSave() {
                return isDirty() && formScope && formScope.$valid;
            }

            function generatePath() {
                // need to return path relative to the root folder
                //return blade.currentEntity.settings.permalink
                //    ? '/' + blade.currentEntity.settings.permalink
                //    : blade.currentEntity.relativeUrl;
                return blade.currentEntity.relativeUrl;
            }

            function runDesigner() {
                if (blade.designerUrl) {
                    // /Modules/$(VirtoCommerce.PageBuilderModule)/Content/builder/
                    var path = blade.currentEntity.relativeUrl.replace(/(?<=[^:])(\/\/)/, "/");
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
                    savePage(newFileName);
                }
                contentApi.get({
                    contentType: $scope.blade.contentType,
                    storeId: $scope.blade.storeId,
                    relativeUrl: $scope.blade.currentEntity.relativeUrl
                }, function (data) {
                    var page = JSON.parse(data.data);
                    angular.extend(page[0], $scope.blade.currentEntity.settings);
                    $scope.blade.currentEntity.blocks = page;
                    savePage(newFileName, originFileName);
                }, function (error) {
                    var dialog = { id: "errorDetails" };
                    dialog.message = error.message;
                    dialogService.showDialog(dialog, '$(Platform)/Scripts/app/modularity/dialogs/errorDetails-dialog.tpl.html', 'platformWebApp.confirmDialogController');
                });
            }

            function savePage(newFileName, originFileName) {
                var oldRelativeUrl = $scope.blade.currentEntity.relativeUrl;
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
                        blade.hasChanges = true; // file has draft version
                        if (newFileName !== originFileName && !!originFileName) {
                            $scope.blade.currentEntity.name = newFileName;
                            var newRelativeUrl = blade.currentEntity.relativeUrl;
                            contentApi.move({
                                contentType: blade.contentType,
                                storeId: blade.storeId,
                                oldUrl: oldRelativeUrl,
                                newUrl: newRelativeUrl
                            }, function () {
                                saveSuccess();
                            }, saveError);
                        } else {
                            saveSuccess();
                        }
                    },
                    saveError);
            }

            function saveSuccess() {
                blade.origEntity = angular.copy(blade.currentEntity);
                updateSearchIndex();
                if (blade.isNew) {
                    $scope.bladeClose();
                    $rootScope.$broadcast("cms-statistics-changed", blade.storeId);
                }
                blade.parentBlade.refresh();
                updateToolbarCommands();

                if (blade.isNew) {
                    runDesigner();
                }
            }

            function updateToolbarCommands() {
                if ($scope.blade.isNew) return;
                $scope.blade.toolbarCommands = blade.toolbarCommands.filter(x => x != publishCommand && x != unpublishCommand);
                if ($scope.blade.published && !$scope.blade.hasChanges) {
                    $scope.blade.toolbarCommands.splice(4, 0, unpublishCommand);
                } else {
                    $scope.blade.toolbarCommands.splice(4, 0, publishCommand);
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

