import { PlatformService } from 'src/app/services/platform.service';
import { Injectable } from '@angular/core';
import { OptionModel, ValueDescriptorModel, ServerResponseDescriptor, ServerRequestDescriptor, ComponentContext } from '@shared/models';
import * as jp from 'jsonpath';
import { catchError, map, tap } from 'rxjs/operators';
import { Observable, of } from 'rxjs';
import { template, getValueByPath } from '@app/services/utils';

@Injectable({
    providedIn: 'root'
})
export class RequestItemsService {

    private cache: { [url: string]: OptionModel[] } = {};

    constructor(private platform: PlatformService) { }

    /**
     * do request to server and return response
     * @param request request descriptor
     * @param query search query
     */
    doSearchRequest(request: ServerRequestDescriptor, context: ComponentContext): Observable<any[]> {
        if (!request) {
            return of([]);
        }
        const { method } = request;
        let { url, body } = request;
        if (!!body) {
            body = JSON.parse(template(JSON.stringify(body), context));
        } else {
            body = null;
        }
        url = template(url, context);
        return this.platform.loadData<any>(url, body, method).pipe(
            map(response => this.getResultValue(response, request.response)),
            catchError(error => {
                console.log(url, body, method, error);
                return of(null);
            })
        );
    }

    /**
     * request options from server with caching
     * @param descriptor request descriptor
     * @param search search query
     */
    getRequestedOptions(descriptor: ServerRequestDescriptor, context: ComponentContext): Observable<any[]> {
        if (!descriptor) {
            return of([]);
        }

        const cacheKey = this.getCacheKey(descriptor, context);
        if (!cacheKey || !this.cache[cacheKey]) {
            return this.doSearchRequest(descriptor, context).pipe(
                tap(result => {
                    if (cacheKey) {
                        this.cache[cacheKey] = result;
                    }
                })
            );
        }

        return of(this.cache[cacheKey]);
    }

    private getCacheKey(request: ServerRequestDescriptor, context: ComponentContext): string {
        if (!request.cacheContextPath) {
            return null;
        }
        const key = JSON.stringify(getValueByPath(context, request.cacheContextPath));
        return btoa(`${request.method}${request.url}${key}`);
    }

    private getResultValue(response: any, descriptor: ServerResponseDescriptor): any {
        let result = response;
        if (!!result) {
            if (descriptor.result) {
                result = getValueByPath(result, descriptor.result);
                result = this.arrayCastByConfig(result, descriptor.isArray);
            }
        }
        if (!result) return result;

        if (descriptor.value && descriptor.value.length) {
            // todo: refactor this!

            const resultMapper = Array.isArray(result)
                ? (value, itemMapper) => (<any[]>value).map(v => itemMapper(v, descriptor.value))
                : (value, itemMapper) => itemMapper(value, descriptor.value);
            const itemMapper = (typeof descriptor.value === 'string')
                ? (v, d) => getValueByPath(v, d)
                : (v, d) => this.getItemValue(v, d);
            
            result = resultMapper(result, itemMapper);

            // if (typeof descriptor.value === 'string') {
            //     if (Array.isArray(result)) {
            //         result = (<any[]>result).map(v => getValueByPath(v, descriptor.value));
            //     } else {
            //         result = getValueByPath(result, descriptor.value);
            //     }
            // }
            // else {
            //     if (Array.isArray(result)) {
            //         result = (<any[]>result).map(v => this.getItemValue(v, <(string | ValueDescriptorModel)[]>descriptor.value));
            //     } else {
            //         result = this.getItemValue(result, <(string | ValueDescriptorModel)[]>descriptor.value);
            //     }
            // }
        }
        return result;
    }

    private getItemValue(item: any, descriptor: (string | ValueDescriptorModel)[]): any {
        const result: any = {};
        descriptor.forEach(p => {
            const [query, property, isArray] = typeof p === 'string' ? [p, p, null] : [p.query, p.key, p.isArray];
            const x = getValueByPath(item, query);
            result[property] = this.arrayCastByConfig(x, isArray);
        });
        return result;
    }

    private arrayCastByConfig(item: any, isArray: boolean = null): any {
        if (isArray === null) {
            return item;
        }
        if (Array.isArray(item) && !isArray) {
            if (item.length > 0) {
                return item[0];
            } else {
                return null;
            }
        } else if (!Array.isArray(item) && isArray) {
            return [item];
        }
        return item;
    }
}
