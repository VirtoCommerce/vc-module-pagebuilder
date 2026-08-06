angular.module('virtoCommerce.contentModule')
    .factory('virtoCommerce.pageBuilderModule.contentApi', ['$q', '$resource', 'virtoCommerce.pageBuilderModule.resourceNameService', function ($q, $resource, helper) {
        return $resource('api/content/:contentType/:storeId', null, {
            savePage: {
                method: 'POST',
                params: { draft: true },
                headers: { 'Content-Type': undefined },
                transformRequest: function (currentEntity) {
                    var blobname = currentEntity.name;
                    var fd = new FormData();
                    var permalink = currentEntity.settings.permalink;
                    if (permalink && permalink.length && permalink[0] !== '/') {
                        currentEntity.settings.permalink = '/' + permalink;
                    }
                    var content = { settings: currentEntity.settings, content: currentEntity.content };
                    if (currentEntity.version === 1) {
                        content = [content.settings].concat(content.content);
                    }
                    content = JSON.stringify(content, null, 4);
                    fd.append(blobname, content);
                    return fd;
                },
                isArray: true
            },
            get: {
                params: { draft: true },
                // using transformResponse to:
                // 1. avoid automatic response result string converting to array;
                transformResponse: function (rawData) { return { data: rawData }; }
            },
            // The page as the server resolves it: this editor's draft, else what is published — from the
            // content repository on the git flow, from blob storage otherwise. The blade reads through
            // this instead of blob directly, or on the git flow it would edit a stale copy and save it
            // back over the draft in the repository.
            getPage: {
                method: 'GET',
                url: 'api/pagebuilder/template',
                params: { draft: true },
                transformResponse: function (rawData) { return { data: rawData }; }
            },
            // published/hasChanges/pending plus the flow in effect ("git" or "blob"). Both flows answer
            // in this shape, so the blade asks once and does not have to guess how the store is set up.
            publishStatus: {
                method: 'GET',
                url: 'api/pagebuilder/git/publish-status'
            },
            // A draft save on the git flow: a commit on this editor's work branch.
            saveDraft: {
                method: 'POST',
                url: 'api/pagebuilder/save',
                params: { draft: true }
            },
            // Publishing on the git flow: merges the work branch into the production branch, or opens a
            // pull request that merges once its checks pass.
            gitPublish: {
                method: 'POST',
                url: 'api/pagebuilder/git/publish'
            }
			// ,
            // getStoreUrl: {
            //    url: 'api/stores/url/:storeId',
            //    method: 'GET',
            //    transformResponse: function(rawData) {
            //        return { data: rawData };
            //    }
            // }
    });
}]);
