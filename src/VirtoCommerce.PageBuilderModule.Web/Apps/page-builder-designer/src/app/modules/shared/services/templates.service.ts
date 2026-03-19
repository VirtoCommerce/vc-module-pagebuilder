import { ServerRequestDescriptor } from './../../models/http/server-request.descriptor';
import { Injectable, inject } from "@angular/core";
import { map, Observable, switchMap, of, catchError } from 'rxjs';

import { AppConfig } from '@integration/services';
import { BuilderHttpClient } from '@integration/services';
import { TemplateEntry, TemplateEntryList } from '@shared/models';

@Injectable({
    providedIn: 'root'
})
export class TemplatesService {

    private readonly http = inject(BuilderHttpClient);
    private readonly appConfig = inject(AppConfig);

    getTemplatesList(): Observable<TemplateEntryList | null> {
        const templatesListUrl = this.appConfig.getValue('templatesListUrl');
        const request = this.http.generateRequest(templatesListUrl);
        return this.http.doRequest<TemplateEntryList>(request).pipe(
            map(x => <any>x),
            switchMap(({ _templates, ...x }) => {
                let result = x;
                if (_templates) {
                    result = { ...x, ...(_templates.entries || {}) };
                    if (_templates.request) {
                        const requests = !Array.isArray(_templates.request) ? [_templates.request] : _templates.request;
                        return this.doRequests(requests.shift(), requests).pipe(
                            map(y => ({ ...result, ...y }))
                        );
                    }
                }
                return of(result);
            }),
            map(x => this.populateTemplatesProperties(x))
        );
    }

    getChildrenTemplates(templateEntry: TemplateEntry, context: any): Observable<TemplateEntryList> {
        const httpRequest = this.http.generateRequest(templateEntry.request || null, null, context);
        return this.http.doRequest<TemplateEntryList>(httpRequest, null, context).pipe(
            map(x => ({ ...templateEntry.children, ...x })),
            map(x => Object.keys(x).reduce((acc, key) => ({...acc, [key]: {...templateEntry, ...x[key]}}), {}))
        );
    }

    private doRequests(request: ServerRequestDescriptor | null, requests: ServerRequestDescriptor[], currentValue: any = {}): Observable<TemplateEntryList> {
        if (!request) {
            return of(currentValue);
        }
        return this.http.doRequest(request).pipe(
            catchError(error => {
                console.log(error);
                return this.doRequests(requests.shift() || null, requests, currentValue);
            }),
            switchMap((result: any) => {
                return this.doRequests(requests.shift() || null, requests, { ...currentValue, ...result });
            })
        );
    }

    private populateTemplatesProperties(entries: TemplateEntryList): TemplateEntryList {
        const result = Object.keys(entries)
            .map(key => {
                const protoKey = entries[key].prototype;
                if (!!protoKey) {
                    return <any>{
                        ...entries[protoKey],
                        disabled: false,
                        prototype: null,
                        request: null,
                        ...entries[key],
                        ___key: key
                    };
                }
                return { ...entries[key], ___key: key };
            }).filter(x => !x.disabled)
            .reduce((acc, { ___key, ...x }) => ({ ...acc, [___key]: x }), {})
        return result;
    }
}
