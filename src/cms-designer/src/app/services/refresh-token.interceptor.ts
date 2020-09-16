import { JwtStorageService } from './jwt-storage.service';
import { Injectable } from '@angular/core';
import {
    HttpRequest,
    HttpHandler,
    HttpInterceptor,
    HttpSentEvent,
    HttpHeaderResponse,
    HttpProgressEvent,
    HttpResponse,
    HttpUserEvent,
    HttpErrorResponse
} from '@angular/common/http';
import { BehaviorSubject, Observable, throwError, of } from 'rxjs';
import { MatDialog } from '@angular/material/dialog';
import { LoginComponent } from '@app/components';
import { AuthService } from './auth.service';
import { catchError, switchMap, finalize, take, exhaustMap } from 'rxjs/operators';

@Injectable()
export class RefreshTokenInterceptor implements HttpInterceptor {

    constructor(private authService: AuthService, private jwt: JwtStorageService, private dialog: MatDialog) { }

    isRefreshingToken = false;
    tokenSubject: BehaviorSubject<string> = new BehaviorSubject<string>(null);

    intercept(request: HttpRequest<any>, next: HttpHandler)
        : Observable<HttpSentEvent | HttpHeaderResponse | HttpProgressEvent | HttpResponse<any> | HttpUserEvent<any> | any> {

        return next.handle(this.addTokenToRequest(request, this.jwt.getToken()))
            .pipe(
                catchError(err => {
                    if (err instanceof HttpErrorResponse) {
                        switch (err.status) {
                            case 401:
                                return this.handle401Error(request, next);
                            // case 400:
                            //     return <any>this.authService.logout();
                        }
                    }
                    return throwError(err);
                }));
    }

    private addTokenToRequest(request: HttpRequest<any>, token: string): HttpRequest<any> {
        if (this.isRefreshingToken) {
            return request;
        }
        return request.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
    }

    private handle401Error(request: HttpRequest<any>, next: HttpHandler) {

        if (!this.isRefreshingToken) {
            this.isRefreshingToken = true;

            // Reset here so that the following requests wait until the token
            // comes back from the refreshToken call.
            this.tokenSubject.next(null);

            return this.authService.refreshToken()
                .pipe(
                    switchMap((info: any) => {
                        if (info) {
                            this.tokenSubject.next(info.token);
                            return next.handle(this.addTokenToRequest(request, info.token));
                        }

                        return this.login(request, next);
                    }),
                    catchError(err => {
                        return of(null);
                    }),
                    finalize(() => {
                        this.isRefreshingToken = false;
                    })
                );
        } else {
            this.isRefreshingToken = false;

            return this.tokenSubject
                .pipe(take(1),
                    switchMap(token => {
                        if (!token) {
                            return this.login(request, next);
                        } else {
                            return next.handle(this.addTokenToRequest(request, token));
                        }
                    })
                );
        }
    }

    private login(request: HttpRequest<any>, next: HttpHandler): Observable<any> {
        return this.authService.loginIfSaved().pipe(
            exhaustMap(authInfo => {
                if (!authInfo) {
                    const dialogRef = this.dialog.open(LoginComponent, {
                        width: '680px',
                        height: '350px',
                        disableClose: true,
                        data: {
                            save: this.authService.hasSavedInfo()
                        }
                    });
                    return dialogRef.afterClosed().pipe(
                        switchMap(x =>
                            this.authService.login(x.data.username, x.data.password, x.data.save).pipe(
                                switchMap(response => {
                                    return next.handle(this.addTokenToRequest(request, response.token));
                                })
                            )
                        )
                    );
                } else {
                    return next.handle(this.addTokenToRequest(request, authInfo.token));
                }
            })
        );
    }
}
