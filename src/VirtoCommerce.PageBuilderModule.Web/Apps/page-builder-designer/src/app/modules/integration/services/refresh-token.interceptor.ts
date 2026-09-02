import { inject } from '@angular/core';
import { HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { Observable, throwError, of } from 'rxjs';
import { catchError, map, switchMap } from 'rxjs/operators';
import { JwtStorageService } from './jwt-storage.service';
import { AuthService } from './auth.service';
import { TokenRefreshStateService } from './token-refresh-state.service';
import { SessionService } from './session.service';

function isAuthRequest(request: HttpRequest<unknown>): boolean {
  return request.headers.get('x-refresh') === 'true';
}

function addAuthData(
  request: HttpRequest<unknown>,
  jwt: JwtStorageService,
  auth: AuthService,
  state: TokenRefreshStateService,
  session: SessionService,
): Observable<HttpRequest<unknown>> {
  if (isAuthRequest(request)) {
    return of(request);
  }

  // a refresh started by another request is about to produce a token for this one as well
  const pending = state.pending;
  if (pending) {
    return pending.pipe(map(token => enrichRequest(request, token)));
  }

  const info = jwt.getInfo();

  if (info?.expiresAt && Date.now() < info.expiresAt) {
    return of(enrichRequest(request, info.token));
  }

  if (info?.refreshToken) {
    return refreshAccessToken(info.refreshToken, jwt, auth, state, session)
      .pipe(map(token => enrichRequest(request, token)));
  }

  return of(request);
}

function refreshAccessToken(
  refreshToken: string,
  jwt: JwtStorageService,
  auth: AuthService,
  state: TokenRefreshStateService,
  session: SessionService,
): Observable<string> {
  return state.share(() => auth.refreshToken(refreshToken).pipe(
    // the token has to reach the storage before any waiting request is released, otherwise they
    // are sent with the expired one and fail with 401 right after a successful refresh (VCST-5847)
    map(response => jwt.save(response).token),
    catchError(error => {
      session.expire();
      return throwError(() => error);
    }),
  ));
}

function enrichRequest(request: HttpRequest<unknown>, token: string): HttpRequest<unknown> {
  // todo: we need add header to local only requests
  return request.clone({ headers: request.headers.set('Authorization', 'Bearer ' + token) });
}

export const refreshTokenInterceptor: HttpInterceptorFn = (req, next) => {
  const jwt = inject(JwtStorageService);
  const auth = inject(AuthService);
  const state = inject(TokenRefreshStateService);
  const session = inject(SessionService);

  return addAuthData(req, jwt, auth, state, session).pipe(
    switchMap(enrichedReq => next(enrichedReq).pipe(
      catchError(err => {
        // Requests are fired with no token at all when nothing is stored, and the stored token
        // may be rejected before it formally expires. Both surface as 401 and mean the same
        // thing for the designer: the session is gone and the user has to sign in again.
        if (err?.status === 401 && !isAuthRequest(enrichedReq)) {
          session.expire();
        }
        return throwError(() => err);
      }),
    )),
  );
};
