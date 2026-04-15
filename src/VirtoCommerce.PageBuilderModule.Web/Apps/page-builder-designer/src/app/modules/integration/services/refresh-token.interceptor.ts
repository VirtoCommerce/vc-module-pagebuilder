import { inject } from '@angular/core';
import { HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { Observable, throwError, of } from 'rxjs';
import { catchError, map, switchMap, tap } from 'rxjs/operators';
import { JwtStorageService } from './jwt-storage.service';
import { AuthService } from './auth.service';
import { TokenRefreshStateService } from './token-refresh-state.service';

function addAuthData(
  request: HttpRequest<unknown>,
  jwt: JwtStorageService,
  auth: AuthService,
  state: TokenRefreshStateService,
): Observable<HttpRequest<unknown>> {
  if (request.headers.get('x-refresh') === 'true') {
    return of(request);
  }

  if (state.inProgress) {
    return new Observable(observer => {
      const sub = state.refreshed$.subscribe(() => {
        const refreshedInfo = jwt.getInfo();
        observer.next(request.clone({ setHeaders: { Authorization: `Bearer ${refreshedInfo.token}` } }));
        observer.complete();
      });
      return () => sub.unsubscribe();
    });
  }

  const info = jwt.getInfo();

  if (info?.expiresAt && Date.now() < info.expiresAt) {
    return of(enrichRequest(request, info.token));
  }

  if (info?.refreshToken) {
    state.start();
    return auth.refreshToken(info.refreshToken).pipe(
      tap(() => state.complete()),
      map(response => jwt.save(response)),
      map(response => enrichRequest(request, response.token)),
      catchError(e => {
        state.fail();
        return throwError(() => e);
      }),
    );
  }

  return of(request);
}

function enrichRequest(request: HttpRequest<unknown>, token: string): HttpRequest<unknown> {
  // todo: we need add header to local only requests
  return request.clone({ headers: request.headers.set('Authorization', 'Bearer ' + token) });
}

export const refreshTokenInterceptor: HttpInterceptorFn = (req, next) => {
  const jwt = inject(JwtStorageService);
  const auth = inject(AuthService);
  const state = inject(TokenRefreshStateService);

  return addAuthData(req, jwt, auth, state).pipe(
    switchMap(enrichedReq => next(enrichedReq).pipe(
      catchError(err => throwError(() => err)),
    )),
  );
};
