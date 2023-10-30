//angular.module('virtoCommerce.contentModule')
//    .controller('virtoCommerce.contentModule.pagesListController', ['$rootScope',
//        '$scope', 'virtoCommerce.contentModule.contentApi', 'platformWebApp.bladeNavigationService',
//        'platformWebApp.dialogService', 'platformWebApp.uiGridHelper', 'platformWebApp.bladeUtils',
//        function ($rootScope, $scope, contentApi, bladeNavigationService, dialogService, uiGridHelper, bladeUtils) {
//        var blade = $scope.blade;
//        blade.updatePermission = 'content:update';
//        $scope.selectedNodeId = null;

//        blade.refresh = function () {
//            blade.isLoading = true;
//            var query = blade.searchKeyword ? contentApi.search : contentApi.query;
//            query({
//                    contentType: blade.contentType,
//                    storeId: blade.storeId,
//                    keyword: blade.searchKeyword,
//                    folderUrl: blade.currentEntity.relativeUrl
//                },
//                function (data) {
//                    $scope.pageSettings.totalItems = data.length;
//                    _.each(data, function (x) {
//                        x.isImage = x.mimeType && x.mimeType.startsWith('image/');
//                        x.isDesignPage = /.+\.page(\-draft){0,1}$/g.test(x.name);
//                        x.isOpenable = x.mimeType && (x.mimeType.startsWith('application/j') || x.mimeType.startsWith('text/'))
//                            || x.isDesignPage;
//                    });
//                    $scope.listEntries = data;
//                    blade.isLoading = false;

//                    //Set navigation breadcrumbs
//                    setBreadcrumbs();
//                }, function (error) {
//                    bladeNavigationService.setError('Error ' + error.status, blade);
//                });
//        };


//        $scope.selectNode = function (listItem) {
//            if (listItem.type === 'folder') {
//                var newBlade = {
//                    id: blade.id,
//                    contentType: blade.contentType,
//                    storeId: blade.storeId,
//                    storeUrl: blade.storeUrl,
//                    languages: blade.languages,
//                    currentEntity: listItem,
//                    breadcrumbs: blade.breadcrumbs,
//                    title: blade.title,
//                    subtitle: blade.subtitle,
//                    controller: blade.controller,
//                    template: blade.template,
//                    disableOpenAnimation: true,
//                    isClosingDisabled: blade.isClosingDisabled
//                };
//                bladeNavigationService.showBlade(newBlade, blade.parentBlade);
//            } else {
//                blade.selectedNodeId = listItem.url;
//                openDetailsBlade(listItem, false);
//            }
//        };

//        function openDetailsBlade(listItem, isNew) {
//            if (isNew || listItem.isOpenable) {
//                var newBlade = {
//                    id: 'pageDetail',
//                    contentType: blade.contentType,
//                    storeId: blade.storeId,
//                    storeUrl: blade.storeUrl,
//                    languages: blade.languages,
//                    folderUrl: blade.currentEntity.relativeUrl,
//                    currentEntity: listItem,
//                    isNew: isNew,
//                    title: listItem.name,
//                    controller: 'virtoCommerce.contentModule.pageDetailController',
//                    template: 'Modules/$(VirtoCommerce.Content)/Scripts/blades/pages/page-detail.tpl.html'
//                };
//                if (isNew) {
//                    angular.extend(newBlade, {
//                        controller: 'virtoCommerce.pageBuilderModule.pageAddController',
//                        template: 'Modules/$(VirtoCommerce.PageBuilderModule)/Scripts/blades/pages/page-add.tpl.html'
//                    });
//                }

//                if (listItem.isDesignPage) {
//                    openJsonDetailsBlade(listItem, isNew);
//                }
//                else {
//                    if (isBlogs()) {
//                        if (isNew) {
//                            angular.extend(newBlade, {
//                                title: 'content.blades.edit-page.title-new-post',
//                                subtitle: 'content.blades.edit-page.subtitle-new-post'
//                            });
//                        } else {
//                            angular.extend(newBlade, {
//                                subtitle: 'content.blades.edit-page.subtitle-post'
//                            });
//                        }
//                    } else {
//                        if (isNew) {
//                            angular.extend(newBlade, {
//                                title: 'content.blades.edit-page.title-new',
//                                subtitle: 'content.blades.edit-page.subtitle-new'
//                            });
//                        } else {
//                            angular.extend(newBlade, {
//                                subtitle: 'content.blades.edit-page.subtitle'
//                            });
//                        }
//                    }
//                    bladeNavigationService.showBlade(newBlade, blade);
//                }
//            }
//        }

//        function openJsonDetailsBlade(listItem, isNew) {
//            var newBlade = {
//                id: 'jsonDetail',
//                contentType: blade.contentType,
//                storeId: blade.storeId,
//                storeUrl: blade.storeUrl,
//                currentEntity: listItem,
//                folderUrl: blade.currentEntity.url,
//                isNew: isNew,
//                title: listItem.name,
//                subtitle: 'content.blades.edit-page.subtitle',
//                controller: 'virtoCommerce.pageBuilderModule.editPageController',
//                template: 'Modules/$(VirtoCommerce.PageBuilderModule)/Scripts/blades/pages/edit-page.tpl.html'
//            };

//            if (isNew) {
//                angular.extend(newBlade, {
//                    title: 'content.blades.edit-page.title-new',
//                    subtitle: 'content.blades.edit-page.subtitle-new'
//                });
//            }

//            bladeNavigationService.showBlade(newBlade, blade);
//        }
