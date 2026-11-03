import { Injectable, signal } from '@angular/core';

/**
 * Tracks whether the designer still has a usable platform session.
 *
 * The designer is opened in its own tab and reuses the admin bearer token, so an expired
 * token used to go unnoticed: every settings request failed silently and the designer kept
 * running with an empty configuration (VCST-5847). Services report the expiry here and the
 * shell reacts by asking the user to sign in again.
 */
@Injectable({
    providedIn: 'root'
})
export class SessionService {

    private readonly _expired = signal(false);

    readonly expired = this._expired.asReadonly();

    expire(): void {
        this._expired.set(true);
    }

    restore(): void {
        this._expired.set(false);
    }
}
