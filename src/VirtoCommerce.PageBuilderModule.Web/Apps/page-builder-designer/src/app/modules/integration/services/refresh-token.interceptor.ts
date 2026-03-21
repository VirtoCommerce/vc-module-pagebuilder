import { Injectable, inject } from '@angular/core';
import { HttpInterceptor, HttpRequest, HttpHandler, HttpErrorResponse } from '@angular/common/http';
import { Store } from '@ngrx/store';
import { Subject, Observable, throwError, of } from 'rxjs';
import { catchError, map, switchMap, tap } from 'rxjs/operators';
import { JwtStorageService } from './jwt-storage.service';

import { AuthService } from './auth.service';

@Injectable({
    providedIn: 'root'
})
export class RefreshTokenInterceptor implements HttpInterceptor {

    private readonly jwt = inject(JwtStorageService);
    private readonly auth = inject(AuthService);
    private readonly store = inject(Store);

    private refreshTokenInProgress = false;

    private readonly tokenRefreshedSource = new Subject<any>();
    private readonly tokenRefreshed$ = this.tokenRefreshedSource.asObservable();

    intercept(request: HttpRequest<any>, next: HttpHandler): Observable<any> {
        return this.addAuthData(request).pipe(
            switchMap(req => next.handle(req).pipe(
                catchError(err => throwError(() => err))
            ))
        );
    }

    private addAuthData(request: HttpRequest<any>): Observable<HttpRequest<any>> {
        if (request.headers.get('x-refresh') !== 'true') {
            if (this.refreshTokenInProgress) {
                return new Observable(observer => {
                    this.tokenRefreshed$.subscribe(() => {
                        const auth = this.jwt.getInfo();
                        const updatedRequest = request.clone({
                            setHeaders: {
                                Authorization: `Bearer ${auth.token}`
                            }
                        });
                        observer.next(updatedRequest);
                        observer.complete();
                    });
                });
            } else {
                const auth = this.jwt.getInfo();
                if (auth && auth.expiresAt && Date.now() < auth.expiresAt) {
                    const result = this.enrichRequest(request, auth.token);
                    return of(result);
                }
                if (auth && auth.refreshToken) {
                    this.refreshTokenInProgress = true;
                    return this.auth.refreshToken(auth.refreshToken).pipe(
                        tap(() => {
                            this.refreshTokenInProgress = false;
                            this.tokenRefreshedSource.next({});
                        }),
                        map(response => this.jwt.save(response)),
                        map(response => this.enrichRequest(request, response.token)),
                        catchError((e) => {
                            this.refreshTokenInProgress = false;
                            return throwError(() => e);
                        })
                    );
                }
            }
        }
        return of(request);
    }

    private enrichRequest(request: HttpRequest<any>, token: string): HttpRequest<any> {
        // todo: we need add header to local only requests
        return request.clone({
            headers: request.headers.set('Authorization', 'Bearer ' + token)
        });
    }
}
