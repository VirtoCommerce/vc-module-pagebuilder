import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatDialogRef } from '@angular/material/dialog';
import { Store } from '@ngrx/store';

import { IconComponent } from '@core/components/icon/icon.component';
import { IconButtonComponent } from '@core/components/icon-button/icon-button.component';
import { LogoComponent } from '@core/components/logo/logo.component';

import { JwtStorageService, SessionService } from '@integration/services';
import { AuthService } from '@integration/services/auth.service';
import { AppInitializator } from '@integration/services/app.initializator';

import { BuilderState } from '@shared/store';
import * as actions from '@shared/store/actions';

/**
 * Sign-in prompt shown when the platform session behind the designer has expired.
 *
 * The designer is opened in a separate tab, so sending the user back to the admin login page
 * would throw away everything that has not been saved yet. Signing in here refreshes the token
 * in place and replays the data the designer could not load (VCST-5847).
 */
@Component({
    selector: 'app-login',
    templateUrl: './login.component.html',
    styleUrls: ['./login.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [IconComponent, IconButtonComponent, LogoComponent]
})
export class LoginComponent {

    private readonly dialogRef = inject(MatDialogRef<LoginComponent>);
    private readonly auth = inject(AuthService);
    private readonly jwt = inject(JwtStorageService);
    private readonly session = inject(SessionService);
    private readonly initializator = inject(AppInitializator);
    private readonly store = inject(Store<BuilderState>);

    readonly userName = signal('');
    readonly password = signal('');
    readonly busy = signal(false);
    readonly error = signal<string | null>(null);
    readonly passwordVisible = signal(false);

    readonly canSubmit = computed(() => !this.busy() && !!this.userName().trim() && !!this.password());

    togglePassword() {
        this.passwordVisible.update(visible => !visible);
    }

    onSubmit(event: Event) {
        event.preventDefault();
        this.signIn();
    }

    signIn() {
        if (!this.canSubmit()) {
            return;
        }
        const userName = this.userName().trim();
        this.busy.set(true);
        this.error.set(null);
        this.auth.login(userName, this.password()).subscribe({
            next: response => {
                // the password grant does not return the user name, the platform admin stores the entered one as well
                this.jwt.save({ ...response, userName });
                this.session.restore();
                void this.restoreApplication();
            },
            error: error => {
                this.busy.set(false);
                this.error.set(describeLoginError(error));
            }
        });
    }

    /**
     * Reloads what the designer failed to load while the session was expired instead of reloading
     * the page: the settings are resolved again and `initShared` restarts the data chain, where
     * every step skips whatever is already in the store. Unsaved changes stay untouched.
     */
    private async restoreApplication() {
        try {
            await this.initializator.init();
        } catch (error) {
            console.warn('Failed to reload the configuration after sign in:', error);
        }
        this.store.dispatch(actions.initShared());
        this.busy.set(false);
        this.dialogRef.close(true);
    }
}

function describeLoginError(error: any): string {
    const description = error?.error?.error_description;
    if (description) {
        return description;
    }
    if (error?.status === 400 || error?.status === 401) {
        return 'Invalid user name or password.';
    }
    return 'Could not sign in. Please try again.';
}
