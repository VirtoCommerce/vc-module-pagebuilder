import { Injectable, inject } from "@angular/core";

import { BuilderHttpClient, AppConfig } from '@integration/services';
import { PageModel, SectionModel, TemplateModel } from '@models/document';
import { Observable, map, of } from "rxjs";

import { helpers } from '@editor/helpers';
import { TemplateEntry } from '@shared/models';
import { } from '@integration/services';

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
        const targetUrl = templateUrl; //this.getValueByEntryType(templateUrl, entry, type);
        const request = this.http.generateRequest(targetUrl, { item: entry });
        return this.http.doRequest<TemplateModel | SectionModel[] | PageModel>(request, { nullWhenError: false }, null).pipe(
            map(template =>
                helpers.convertTemplateIntoCorrectVersion(template)
            )
        );
    }

    getTemplatePublishStatus(path: string, type: string, entry: TemplateEntry, groupId: string): Observable<{ published: boolean, hasChanges: boolean }> {
        const value = groupId ? 'publishPages' : 'publish';
        const publishStatusUrls = this.appConfig.getValueByEntryType(value, { item: entry, type, path, groupId }, entry.type || type);
        const statusUrl = publishStatusUrls['status'];
        const request = this.http.generateRequest(statusUrl, { item: entry });
        return this.http.doRequest<{ published: boolean, hasChanges: boolean }>(request, { nullWhenError: false }, null).pipe(
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

    // private getValueByEntryType(source: any, entry: TemplateEntry, type: string): any {
    //     return source[entry.type || type || 'default'] || source['default'] || source;
    // }
}
