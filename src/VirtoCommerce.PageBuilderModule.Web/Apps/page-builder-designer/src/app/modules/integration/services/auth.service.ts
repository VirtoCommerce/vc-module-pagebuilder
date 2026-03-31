import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { BuilderHttpClient } from './builder-http.client';
import { HttpHeaders } from '@angular/common/http';

@Injectable({
    providedIn: 'root'
})
export class AuthService {

    private readonly http = inject(BuilderHttpClient);

    refreshToken(token: string): Observable<any> {
        const url = '/connect/token'; // todo: move to settings/config
        const headers = new HttpHeaders({
            'x-refresh': 'true',
            'content-type': 'application/x-www-form-urlencoded'
        });
        const body = `grant_type=refresh_token&refresh_token=${token}`;
        return this.http.post<any>(url, body, { headers });
    }

}
