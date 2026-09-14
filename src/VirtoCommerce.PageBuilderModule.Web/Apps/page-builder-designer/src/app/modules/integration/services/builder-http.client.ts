import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHandler } from '@angular/common/http';
import { tap, catchError, map, Observable, of, switchMap } from 'rxjs';

import { AppConfig, EvaluatorService } from '@integration/services';
import { ServerRequestDescriptor, ServerResponseDescriptor } from '@models/http';
import { appHelpers } from '@integration/helpers';

type CustomRequest = string | ServerRequestDescriptor | null;
type CustomRequests = CustomRequest | CustomRequest[] | null;

@Injectable({
    providedIn: 'root'
})
export class BuilderHttpClient extends HttpClient {

    private readonly _defaultOpts = {
        nullWhenError: true
    };

    private readonly cacheSize = 100;

    private readonly _cache: Map<string, any> = new Map();

    private readonly evaluator = inject(EvaluatorService);
    private readonly appConfig = inject(AppConfig);

    constructor() {
        super(inject(HttpHandler));
    }

    /**
     * Drops every cached response. The store request behind the settings is cacheable, so a store
     * URL that has just been corrected in the Manager would otherwise stay invisible to a reload
     * of the configuration and the designer would keep computing the same broken address
     * (VCST-5847).
     */
    clearCache(): void {
        this._cache.clear();
    }

    doRequest<T>(request: CustomRequests, additionalOptions: any = null, context: any = null): Observable<T | null> {
        if (!request) {
            return of(null);
        }
        const requests = Array.isArray(request) ? request : [request];
        return this.queueRequests(requests.shift(), requests, additionalOptions, context);
    }

    private queueRequests<T>(request: CustomRequest | undefined, requests: CustomRequests, additionalOptions: any = null, context: any = null): Observable<T | null> {
        if (!request) {
            return of(null);
        }
        return this.doRequestInternal<T>(<any>request, additionalOptions, context).pipe(
            catchError(error => {
                const isLast = !requests || (Array.isArray(requests) && requests.length === 0);
                if (!isLast) {
                    console.log(error);
                    return this.queueRequests<T>((<any>requests).shift(), requests, additionalOptions, context);
                }
                if (additionalOptions?.nullWhenError) {
                    return of(null);
                }
                throw error;
            }),
            switchMap(result => {
                if (result === null || result === <any>'' || result === undefined) {
                    const fallbackValue = (request && typeof request !== 'string') ? request.fallbackValue : null;
                    if (fallbackValue) {
                        return of(fallbackValue);
                    }
                    return this.queueRequests<T>((<any>requests).shift(), requests, additionalOptions, context);
                }
                return of(result);
            })
        );
    }

    private doRequestInternal<T>(request: ServerRequestDescriptor | null, additionalOptions: any = null, context: any = null): Observable<T | null> {
        if (!request) {
            return of(null);
        }
        const { method, url, body, options } = request;
        const opts = { ...this._defaultOpts, ...additionalOptions };

        if (!url) {
            return of(null);
        }

        let result;

        const cacheKey = JSON.stringify({ method, url, body, options });
        if (this._cache.has(cacheKey)) {
            result = of(this._cache.get(cacheKey));
        } else {
            const uppercaseMethod = method?.toUpperCase();
            switch (uppercaseMethod) {
                case 'POST':
                    result = super.post<T>(url, body, options);
                    break;
                case 'GET':
                default:
                    result = super.get<T>(url, options);
                    break;
            }
            result = result.pipe(
                tap(x => {
                    if (request.cacheable) {
                        this._cache.set(cacheKey, x);
                    }
                    if (this._cache.size > this.cacheSize) {
                        const key = this._cache.keys().next().value;
                        key && this._cache.delete(key);
                    }
                })
            );
        }
        result = result.pipe(
            map(response => {
                return this.mapResponseToResult(response, request.response || null, this.getCurrentContext(context));
            })
        );
        if (opts.nullWhenError) {
            result = result.pipe(
                catchError(error => {
                    console.log(error);
                    return of(null);
                })
            );
        }
        return result;
    }

    generateRequest(request: CustomRequests, data: any = null, context: any = null): CustomRequests {
        if (!request) {
            return null;
        }
        if (Array.isArray(request)) {
            return <any>request.map(x => this.generateRequestInternal(x, data, context));
        }
        return this.generateRequestInternal(request, data, context);
    }

    private generateRequestInternal(request: string | ServerRequestDescriptor | null, data: any = null, context: any = null): ServerRequestDescriptor | null {
        if (!request) {
            return null;
        }
        if (typeof request === 'string') {
            return {
                url: this.evaluator.evaluate(request, this.getCurrentContext(context)),
                method: 'GET',
                body: null,
                options: {
                    responseType: 'json'
                }
            };
        }
        const evaluatedRequest = this.evaluator.evaluate(request, this.getCurrentContext(context));
        const result = {
            url: evaluatedRequest.url,
            cacheable: evaluatedRequest.cacheable,
            method: evaluatedRequest.method || 'GET',
            body: evaluatedRequest.body,
            response: evaluatedRequest.response,
            fallbackValue: evaluatedRequest.fallbackValue,
            options: {
                responseType: 'json',
                ...evaluatedRequest.options
            }
        };

        if (evaluatedRequest.form) {
            const form = new FormData();
            if (evaluatedRequest.form.fileName) {
                form.append(evaluatedRequest.form.name, data, evaluatedRequest.form.fileName);
            } else {
                form.append(evaluatedRequest.form.name, data);
            }
            result.body = form;
        } else if (data) {
            const form = new FormData();
            if (data.fileName) {
                form.append(data.name, data.value, data.fileName);
            } else {
                form.append(data.name, data.value);
            }
            result.body = form;
        }

        return result;
    }

    private mapResponseToResult(response: any, descriptor: ServerResponseDescriptor | null, context: any): any {
        if (!descriptor) {
            return response;
        }
        let result = response;
        if (result) {
            if (descriptor.selector) {
                const script = descriptor.selector;
                result = appHelpers.evalInContext(script, { ...context, response: result }); // todo: maybe here should be some additional data
            }
            if (descriptor.result) {
                result = appHelpers.getValueByPath(result, descriptor.result);
                result = appHelpers.arrayCastByConfig(result, descriptor.isArray);
            }
        }
        if (!result) {
            return result;
        }

        if (descriptor.value?.length) {
            // we need to get value from response
            // result may be array or object
            // response also may be array or object
            // so we need to get value from response by path or by object, where each property is path
            const resultMapper = Array.isArray(result)
                ? (value: any, arrayMapper: any) => (<any[]>value).map(v => arrayMapper(v, descriptor.value))
                : (value: any, elementMapper: any) => elementMapper(value, descriptor.value);
            const itemMapper = (typeof descriptor.value === 'string')
                ? (v: any, d: any) => appHelpers.getValueByPath(v, d)
                : (v: any, d: any) => appHelpers.getItemValue(v, d);

            result = resultMapper(result, itemMapper);

        }
        return result;
    }

    private getCurrentContext(context: any): any {
        const config = this.appConfig;
        return { ...config.getContext(), ...context };
    }
}
