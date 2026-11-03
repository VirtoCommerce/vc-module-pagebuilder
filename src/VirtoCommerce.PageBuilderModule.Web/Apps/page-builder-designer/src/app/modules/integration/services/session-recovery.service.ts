import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { Store } from '@ngrx/store';

import * as sharedActions from '@shared/store/actions';

import { AuthService } from './auth.service';
import { JwtStorageService } from './jwt-storage.service';
import { SessionService } from './session.service';
import { AppInitializator } from './app.initializator';

/**
 * Brings the designer back to life after the platform session behind it has expired.
 *
 * The designer runs in its own tab but shares the platform cookie session, so in the usual case
 * the bearer token is the only thing that is gone and it can be re-issued without involving the
 * user at all. Asking for a password is only the fallback - and not even an option when the
 * platform is configured for external (SSO) login, where the password grant is refused
 * (VCST-5847).
 */
@Injectable({ providedIn: 'root' })
export class SessionRecoveryService {

    private readonly auth = inject(AuthService);
    private readonly jwt = inject(JwtStorageService);
    private readonly session = inject(SessionService);
    private readonly initializator = inject(AppInitializator);
    private readonly store = inject(Store);

    private pending: Promise<boolean> | null = null;

    /**
     * Trades the browser cookie session for a fresh bearer token and reloads what the designer
     * could not load. Resolves to false when the cookie session is gone as well, which is the
     * only case where the user has to authenticate by hand.
     */
    tryRestoreSilently(): Promise<boolean> {
        if (!this.pending) {
            const attempt = this.obtainToken();
            this.pending = attempt;
            void attempt.finally(() => {
                if (this.pending === attempt) {
                    this.pending = null;
                }
            });
        }
        return this.pending;
    }

    /**
     * Reloads what the designer failed to load while the session was expired instead of reloading
     * the page: the settings are resolved again and `initShared` restarts the data chain, where
     * every step skips whatever is already in the store. Unsaved changes stay untouched.
     */
    async resume(): Promise<void> {
        this.session.restore();
        try {
            await this.initializator.init();
        } catch (error) {
            console.warn('Failed to reload the configuration after sign in:', error);
        }
        this.store.dispatch(sharedActions.initShared());
    }

    private async obtainToken(): Promise<boolean> {
        try {
            this.jwt.save(await firstValueFrom(this.auth.obtainToken()));
        } catch (error) {
            console.warn('Could not restore the session from the platform cookie session:', error);
            return false;
        }
        await this.resume();
        return true;
    }
}
