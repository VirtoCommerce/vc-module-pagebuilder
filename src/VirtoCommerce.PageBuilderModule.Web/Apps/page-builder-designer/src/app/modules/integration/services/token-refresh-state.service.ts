import { Injectable } from '@angular/core';
import { Observable, finalize, shareReplay } from 'rxjs';

/**
 * Coordinates the token refresh between the requests that run into the expired token together.
 *
 * A route like /themes starts several requests at once, so the refresh has to be shared: every
 * waiting request gets the result of the very same call, is released only after that result is
 * ready, and is told about a failed refresh instead of waiting for a token that will never
 * arrive (VCST-5847).
 */
@Injectable({ providedIn: 'root' })
export class TokenRefreshStateService {

  private _pending: Observable<string> | null = null;

  /** The refresh that is currently running, if any. */
  get pending(): Observable<string> | null {
    return this._pending;
  }

  /** Starts `refresh`, or joins the one already running, and yields the fresh access token. */
  share(refresh: () => Observable<string>): Observable<string> {
    if (!this._pending) {
      this._pending = refresh().pipe(
        finalize(() => { this._pending = null; }),
        // refCount: false keeps the refresh going even when every waiting request is cancelled,
        // so the token still ends up in the storage
        shareReplay({ bufferSize: 1, refCount: false }),
      );
    }
    return this._pending;
  }
}
