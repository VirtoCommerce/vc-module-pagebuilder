import { ApiUrlsService } from './api-url.service';
import { JwtStorageService } from './jwt-storage.service';
import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';

@Injectable({
    providedIn: 'root'
})
export class AuthService {

    private username: string;
    private password: string;

    constructor(private http: HttpClient, private jwt: JwtStorageService, private urls: ApiUrlsService) { }

    hasSavedInfo() {
        return !!this.username && !!this.password;
    }

    refreshToken(): Observable<any> {
        const token = this.jwt.getRefreshToken();
        if (token) {
            const data = 'grant_type=refresh_token&refresh_token=' + encodeURIComponent(token);
            return this.sendAuthRequest(data);
        }
        return of(null);
    }

    loginIfSaved(): Observable<any> {
        if (this.username && this.password) {
            return this.login(this.username, this.password, true)
        }
        return of(null);
    }

    login(username: string, password: string, save: boolean): Observable<any> {
        if (!!username && !!password) {
            if (save) {
                this.username = username;
                this.password = password;
            } else {
                this.username = null;
                this.password = null;
            }
            const data = `grant_type=password&scope=offline_access&username=${encodeURIComponent(username)}&password=${encodeURIComponent(password)}`;
            return this.sendAuthRequest(data).pipe(
                catchError(error => {
                    this.username = null;
                    this.password = null;
                    return of(error);
                })
            );
        }
        return of(null);
    }

    private sendAuthRequest(data: string): Observable<any> {
        const options = {
            headers: new HttpHeaders({
                'Content-Type': 'application/x-www-form-urlencoded'
            })
        };
        return this.http.post<any>(this.urls.getTokenUrl(), data, options).pipe(
            map(info => {
                if (info && info.access_token) {
                    return this.jwt.save(info);
                }
                return info;
            })
        );
    }
}
