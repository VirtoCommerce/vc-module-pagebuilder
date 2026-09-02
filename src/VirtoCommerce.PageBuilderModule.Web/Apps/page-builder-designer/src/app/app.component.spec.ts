import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { provideMockStore } from '@ngrx/store/testing';
import { NEVER } from 'rxjs';

import * as sharedSelectors from '@shared/store/selectors';
import * as editorSelectors from '@editor/store/selectors';
import * as themeSelectors from '@theme/store/selectors';

import { SessionRecoveryService, SessionService } from '@integration/services';

import { AppComponent } from './app.component';

describe('AppComponent session recovery', () => {
    let session: SessionService;
    let recovery: { tryRestoreSilently: ReturnType<typeof vi.fn> };
    let dialogs: { open: ReturnType<typeof vi.fn> };
    let dialogRef: { afterClosed: () => typeof NEVER; close: ReturnType<typeof vi.fn> };

    beforeEach(() => {
        recovery = { tryRestoreSilently: vi.fn() };
        // the prompt cannot be dismissed, so it stays open for the whole test
        dialogRef = { afterClosed: () => NEVER, close: vi.fn() };
        dialogs = { open: vi.fn().mockReturnValue(dialogRef) };

        TestBed.configureTestingModule({
            providers: [
                provideMockStore({
                    selectors: [
                        { selector: sharedSelectors.isHttpLoading, value: false },
                        { selector: editorSelectors.isLoading, value: false },
                        { selector: themeSelectors.isLoading, value: false },
                    ]
                }),
                { provide: SessionRecoveryService, useValue: recovery },
                { provide: MatDialog, useValue: dialogs },
            ]
        });

        // the shell layout plays no part in the recovery
        TestBed.overrideComponent(AppComponent, { set: { template: '', imports: [] } });

        session = TestBed.inject(SessionService);
    });

    it('keeps the sign-in prompt away when the cookie session still yields a token', async () => {
        recovery.tryRestoreSilently.mockImplementation(async () => {
            session.restore();
            return true;
        });

        const fixture = TestBed.createComponent(AppComponent);
        fixture.detectChanges();

        session.expire();
        fixture.detectChanges();
        await Promise.resolve();

        expect(recovery.tryRestoreSilently).toHaveBeenCalled();
        expect(dialogs.open).not.toHaveBeenCalled();
    });

    it('asks the user to sign in once the cookie session is gone as well', async () => {
        recovery.tryRestoreSilently.mockResolvedValue(false);

        const fixture = TestBed.createComponent(AppComponent);
        fixture.detectChanges();

        session.expire();
        fixture.detectChanges();
        await Promise.resolve();

        expect(dialogs.open).toHaveBeenCalled();
    });

    it('takes the prompt down again when the session comes back on its own', async () => {
        recovery.tryRestoreSilently.mockResolvedValue(false);

        const fixture = TestBed.createComponent(AppComponent);
        fixture.detectChanges();

        session.expire();
        fixture.detectChanges();
        await Promise.resolve();
        expect(dialogs.open).toHaveBeenCalled();

        // a background token refresh succeeded, so there is nothing left to ask for
        session.restore();
        fixture.detectChanges();

        expect(dialogRef.close).toHaveBeenCalled();
    });

    it('still asks when the session goes again while the designer catches up', async () => {
        // the silent attempt reports success, but something expired the session again right after
        recovery.tryRestoreSilently.mockResolvedValue(true);

        const fixture = TestBed.createComponent(AppComponent);
        fixture.detectChanges();

        session.expire();
        fixture.detectChanges();
        await Promise.resolve();

        expect(dialogs.open).toHaveBeenCalled();
    });
});
