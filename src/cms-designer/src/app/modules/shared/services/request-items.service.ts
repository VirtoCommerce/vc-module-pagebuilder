import { PlatformService } from 'src/app/services/platform.service';
import { Injectable } from '@angular/core';
import { OptionsRequest, OptionModel, SelectControlDescriptor, ValueDescriptorModel, SearchControlDescriptor, ServerRequest } from '@shared/models';
import * as jp from 'jsonpath';
import { map, tap } from 'rxjs/operators';
import { Observable, of } from 'rxjs';

@Injectable({
    providedIn: 'root'
})
export class RequestItemsService {

    private cache: { [url: string]: OptionModel[] } = {};

    constructor(private platform: PlatformService) { }

    doSearchRequest(request: ServerRequest, query: string): Observable<any[]> {
        if (!request) {
            return of([]);
        }


        let { params } = request;
        const { url, value, method, searchField } = request;
        if (!!query) {
            params = { ...params, [searchField]: query };
        }

        return this.platform.loadData<any>(url, params, method).pipe(
            map(results => {
                if (!!request.resultField) {
                    return results[request.resultField];
                }
                if (Array.isArray(results)) {
                    return results;
                }
                const res = results['results'];
                if (res && Array.isArray(res)) {
                    return res;
                }
                return results;
            }),
            map(items => items.map(item => this.getValue(item, value)))
        );
    }

    getRequestedOptions(descriptor: ServerRequest, useCache: boolean, search: string): Observable<any[]> {
        if (!descriptor) {
            return of([]);
        }

        const cacheKey = useCache ? this.getCacheKey(descriptor, search) : null;
        if (!cacheKey || !this.cache[cacheKey]) {
            return this.doSearchRequest(descriptor, search).pipe(
                tap(result => {
                    if (cacheKey) {
                        this.cache[cacheKey] = result;
                    }
                })
            );
        }

        return of(this.cache[cacheKey]);
    }

    private getCacheKey(request: ServerRequest, query: string): string {
        const params = JSON.stringify(request.params);
        return btoa(`${request.method}${request.url}${params}${query}`);
    }

    private getValue(item: any, valueDescriptor: string | (string | ValueDescriptorModel)[]): any {
        if (Array.isArray(valueDescriptor)) {
            const result = {};
            const properties = <(string | ValueDescriptorModel)[]>valueDescriptor;
            properties.forEach(x => {
                if (typeof x === 'string') {
                    result[x] = item[x];
                } else {
                    const value = jp.query(item, x.query);
                    result[x.key] = x.isArray ? value : value[0]; // ???
                }
            });
            return result;
        } else {
            const property = <string>valueDescriptor;
            return item[property];
        }
    }
}
