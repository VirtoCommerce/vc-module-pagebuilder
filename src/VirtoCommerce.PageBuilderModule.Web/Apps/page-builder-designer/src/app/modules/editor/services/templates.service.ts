import { Injectable, inject } from "@angular/core";

import { BuilderHttpClient, AppConfig } from '@integration/services';
import { PageModel, SectionModel, TemplateModel } from '@models/document';
import { Observable, map, of } from "rxjs";

import { helpers } from '@editor/helpers';
import { PageHistory } from '@editor/models';
import { TemplateEntry } from '@shared/models';

export interface PublishStatus {
    published: boolean;
    hasChanges: boolean;
    /** Only the git flow reports this: a pull request for the page is open and has not merged yet. */
    pending?: boolean;
}

@Injectable({
    providedIn: 'root'
})
export class TemplatesService {

    private readonly http = inject(BuilderHttpClient);
    private readonly appConfig = inject(AppConfig);

    // this method requires templateId and parent to identify template, end template entry to fill out a request
    getTemplate(path: string, type: string, template: TemplateEntry, groupId: string): Observable<TemplateModel | null> {
        const entry = { ...template, path, groupId }
        if (!entry.groupId && !entry.path) {
            return of(null);
        }

        // get template depends of its type. If no such type, use '__template' entry
        const templateUrl = this.appConfig.getValueByEntryType('templateUrl', { item: entry, type, path, groupId }, entry.type || type);
        const targetUrl = templateUrl;
        const request = this.http.generateRequest(targetUrl, { item: entry });
        return this.http.doRequest<TemplateModel | SectionModel[] | PageModel>(request, { nullWhenError: false }, null).pipe(
            map(result =>
                helpers.convertTemplateIntoCorrectVersion(result)
            )
        );
    }

    getTemplatePublishStatus(path: string, type: string, entry: TemplateEntry, groupId: string): Observable<PublishStatus> {
        const value = groupId ? 'publishPages' : 'publish';
        const publishStatusUrls = this.appConfig.getValueByEntryType(value, { item: entry, type, path, groupId }, entry.type || type);
        const statusUrl = publishStatusUrls['status'];
        const request = this.http.generateRequest(statusUrl, { item: entry });
        return this.http.doRequest<PublishStatus>(request, { nullWhenError: false }, null).pipe(
            map(result => result || { published: true, hasChanges: false })
        );
    }

    publishTemplate(path: string, type: string, entry: TemplateEntry, groupId: string): Observable<any> {
        const value = groupId ? 'publishPages' : 'publish';
        const publishStatusUrls = this.appConfig.getValueByEntryType(value, { item: entry, type, path, groupId }, entry.type || type);
        const statusUrl = publishStatusUrls['publish'];
        const request = this.http.generateRequest(statusUrl, { item: entry });
        return this.http.doRequest(request, { nullWhenError: false }, null);
    }

    unpublishTemplate(path: string, type: string, entry: TemplateEntry, groupId: string): Observable<any> {
        const value = groupId ? 'publishPages' : 'publish';
        const publishStatusUrls = this.appConfig.getValueByEntryType(value, { item: entry, type, path, groupId }, entry.type || type);
        const statusUrl = publishStatusUrls['unpublish'];
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
        const saveGroupedPage = this.appConfig.getValue('saveGroupedPage', context);
        const request = this.http.generateRequest(saveGroupedPage, null, context);
        return this.http.doRequest(request);
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
