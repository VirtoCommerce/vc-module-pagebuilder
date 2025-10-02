angular.module('virtoCommerce.pageBuilderModule')
    .controller('virtoCommerce.pageBuilderModule.editPageController', ['$rootScope', '$scope', "$q",
        'platformWebApp.validators', 'virtoCommerce.contentModule.contentApi',
        'virtoCommerce.pageBuilderModule.contentApi', 'platformWebApp.bladeNavigationService', 'platformWebApp.dialogService',
        'platformWebApp.dynamicProperties.dictionaryItemsApi', 'platformWebApp.settings',
        'virtoCommerce.pageBuilderModule.resourceNameService', 'virtoCommerce.searchModule.searchIndexation', "moment",
        'virtoCommerce.contentModule.broadcastChannelFactory', 'virtoCommerce.contentModule.files-draft',
        function ($rootScope, $scope, $q, validators, contentApi, pageBuilderApi, bladeNavigationService, dialogService, dictionaryItemsApi, settings, nameHelper, searchApi, moment, broadcastChannelFactory, filesDraftService) {

            var momentFormat = "YYYYMMDDHHmmss";

            var blade = $scope.blade;
            blade.updatePermission = 'content:update';
            blade.designerUrl = null;
            $scope.blade.currentEntity.settings = { type: 'settings', permalink: '' };
            $scope.validators = validators;
            $scope.searchEnabled = false;
            var channel;

            blade.initialize = function () {
                channel = broadcastChannelFactory(blade);
                blade.designerUrl = window.location.origin +
                    (window.location.pathname === '/' ? '' : window.location.pathname) +
                    '/Modules/$(VirtoCommerce.PageBuilderModule)/Content/builder/index.html';
                if (blade.isNew) {
                    blade.isLoading = false;

                    fillMetadata();
                    $scope.blade.isDraft = true;
                    $scope.blade.currentEntity.content = [];
                    $scope.blade.currentEntity.metadata = { // todo: load from settings
                        contentType: blade.contentType,
                        parent: 'page',
                        template: 'page'
                    };
                } else {
                    contentApi.get({
                        contentType: blade.contentType,
                        storeId: blade.storeId,
                        relativeUrl: blade.currentEntity.relativeUrl
                    }, function (data) {
                        blade.isLoading = false;
                        var entity = $scope.blade.currentEntity;
                        var fileContent = parseFileContent(data.data);
                        entity.settings = fileContent.settings;
                        if (entity.settings.name && !entity.settings.displayName) {
                            entity.settings.displayName = entity.settings.name;
                        }
                        entity.blocks = fileContent.content;
                        entity.version = fileContent.version;
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

                }
                loadSearchIndex();
            };

            $scope.permalinkDuplicates = [];

            var timer = 0;
            var request = null;

            $scope.validatePermalink = function (value) {
                if (!value || !$scope.searchEnabled) {
                    $scope.permalinkDuplicates = [];
                    return $q.resolve();
                }
                clearTimeout(timer);
                timer = setTimeout(function () {
                    if (request) {
                        request.$cancelRequest();
                        request = null;
                    }
                    request = contentApi.search(
                        {
                            contentType: null,
                            storeId: blade.storeId,
                            keyword: value,
                            folderUrl: null
                        },
                        function (data) {
                            request = null;
                            var permalinks = _.filter(data, function (x) {
                                try {
                                    var content = parseFileContent(x.content);
                                    var permalink = content.settings.permalink;
                                    if (permalink && permalink.length && permalink[0] !== '/') {
                                        permalink = '/' + permalink;
                                    }
                                    return permalink == value && !compareFileNames(x.relativeUrl, blade.currentEntity.relativeUrl);
                                } catch { }
                                return false;
                            });
                            $scope.permalinkDuplicates = permalinks;
                        }, function (error) {
                            $scope.permalinkDuplicates = [];
                    });
                }, 1000);
                return $q.resolve();
            };

            function undraftUrl(url) {
                if (value && value.endsWith('-draft')) {
                    return value.slice(0, -6);
                }
                return value;
            }

            function compareFileNames(file1, file2) {
                return undraftUrl(file1) === undraftUrl(file2);
            }
            
            $scope.copyToClipboard = function (elementId) {
                var text = document.getElementById(elementId);
                text.focus();
                text.select();
                document.execCommand('copy');
            }

            $scope.saveChanges = function () {
                var newFileName = [blade.currentEntity.pageName, blade.currentEntity.language, 'page'].filter(x => x).join('.');
                var originFileName = null;

                if (!blade.isNew) {
                    originFileName = blade.origEntity.name;
                }
                if (!$scope.blade.currentEntity.settings.name) {
                    $scope.blade.currentEntity.settings.name = blade.currentEntity.pageName;
                }
                if (!$scope.blade.currentEntity.settings.permalink) {
                    $scope.blade.currentEntity.settings.permalink = '/' + blade.currentEntity.pageName;
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
                        getDocumentIndex();
                        updateToolbarCommands();
                        postMessageToPageBuilder({ source: 'platform', published: true, hasChanges: false });
                        blade.parentBlade.refresh();
                    });
                },
                canExecuteMethod: function () { return !isDirty(); }
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
                        updateToolbarCommands();
                        postMessageToPageBuilder({ source: 'platform', published: false, hasChanges: true });
                        blade.parentBlade.refresh();
                    });
                },
                canExecuteMethod: function () { return !isDirty(); }
            };

            function fillMetadata() {
                var blobName = blade.currentEntity.name || '';

                var blobNameParts = blobName.split('.');
                blobNameParts.length > 1 ? blobNameParts.pop() : ''; // ignore extension

                if (blade.languages && blade.languages.length) {
                    var possibleFileLanguage = blobNameParts.length > 1 ? blobNameParts[blobNameParts.length - 1] : '';

                    var language = blade.languages.find(function (lang) { return lang.toLowerCase() === possibleFileLanguage.toLowerCase(); });

                    if (language) {
                        blobNameParts.pop();
                        blade.currentEntity.language = language;
                    }
                }

                blade.currentEntity.pageName = blobNameParts.join('.');
                blade.origEntity = angular.copy(blade.currentEntity);
            }

            function parseFileContent(fileContent) {
                var result = JSON.parse(fileContent);
                if (Array.isArray(result)) {
                    return {
                        settings: result[0],
                        content: result.filter((x, i) => i > 0),
                        version: 1
                    };
                }
                return result;
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
                contentApi.indexedSearchEnabled({}, function (data) {
                    $scope.searchEnabled = data.result;
                    if (blade.isNew || !blade.currentEntity.published) {
                        return;
                    }
                    $scope.validatePermalink(blade.currentEntity.settings.permalink);
                    getDocumentIndex(addIndexToolbarButton);
                });
            }

            function updateIndexStatus(data, doc) {
                if (_.any(data)) {
                    $scope.index = data[0];
                    $scope.indexDate = moment.utc($scope.index.indexationdate, momentFormat);
                }
            }

            function getSearchDocumentInfo() {
                var relativeUrl = undraftUrl(blade.currentEntity.relativeUrl);
                var documentId = btoa(`${blade.storeId}::${blade.contentType}::${relativeUrl}`).replaceAll('=', '-');
                var documentType = 'ContentFile';
                return { documentType: documentType, documentId: documentId };
            }

            function undraftUrl(url) {
                if (!!url && url.endsWith('-draft')) {
                    return url.substring(0, url.length - 6);
                }
                return url;
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
                    var relativeUrl = filesDraftService.getDraftFileName(blade);
                    window.open(blade.designerUrl + '?storeId=' + blade.storeId + '#/pages?type=' + blade.contentType + '&path=' + relativeUrl, '_blank');
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
                    var page = parseFileContent(data.data);
                    $scope.blade.currentEntity.settings = Object.assign({}, page.settings, $scope.blade.currentEntity.settings);
                    $scope.blade.currentEntity.content = page.content;

                    savePage(newFileName, originFileName);
                }, function (error) {
                    savePage(newFileName, originFileName);
                });
            }

            function joinPath(path1, path2) {
                if (!path1) {
                    return '/' + path2;
                }
                if (path1.endsWith('/')) {
                    return path1 + path2;
                }
                return path1 + '/' + path2;
            }

            function savePage(newFileName, originFileName) {
                $scope.blade.currentEntity.relativeUrl = joinPath($scope.blade.parentBlade.currentEntity?.relativeUrl || '', newFileName);
                $scope.blade.currentEntity.relativeUrl = nameHelper.prepareRelativeUrl($scope.blade.currentEntity);

                var oldRelativeUrl = blade.origEntity && blade.origEntity.relativeUrl;
                var oldLanguage = $scope.blade.origEntity && $scope.blade.origEntity.language;
                var newLanguage = $scope.blade.currentEntity.language;

                //$scope.blade.currentEntity.content = JSON.stringify($scope.blade.currentEntity.blocks, null, 4);
                $scope.blade.currentEntity.name = newFileName;
                pageBuilderApi.savePage({
                    contentType: blade.contentType,
                    storeId: blade.storeId,
                    folderUrl: blade.folderUrl || ''
                },
                    $scope.blade.currentEntity,
                    function () {
                        blade.isLoading = false;
                        blade.hasChanges = true; // file has draft version
                        postMessageToPageBuilder({ source: 'platform', published: blade.published, hasChanges: true });
                        if ((newFileName !== originFileName && !!originFileName) ||
                            (!blade.isNew && oldLanguage !== newLanguage)) {
                            $scope.blade.currentEntity.language = newLanguage;
                            $scope.blade.currentEntity.name = newFileName;

                            contentApi.delete({
                                contentType: blade.contentType,
                                storeId: blade.storeId,
                                urls: [oldRelativeUrl]
                            }, function () {
                                setTimeout(blade.parentBlade.refresh, 1000);
                                saveSuccess();
                                getDocumentIndex();
                            }, saveError);
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
                updateToolbarCommands();

                if (blade.isNew) {
                    runDesigner();
                    blade.parentBlade.refresh();
                }
            }

            function updateToolbarCommands() {
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
            blade.headIcon = 'fa fa-inbox';

            blade.initialize();
                       
            channel.onmessage = function (event) {
                var contentType = event.data.contentType;
                if (contentType === blade.contentType &&
                    filesDraftService.undraftUrl(blade.currentEntity.relativeUrl) === filesDraftService.undraftUrl(event.data.relativeUrl)) {
                    blade.currentEntity.hasChanges = event.data.hasChanges;
                    blade.currentEntity.published = event.data.published;
                    blade.hasChanges = blade.currentEntity.hasChanges;
                    blade.published = blade.currentEntity.published;
                    updateToolbarCommands();
                    $scope.$apply();
                }
            };

            function postMessageToPageBuilder(msg) {
                msg.template = {
                    settings: blade.currentEntity.settings,
                    content: blade.currentEntity.blocks
                };
                channel.postMessage(msg);
            }
        }
    ]);

