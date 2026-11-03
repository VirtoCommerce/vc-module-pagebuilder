import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { Store } from '@ngrx/store';

import * as sharedActions from '@shared/store/actions';

import { AuthService } from './auth.service';
import { JwtStorageService } from './jwt-storage.service';
import { SessionService } from './session.service';
import { AppInitializator } from './app.initializator';
import { SessionRecoveryService } from './session-recovery.service';

const STORAGE_KEY = 'ls.authenticationData';

describe('SessionRecoveryService', () => {
    let recovery: SessionRecoveryService;
    let session: SessionService;
    let auth: { obtainToken: ReturnType<typeof vi.fn> };
    let initializator: { init: ReturnType<typeof vi.fn> };
    let store: { dispatch: ReturnType<typeof vi.fn> };

    beforeEach(() => {
        auth = { obtainToken: vi.fn().mockReturnValue(of({ access_token: 'cookie-token', expires_in: 3600 })) };
        initializator = { init: vi.fn().mockResolvedValue({}) };
        store = { dispatch: vi.fn() };

        TestBed.configureTestingModule({
            providers: [
                { provide: AuthService, useValue: auth },
                { provide: AppInitializator, useValue: initializator },
                { provide: Store, useValue: store },
                JwtStorageService,
                SessionService,
                SessionRecoveryService
            ]
        });

        recovery = TestBed.inject(SessionRecoveryService);
        session = TestBed.inject(SessionService);
        session.expire();
    });

    afterEach(() => {
        localStorage.removeItem(STORAGE_KEY);
    });

    it('trades the cookie session for a token without involving the user', async () => {
        const restored = await recovery.tryRestoreSilently();

        expect(restored).toBe(true);
        expect(TestBed.inject(JwtStorageService).getInfo().token).toBe('cookie-token');
        expect(session.expired()).toBe(false);
        // whatever could not be loaded while the session was gone is asked for again
        expect(initializator.init).toHaveBeenCalled();
        expect(store.dispatch).toHaveBeenCalledWith(sharedActions.initShared());
    });

    it('reports a cookie session that is gone as well, so the user can be asked to sign in', async () => {
        auth.obtainToken.mockReturnValue(throwError(() => ({ status: 401 })));

        const restored = await recovery.tryRestoreSilently();

        expect(restored).toBe(false);
        expect(session.expired()).toBe(true);
        expect(initializator.init).not.toHaveBeenCalled();
    });

    it('runs a single attempt for everything that asks at once', async () => {
        const [first, second] = await Promise.all([recovery.tryRestoreSilently(), recovery.tryRestoreSilently()]);

        expect([first, second]).toEqual([true, true]);
        expect(auth.obtainToken).toHaveBeenCalledTimes(1);
    });

    it('tries again after a failed attempt', async () => {
        auth.obtainToken.mockReturnValueOnce(throwError(() => ({ status: 401 })));

        expect(await recovery.tryRestoreSilently()).toBe(false);
        expect(await recovery.tryRestoreSilently()).toBe(true);
        expect(auth.obtainToken).toHaveBeenCalledTimes(2);
    });
});
