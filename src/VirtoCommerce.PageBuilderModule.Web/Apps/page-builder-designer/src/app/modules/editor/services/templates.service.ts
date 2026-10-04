import { Injectable, inject } from "@angular/core";
import { HttpErrorResponse, HttpHeaders } from '@angular/common/http';

import { BuilderHttpClient, AppConfig } from '@integration/services';
import { PageModel, SectionModel, TemplateModel } from '@models/document';
import { Observable, defer, map, of, tap, throwError } from "rxjs";

import { helpers } from '@editor/helpers';
import { PageHistory, ProductionStatus } from '@editor/models';
import { ServerRequestDescriptor } from '@models/http';
import { TemplateEntry } from '@shared/models';

export interface PublishStatus {
    published: boolean;
    hasChanges: boolean;
    /** Only the git flow reports this: a pull request for the page is open and has not merged yet. */
    pending?: boolean;
    /**
     * That pull request needs another Publish to finish: nothing will merge it on its own, because
     * the content repository does not allow auto-merge.
     */
    awaitingMerge?: boolean;
    /**
     * Where the page stands on the production branch, or null where the installation has none.
     * Reported apart from the fields above because a page can be published and production still
     * be serving last week's copy of it — the in-between state that makes an editor say the site
     * did not update.
     */
    production?: ProductionStatus | null;
}

@Injectable({
    providedIn: 'root'
})
export class TemplatesService {

    private readonly http = inject(BuilderHttpClient);
    private readonly appConfig = inject(AppConfig);
    private readonly pageVersions = new Map<string, { eTag: string }>();

    // this method requires templateId and parent to identify template, end template entry to fill out a request
    getTemplate(path: string, type: string, template: TemplateEntry, groupId: string): Observable<TemplateModel | null> {
        const entry = { ...template, path, groupId }
        if (!entry.groupId && !entry.path) {
            return of(null);
        }

        // get template depends of its type. If no such type, use '__template' entry
        const templateUrl = this.appConfig.getValueByEntryType('templateUrl', { item: entry, type, path, groupId }, entry.type || type);
        return defer(() => {
            const request = this.http.generateRequest(templateUrl, { item: entry });
            // Reloads own a new document/version pair, including while a save is pending.
            this.pageVersions.delete(groupId);
            for (const item of Array.isArray(request) ? request : [request]) {
                if (item && typeof item !== 'string' && item.versioned) {
                    item.cacheable = false;
                }
            }
            return this.http.doRequest<TemplateModel | SectionModel[] | PageModel | { content: string, eTag: string }>(
                request, { nullWhenError: false }, null).pipe(
                map(result => {
                    const versioned = groupId && result && 'eTag' in result && typeof result.content === 'string';
                    const document = versioned ? JSON.parse(result.content) : result;
                    const converted = helpers.convertTemplateIntoCorrectVersion(document);
                    const model = converted ? helpers.prepareTemplate(converted) : null;
                    this.setPageVersion(groupId, model && versioned ? result.eTag : null);
                    return model;
                }),
                tap({ error: () => this.pageVersions.delete(groupId) }),
            );
        });
    }

    getTemplatePublishStatus(path: string, type: string, entry: TemplateEntry, groupId: string): Observable<PublishStatus | null> {
        const value = groupId ? 'publishPages' : 'publish';
        const publishStatusUrls = this.appConfig.getValueByEntryType(value, { item: entry, type, path, groupId }, entry.type || type);
        // No descriptor at all: this store has no publishing surface, or the configuration could
        // not be read. Either way there is no status to report, and inventing one would put a
        // Publish button on a page whose flow we do not know.
        if (!publishStatusUrls || !publishStatusUrls['status']) {
            return of(null);
        }
        const statusUrl = publishStatusUrls['status'];
        const request = this.http.generateRequest(statusUrl, { item: entry });
        return this.http.doRequest<PublishStatus>(request, { nullWhenError: false }, null).pipe(
            map(result => result || { published: true, hasChanges: false })
        );
    }

    /**
     * `rebase` is the way out of a conflict the server has offered (a 409 with `canRebase`): the draft
     * is published as it is on top of the current page, replacing what changed there. Never sent on the
     * first attempt — it is the editor's answer to a question, not a default.
     */
    publishTemplate(path: string, type: string, entry: TemplateEntry, groupId: string, options: { rebase?: boolean } = {}): Observable<any> {
        const value = groupId ? 'publishPages' : 'publish';
        const publishStatusUrls = this.appConfig.getValueByEntryType(value, { item: entry, type, path, groupId }, entry.type || type);
        const statusUrl = publishStatusUrls['publish'];
        const request = this.http.generateRequest(statusUrl, { item: entry });
        if (options.rebase && request && typeof request === 'object' && !Array.isArray(request)) {
            request.url = `${request.url}${request.url.includes('?') ? '&' : '?'}rebase=true`;
        }
        return this.http.doRequest(request, { nullWhenError: false }, null);
    }

    unpublishTemplate(path: string, type: string, entry: TemplateEntry, groupId: string): Observable<any> {
        const value = groupId ? 'publishPages' : 'publish';
        const publishStatusUrls = this.appConfig.getValueByEntryType(value, { item: entry, type, path, groupId }, entry.type || type);
        const statusUrl = publishStatusUrls['unpublish'];
        const request = this.http.generateRequest(statusUrl, { item: entry });
        return this.http.doRequest(request, { nullWhenError: false }, null);
    }

    /**
     * The second step of shipping: the page's state on the base branch placed onto the release
     * branch. Only the git flow offers the descriptor, so only there does the button exist.
     */
    promoteTemplate(path: string, type: string, entry: TemplateEntry, groupId: string): Observable<any> {
        const value = groupId ? 'publishPages' : 'publish';
        const publishStatusUrls = this.appConfig.getValueByEntryType(value, { item: entry, type, path, groupId }, entry.type || type);
        const statusUrl = publishStatusUrls['promote'];
        const request = this.http.generateRequest(statusUrl, { item: entry });
        return this.http.doRequest(request, { nullWhenError: false }, null);
    }

    externalPreview(path: string, type: string, entry: TemplateEntry, groupId: string): void {
        const previewUrl = this.appConfig.getValue('externalPreview', { item: entry, type, path, groupId });
        // open new tab with the previewUrl
        window.open(previewUrl.url, '_blank');
    }

    /**
     * Versions of the page. The descriptor exists only for a store whose pages live in git, so a `null`
     * config here is the answer "this store keeps no history" rather than a failure.
     */
    getPageHistory(path: string, type: string, entry: TemplateEntry, groupId: string, after?: string): Observable<PageHistory | null> {
        const context = { item: entry, type, path, groupId };
        const history = this.appConfig.getValue('history', context);
        if (!history?.url) {
            return of(null);
        }
        const url = after ? `${history.url}&after=${encodeURIComponent(after)}` : history.url;
        const request = this.http.generateRequest(url, null, context);
        return this.http.doRequest<PageHistory>(request, { nullWhenError: false }, null);
    }

    /**
     * Continues editing from an earlier version: the server appends its content to my own work branch.
     * Nothing is rewritten, so the version this came from — and my current draft — stay in history.
     */
    restoreVersion(path: string, type: string, entry: TemplateEntry, groupId: string, sha: string): Observable<{ branch: string, commitSha: string } | null> {
        const context = { item: entry, type, path, groupId, sha };
        const restore = this.appConfig.getValue('history', context)?.restore;
        const request = this.http.generateRequest(restore, null, context);
        return this.http.doRequest<{ branch: string, commitSha: string }>(request, { nullWhenError: false }, null);
    }

    /** Opens the storefront preview of one exact commit — a sha, so the link keeps showing what it showed. */
    previewVersion(path: string, type: string, entry: TemplateEntry, groupId: string, sha: string): void {
        const preview = this.appConfig.getValue('history', { item: entry, type, path, groupId, sha })?.preview;
        if (preview?.url) {
            window.open(preview.url, '_blank');
        }
    }

    saveGroupedPage(groupId: string, pageContent: any): Observable<any> {
        const context = { groupId, content: pageContent };
        // Evaluate the descriptor once. A second evaluation would execute template syntax in page text.
        const descriptor = this.appConfig.getContext().config.saveGroupedPage;
        const request = this.http.generateRequest(descriptor, null, context);
        const requests = Array.isArray(request) ? request : [request];
        const isDescriptor = (value: unknown): value is ServerRequestDescriptor =>
            !!value && typeof value === 'object' && 'url' in value && !!value.url;
        if (!requests.length || !requests.every(isDescriptor)) {
            return throwError(() => new Error('The page save request is unavailable.'));
        }
        if (!requests.some(value => value.versioned)) {
            return this.http.doRequest(request, { nullWhenError: false }, null);
        }
        if (Array.isArray(request)) {
            return throwError(() => new Error('A versioned page save requires one request.'));
        }
        const versionedRequest = requests[0];
        const version = this.pageVersions.get(groupId);
        if (!version) {
            return throwError(() => new HttpErrorResponse({ status: 428, error: 'The page version is unavailable.' }));
        }
        const headers = versionedRequest.options?.headers instanceof HttpHeaders
            ? versionedRequest.options.headers : new HttpHeaders(versionedRequest.options?.headers);
        versionedRequest.options = { ...versionedRequest.options, headers: headers.set('If-Match', version.eTag) };
        versionedRequest.cacheable = false;
        return this.http.doRequest<{ eTag: string }>(versionedRequest, { nullWhenError: false }, null).pipe(
            tap(result => {
                // A reload owns its own version, even if it read the same ETag while this save was pending.
                if (this.pageVersions.get(groupId) === version) {
                    this.setPageVersion(groupId, result?.eTag);
                }
            }),
        );
    }

    private setPageVersion(groupId: string, eTag: string | null | undefined): void {
        if (eTag) {
            this.pageVersions.set(groupId, { eTag });
        } else {
            this.pageVersions.delete(groupId);
        }
    }

    saveTemplates(templates: { entry: TemplateEntry, content: TemplateModel }[]): Observable<any> {
        const templatesToSave = templates.map(template => (
            {
                ...template,
                content: helpers.prepareTemplateForSave(template.content)
            }));
        const context = { templatesToSave };
        const saveTemplates = this.appConfig.getValue('saveTemplates', context);
        const request = this.http.generateRequest(saveTemplates, null, context);
        return this.http.doRequest(request);
    }

}
