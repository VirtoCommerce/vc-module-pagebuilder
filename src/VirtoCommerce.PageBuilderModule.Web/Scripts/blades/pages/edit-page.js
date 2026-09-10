angular.module('virtoCommerce.pageBuilderModule')
    .controller('virtoCommerce.pageBuilderModule.editPageController', ['$rootScope', '$scope', "$q",
        'platformWebApp.validators', 'virtoCommerce.contentModule.contentApi',
        'virtoCommerce.pageBuilderModule.contentApi', 'platformWebApp.bladeNavigationService', 'platformWebApp.dialogService',
        'platformWebApp.dynamicProperties.dictionaryItemsApi', 'platformWebApp.settings',
        'virtoCommerce.pageBuilderModule.resourceNameService', 'virtoCommerce.searchModule.searchIndexation', "moment",
        'virtoCommerce.contentModule.broadcastChannelFactory', 'virtoCommerce.contentModule.files-draft',
        function ($rootScope, $scope, $q, validators, contentApi, pageBuilderApi, bladeNavigationService, dialogService,
            dictionaryItemsApi, settings, nameHelper, searchApi, moment, broadcastChannelFactory, filesDraftService) {

            var momentFormat = "YYYYMMDDHHmmss";

            var formScope;
            $scope.setForm = function (form) {
                $scope.formScope = formScope = form;
            };

            var blade = $scope.blade;
            blade.updatePermission = 'content:update';
            blade.designerUrl = null;
            $scope.blade.currentEntity.settings = { type: 'settings', permalink: '' };
            $scope.validators = validators;
            $scope.searchEnabled = false;
            var channel;
            // Whether the server has told us which flow this store is on. A save goes to a different
            // place under each, so it must never run on a guess.
            var flowResolved = false;

            blade.initialize = function () {
                channel = broadcastChannelFactory(blade);
                var pathname = window.location.pathname === '/' ? '' : window.location.pathname;
                blade.designerUrl = `${window.location.origin + pathname}/Modules/$(VirtoCommerce.PageBuilderModule)/Content/page-builder-designer/index.html`;
                loadPublishStatus();
                if (blade.isNew) {
                    blade.isLoading = false;

                    fillMetadata();
                    $scope.blade.isDraft = true;
                    $scope.blade.currentEntity.content = [];
                    $scope.blade.currentEntity.metadata = { // debt: load from settings
                        contentType: blade.contentType,
                        parent: 'page',
                        template: 'page'
                    };
                } else {
                    // Read through the page builder, not blob storage: on the git flow the page lives in
                    // the content repository, and editing a stale blob copy here would save it back over
                    // the draft the designer committed.
                    pageBuilderApi.getPage({
                        storeId: blade.storeId,
                        type: blade.contentType,
                        path: blade.currentEntity.relativeUrl
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
                        updateToolbarCommands();
                        fillMetadata();
                        blade.origEntity = angular.copy(blade.currentEntity);
                    }, function (error) {
                        bladeNavigationService.setError('Error ' + error.status, $scope.blade);
                        blade.isLoading = false;
                    });

                }
                loadSearchIndex();
                loadLegacyDraft();
            };

            // Which flow this store is on, and the page's real publish state. The list one blade back
            // derives both from blob file names, which says nothing about a page whose drafts are
            // branches — so ask the server rather than trusting what we were handed.
            function loadPublishStatus(callback) {
                pageBuilderApi.publishStatus({
                    storeId: blade.storeId,
                    type: blade.contentType,
                    // a page being created has no path yet; the answer is then just the flow
                    path: blade.isNew ? '' : blade.currentEntity.relativeUrl
                }, function (status) {
                    flowResolved = true;
                    blade.gitFlow = status.flow === 'git';
                    if (!blade.isNew) {
                        blade.published = status.published;
                        blade.hasChanges = status.hasChanges;
                        blade.pending = status.pending;
                        // The open pull request is not going to merge itself — see inFlight below.
                        blade.awaitingMerge = status.awaitingMerge;
                        // Null when the installation has no production branch at all — which is how
                        // the toolbar knows whether promotion exists here as an action.
                        blade.production = status.production;
                        // a page that does not exist yet has nothing to publish, and no toolbar to update
                        updateToolbarCommands();
                    }
                    callback && callback(true);
                }, function (error) {
                    // Unknown state: leave the toolbar as it is rather than inventing one, and leave the
                    // flow unresolved. Treating an unanswered question as "blob" is how settings for a
                    // page the server reads from the repository end up written to blob storage, where the
                    // next deploy overwrites them — so the answer is asked for again rather than assumed.
                    bladeNavigationService.setError('Error ' + error.status, $scope.blade);
                    callback && callback(false);
                });
            }

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
                                    return permalink === value && !compareFileNames(x.relativeUrl, blade.currentEntity.relativeUrl);
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
                var pageSettings = $scope.blade.currentEntity.settings;
                if (!pageSettings.name) {
                    pageSettings.name = blade.currentEntity.pageName;
                }
                if (!pageSettings.permalink) {
                    pageSettings.permalink = '/' + blade.currentEntity.pageName;
                }
                // VCST-5274: the baked settings.name/displayName are not updated when the File name
                // is changed. When they still match the previous File name (i.e. they were auto-filled,
                // not customized), keep them in sync with the new File name so the stored document —
                // and everything derived from it (search index, SEO, storefront) — does not go stale.
                var oldPageName = blade.origEntity && blade.origEntity.pageName;
                if (oldPageName && oldPageName !== blade.currentEntity.pageName) {
                    if (pageSettings.name === oldPageName) {
                        pageSettings.name = blade.currentEntity.pageName;
                    }
                    if (pageSettings.displayName === oldPageName) {
                        pageSettings.displayName = blade.currentEntity.pageName;
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
                            if (blade.gitFlow) {
                                // The repository is the source of truth: preview the exact commit at the
                                // head of this editor's branch, else what is published. The blob draft
                                // this url would otherwise point at is not written on this flow.
                                window.open(gitUrl('git/preview'), '_blank');
                                return;
                            }
                            var showPreview = function (storeUrl) {
                                storeUrl = (storeUrl || blade.storeUrl).replace(/\/$/, '');
                                if (storeUrl) {
                                    var documentId = filesDraftService.getDocumentId(blade, true);
                                    // Pass the page language so the storefront preview renders in that language
                                    // instead of the store default (VCST-5219).
                                    var language = blade.currentEntity && blade.currentEntity.language;
                                    var previewUrl = `${storeUrl}/designer-preview?pageId=${encodeURIComponent(documentId)}`;
                                    if (language) {
                                        previewUrl += `&cultureName=${encodeURIComponent(language)}`;
                                    }
                                    window.open(previewUrl, '_blank');
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
                    if (blade.gitFlow) {
                        gitPublish();
                        return;
                    }
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
                // a pull request that is merging itself is already publishing this page — pressing
                // publish again would achieve nothing
                canExecuteMethod: function () { return !isDirty() && !inFlight(blade); }
            };

            // "A pull request is open" is not the same as "the page is on its way": where the content
            // repository does not allow auto-merge, a merge blocked by a check stays blocked, and the
            // page ships only when somebody asks again. Commands rest for the first case and stay
            // available for the second, which is the whole difference between Pending and AwaitingMerge.
            function inFlight(status) {
                return !!status && !!status.pending && !status.awaitingMerge;
            }

            // Publishing on the git flow merges this editor's work branch into the production branch;
            // unpublishing merges a branch whose commit deletes the page. Both are the same act of
            // shipping a commit, hence one function. That can land straight away or wait on the CI checks
            // of a pull request, so the state comes from the server afterwards: telling the editor
            // "published" when it is still pending would send them away believing the page is live.
            function gitShip(operation, pendingDialog) {
                blade.isLoading = true;
                operation({
                    storeId: blade.storeId,
                    type: blade.contentType,
                    path: blade.currentEntity.relativeUrl
                }, {}, function (result) {
                    blade.isLoading = false;
                    if (result.state === 'Pending') {
                        dialogService.showNotificationDialog({
                            id: pendingDialog.id,
                            title: pendingDialog.title,
                            message: pendingDialog.message,
                            messageValues: { url: result.url }
                        });
                    } else if (result.state === 'AwaitingMerge') {
                        // Not "in progress": nothing is going to merge this pull request, so say what
                        // finishes it instead of leaving the editor waiting for something to happen.
                        dialogService.showNotificationDialog({
                            id: "gitAwaitingMerge",
                            title: "pageBuilder.dialogs.git-awaiting-merge.title",
                            message: "pageBuilder.dialogs.git-awaiting-merge.message",
                            messageValues: { url: result.url }
                        });
                    }
                    loadPublishStatus(function () {
                        getDocumentIndex();
                        postMessageToPageBuilder({ source: 'platform', published: blade.published, hasChanges: blade.hasChanges });
                        blade.parentBlade.refresh();
                    });
                }, function (error) {
                    blade.isLoading = false;
                    // 409 means the page changed in production while this draft was being written; the
                    // server explains what to do, so show that rather than a bare status code.
                    var message = error.data && error.data.error ? error.data.error : 'Error ' + error.status;
                    bladeNavigationService.setError(message, blade);
                });
            }

            function gitPublish() {
                gitShip(pageBuilderApi.gitPublish, {
                    id: "gitPublishPending",
                    title: "pageBuilder.dialogs.git-publish-pending.title",
                    message: "pageBuilder.dialogs.git-publish-pending.message"
                });
            }

            function gitUnpublish() {
                gitShip(pageBuilderApi.gitUnpublish, {
                    id: "gitUnpublishPending",
                    title: "pageBuilder.dialogs.git-unpublish-pending.title",
                    message: "pageBuilder.dialogs.git-unpublish-pending.message"
                });
            }

            // Promotion ships the same way publishing does — a commit, then a merge — so it goes
            // through gitShip and inherits its handling of a merge that waits on CI checks.
            var promoteCommand = {
                name: "pageBuilder.commands.promote", icon: 'fa fa-rocket',
                executeMethod: function () {
                    gitShip(pageBuilderApi.gitPromote, {
                        id: "gitPromotePending",
                        title: "pageBuilder.dialogs.git-promote-pending.title",
                        message: "pageBuilder.dialogs.git-promote-pending.message"
                    });
                },
                // Production follows what has already been through the base branch: there is
                // nothing to promote until the page is published there, nothing to promote when
                // production already matches, and nothing to add while a promotion is open.
                canExecuteMethod: function () {
                    return !isDirty() && blade.published && !blade.hasChanges && !!blade.production &&
                        blade.production.behind && !inFlight(blade.production);
                }
            };

            // The draft file this page carried over from the flow before git, if it still has one.
            // Asked separately from publish status: that answers questions about the repository,
            // this one is about a leftover in blob storage that nothing else will ever clear.
            function loadLegacyDraft() {
                blade.legacyDraft = null;
                if (blade.isNew) {
                    return;
                }
                pageBuilderApi.legacyDraft({
                    storeId: blade.storeId,
                    type: blade.contentType,
                    path: blade.currentEntity.relativeUrl
                }, function (info) {
                    blade.legacyDraft = info && info.exists ? info : null;
                    updateToolbarCommands();
                }, function () {
                    // The store is not on the git flow, or the question could not be answered.
                    // Either way there is no cleanup to offer, and an error banner here would be
                    // about a file the editor never asked after.
                    blade.legacyDraft = null;
                    updateToolbarCommands();
                });
            }

            var deleteLegacyDraftCommand = {
                name: "pageBuilder.commands.delete-legacy-draft", icon: 'fa fa-eraser',
                executeMethod: function () {
                    var draft = blade.legacyDraft;
                    // One file, and it has no second copy anywhere. Name it in the prompt, and say
                    // plainly when it still holds something the repository has not got.
                    var dialogKey = draft.differsFromCurrent ? 'delete-legacy-draft-unsaved' : 'delete-legacy-draft';
                    dialogService.showConfirmationDialog({
                        id: "confirmDeleteLegacyDraft",
                        title: `pageBuilder.dialogs.${dialogKey}.title`,
                        message: `pageBuilder.dialogs.${dialogKey}.message`,
                        messageValues: { path: draft.blobPath },
                        callback: function (confirmed) {
                            if (confirmed) {
                                deleteLegacyDraft(draft.blobPath);
                            }
                        }
                    });
                },
                canExecuteMethod: function () { return !!blade.legacyDraft; }
            };

            function deleteLegacyDraft(path) {
                blade.isLoading = true;
                pageBuilderApi.deleteLegacyDraft({
                    storeId: blade.storeId,
                    type: blade.contentType
                }, { paths: [path], dryRun: false }, function (result) {
                    blade.isLoading = false;
                    // The server keeps a draft whose page is not in the repository — it is still
                    // what the builder reads. Say so, rather than report a deletion that did not
                    // happen.
                    if (result.skipped && result.skipped.length) {
                        dialogService.showNotificationDialog({
                            id: "legacyDraftKept",
                            title: "pageBuilder.dialogs.legacy-draft-kept.title",
                            message: "pageBuilder.dialogs.legacy-draft-kept.message"
                        });
                    }
                    loadLegacyDraft();
                    blade.parentBlade.refresh();
                }, function (error) {
                    blade.isLoading = false;
                    var message = error.data && error.data.error ? error.data.error : 'Error ' + error.status;
                    bladeNavigationService.setError(message, blade);
                });
            }

            function gitUrl(route) {
                return `api/pagebuilder/${route}?storeId=${encodeURIComponent(blade.storeId)}` +
                    `&type=${encodeURIComponent(blade.contentType)}` +
                    `&path=${encodeURIComponent(blade.currentEntity.relativeUrl)}`;
            }

            var unpublishCommand = {
                name: "pageBuilder.commands.unpublish", icon: 'fa fa-file-alt',
                executeMethod: function () {
                    if (blade.gitFlow) {
                        gitUnpublish();
                        return;
                    }
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
                // a pull request that is merging itself has the page on its way somewhere, and shipping
                // a second commit for it would only race with the first
                canExecuteMethod: function () { return !isDirty() && !inFlight(blade); }
            };

            function fillMetadata() {
                var blobName = blade.currentEntity.name || '';

                var blobNameParts = blobName.split('.');
                if (blobNameParts.length > 1) {
                    blobNameParts.pop(); // ignore extension
                }

                if (blade.languages && blade.languages.length) {
                    var possibleFileLanguage = blobNameParts.length > 1 ? blobNameParts[blobNameParts.length - 1] : '';

                    var language = blade.languages.find(function (lang) {
                        return lang.toLowerCase() === possibleFileLanguage.toLowerCase();
                    });

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
                var documentId = filesDraftService.getDocumentId(blade, false);
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

            function runDesigner() {
                if (blade.designerUrl) {
                    var relativeUrl = filesDraftService.getDraftFileName(blade);
                    var previewId = filesDraftService.getDocumentId(blade, true);
                    var parameters = `storeId=${blade.storeId}#/pages?type=${blade.contentType}&path=${relativeUrl}&previewId=${encodeURIComponent(previewId)}`;
                    // Pass the page language so the designer preview renders in that language (VCST-5219).
                    var designerLanguage = blade.currentEntity && blade.currentEntity.language;
                    if (designerLanguage) {
                        parameters += `&cultureName=${encodeURIComponent(designerLanguage)}`;
                    }
                    window.open(`${blade.designerUrl}?${parameters}`, '_blank');
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
                // Re-read the page the same way it was loaded — from the repository on the git flow. Taking
                // the content from blob here would save the designer's draft back to an older revision.
                pageBuilderApi.getPage({
                    storeId: $scope.blade.storeId,
                    type: $scope.blade.contentType,
                    path: $scope.blade.currentEntity.relativeUrl
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
                    return `/${path2}`;
                }
                if (path1.endsWith('/')) {
                    return `${path1}${path2}`;
                }
                return `${path1}/${path2}`;
            }

            // A save on the git flow is a commit on this editor's work branch. Writing the blob draft
            // instead would put these settings where nothing reads them, and the next deploy — which
            // syncs blob storage from the repository — would drop them.
            function saveToGit() {
                var entity = $scope.blade.currentEntity;
                var envelope = { settings: entity.settings, content: entity.content };

                pageBuilderApi.saveDraft({ storeId: blade.storeId }, {
                    files: JSON.stringify([{
                        path: entity.relativeUrl,
                        type: blade.contentType,
                        // the legacy flat array is still a valid page document — keep the shape the page had
                        content: entity.version === 1 ? [envelope.settings].concat(envelope.content) : envelope
                    }])
                }, function () {
                    blade.isLoading = false;
                    blade.hasChanges = true;
                    postMessageToPageBuilder({ source: 'platform', published: blade.published, hasChanges: true });
                    if (!blade.isNew) {
                        // a commit that says the same thing as production is not a change — let the server
                        // decide instead of assuming
                        loadPublishStatus();
                    }
                    saveSuccess();
                }, saveError);
            }

            // The permalink is stored with a leading slash; the blob save normalizes it in the resource's
            // transformRequest, which the git save does not go through.
            function normalizePermalink(pageSettings) {
                var permalink = pageSettings && pageSettings.permalink;
                if (permalink && permalink.length && permalink[0] !== '/') {
                    pageSettings.permalink = '/' + permalink;
                }
            }

            function savePage(newFileName, originFileName) {
                if (!flowResolved) {
                    // The flow says where this save goes, so the save waits for it — and gives up when it
                    // does not come. Not saving is recoverable; saving to the wrong store is not.
                    loadPublishStatus(function (resolved) {
                        if (resolved) {
                            savePage(newFileName, originFileName);
                            return;
                        }
                        blade.isLoading = false;
                    });
                    return;
                }

                normalizePermalink($scope.blade.currentEntity.settings);
                $scope.blade.currentEntity.relativeUrl = joinPath($scope.blade.parentBlade.currentEntity?.relativeUrl || '', newFileName);
                $scope.blade.currentEntity.relativeUrl = nameHelper.prepareRelativeUrl($scope.blade.currentEntity);

                var oldRelativeUrl = blade.origEntity && blade.origEntity.relativeUrl;
                var oldLanguage = $scope.blade.origEntity && $scope.blade.origEntity.language;
                var newLanguage = $scope.blade.currentEntity.language;

                $scope.blade.currentEntity.name = newFileName;

                if (blade.gitFlow) {
                    saveToGit();
                    return;
                }

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
                var managed = [publishCommand, unpublishCommand, promoteCommand, deleteLegacyDraftCommand];
                $scope.blade.toolbarCommands = blade.toolbarCommands.filter(x => managed.indexOf(x) < 0);

                // Nothing to publish and the page is live: the one thing left to do with it is take it
                // down. On the git flow that is a commit deleting the page, merged like any other.
                var command = $scope.blade.published && !$scope.blade.hasChanges
                    ? unpublishCommand
                    : publishCommand;
                $scope.blade.toolbarCommands.splice(4, 0, command);

                // Publishing reaches the environment editors work against; promotion reaches the
                // public site. The command exists only where there is a production branch to reach.
                if (blade.production) {
                    $scope.blade.toolbarCommands.splice(5, 0, promoteCommand);
                }

                // Last, and only for a page that actually has a leftover: a rare, destructive one-off
                // rather than part of the everyday toolbar.
                if (blade.legacyDraft) {
                    $scope.blade.toolbarCommands.push(deleteLegacyDraftCommand);
                }
            }

            function saveError(error) {
                bladeNavigationService.setError('Error ' + error.status, blade);
            }

            blade.onClose = function (closeCallback) {
                bladeNavigationService.showConfirmationIfNeeded(isDirty(), canSave(), blade,
                    $scope.saveChanges, closeCallback, "content.dialogs.page-save.title", "content.dialogs.page-save.message");
            };

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

