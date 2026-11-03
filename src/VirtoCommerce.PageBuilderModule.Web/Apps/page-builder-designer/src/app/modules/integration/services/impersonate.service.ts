import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { BuilderHttpClient } from './builder-http.client';
import { HttpHeaders } from '@angular/common/http';

export interface ImpersonateTokenResponse {
    access_token: string;
    token_type: string;
    refresh_token: string;
    expires_in: number;
}

@Injectable({
    providedIn: 'root'
})
export class ImpersonateService {
    private readonly http = inject(BuilderHttpClient);

    getImpersonateToken(userId: string): Observable<ImpersonateTokenResponse> {
        const url = '/connect/token';
        const headers = new HttpHeaders({
            'content-type': 'application/x-www-form-urlencoded'
        });
        const body = `grant_type=impersonate&user_id=${encodeURIComponent(userId)}&scope=offline_access`;
        return this.http.post<ImpersonateTokenResponse>(url, body, { headers });
    }
}
