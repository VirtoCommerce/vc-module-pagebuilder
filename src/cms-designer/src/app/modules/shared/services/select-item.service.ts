import { PlatformService } from 'src/app/services/platform.service';
import { Injectable } from '@angular/core';
import { OptionsRequest, OptionModel, SelectControlDescriptor, ValueDescriptorModel } from '@shared/models';
import * as jp from 'jsonpath';
import { map, tap } from 'rxjs/operators';
import { Observable, of } from 'rxjs';
import { isArray } from 'util';

@Injectable({
    providedIn: 'root'
})
export class SelectItemService {

    private cache: { [url: string]: OptionModel[] } = { };

    constructor(private readonly platform: PlatformService) { }

    public getRequestedOptions(descriptor: SelectControlDescriptor): Observable<OptionModel[]> {
        if (!descriptor.request) {
            return of([]);
        }

        const cacheKey = this.getCacheKey(descriptor.request);
        if (!this.cache[cacheKey]) {
            const { url, params, method } = descriptor.request;
            const { group, label, value } = descriptor.request;
            return this.platform.loadData<{results: any[]}>(url, params, method).pipe(
                map(({ results }) => results.map<OptionModel>(x => <OptionModel>{
                        label: x[label],
                        group: x[group],
                        value: this.getValue(x, value)
                    })
                ),
                tap(result => {
                    this.cache[cacheKey] = result;
                })
            );
        }

        return of(this.cache[cacheKey]);
    }

    private getCacheKey(request: OptionsRequest): string {
        const params = JSON.stringify(request.params);
        return btoa(`${request.method}${request.url}${params}`);
    }

    private getValue(item: any, valueDescriptor: string | (string | ValueDescriptorModel)[]): any {
        if (isArray(valueDescriptor)) {
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
