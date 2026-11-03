import { Injectable, inject } from '@angular/core';
import { BuilderHttpClient } from '@integration/services';
import { Observable } from 'rxjs';
import { ServerRequestDescriptor } from '@models/http';

@Injectable({
    providedIn: 'root'
})
export class DataService {
    private readonly http = inject(BuilderHttpClient);

    doRequest(request: ServerRequestDescriptor | string, context: any, data: any = null, httpServiceOptions: any = null): Observable<any> {
        const serverRequest = this.http.generateRequest(request, data, context);
        return this.http.doRequest(serverRequest, httpServiceOptions);
    }
}
