import { Injectable } from '@angular/core';
import { OptionModel, SelectControlDescriptor } from '@shared/models';
import { HttpClient } from '@angular/common/http';
import * as jp from 'jsonpath';
import { map } from 'rxjs/operators';
import { Observable, of } from 'rxjs';
import { ApiUrlsService } from '@app/services';

@Injectable({
  providedIn: 'root'
})
export class SelectItemService {

  private descriptor: SelectControlDescriptor;

  constructor(private readonly http: HttpClient, private readonly urlService: ApiUrlsService) { }

  public getRequestedOptions(descriptor: SelectControlDescriptor): Observable<OptionModel[]> {
    this.descriptor = descriptor;
    if (!this.descriptor.request) {
      return of([]);
    }

    const url = this.urlService.params.platformUrl + this.descriptor.request.url;
    switch (this.descriptor.request.type) {
      case 'get': return this.http.get<any>(url, { params: this.descriptor.request.params })
        .pipe(map((data: any) => this.parseOptions(data)));
      case 'post': return this.http.post<any>(url, this.descriptor.request.params)
        .pipe(map((data: any) => this.parseOptions(data)));
    }
  }

  private parseOptions(data: any): OptionModel[] {
    return data.results
      .map(c => {
        return {
          label: c[this.descriptor.request.labelDescriptor], value: this.valueParse(c)
        } as OptionModel;
      });
  }

  private valueParse(value: any): any {
    let result: any = {};

    if (this.descriptor.request.valueDescriptor instanceof Array) {
      this.descriptor.request.valueDescriptor.forEach(c => {

        if (typeof c === 'string') {
          result[c] = value[c];
        } else {
          const currentResult = jp.query(value, c.query);
          result[c.key] = c.isArray ? currentResult : currentResult[0];
        }
      });
    } else {
      result = value[this.descriptor.request.valueDescriptor];
    }

    return result;
  }
}
