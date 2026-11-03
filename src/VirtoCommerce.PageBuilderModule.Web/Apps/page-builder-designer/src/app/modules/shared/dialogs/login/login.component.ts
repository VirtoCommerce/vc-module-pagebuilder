import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatDialogRef } from '@angular/material/dialog';

import { IconComponent } from '@core/components/icon/icon.component';
import { IconButtonComponent } from '@core/components/icon-button/icon-button.component';
import { LogoComponent } from '@core/components/logo/logo.component';

import { EnvironmentRef, JwtStorageService, SessionRecoveryService } from '@integration/services';
import { AuthService } from '@integration/services/auth.service';

/**
 * Sign-in prompt shown when the platform session behind the designer has expired and could not be
 * restored from the browser cookie session.
 *
 * The designer is opened in a separate tab, so sending the user back to the admin login page
 * would throw away everything that has not been saved yet. Signing in here refreshes the token
 * in place and replays the data the designer could not load (VCST-5847).
 *
 * The password form only works where the platform accepts the password grant. With external (SSO)
 * login the user signs in on the platform in another tab and comes back here, which is what the
 * second half of the dialog is for.
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
    private readonly recovery = inject(SessionRecoveryService);
    private readonly env = inject(EnvironmentRef);

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
                void this.restoreApplication();
            },
            error: error => {
                this.busy.set(false);
                this.error.set(describeLoginError(error));
            }
        });
    }

    /** Opens the platform sign-in page, whatever it is configured to be, in a separate tab. */
    openPlatformSignIn() {
        this.env.nativeWindow.open(this.env.nativeWindow.location.origin, '_blank', 'noopener');
    }

    /** Picks up the session the user has just established on the platform in another tab. */
    continueWithPlatformSession() {
        this.busy.set(true);
        this.error.set(null);
        void this.recovery.tryRestoreSilently().then(restored => {
            this.busy.set(false);
            if (restored) {
                this.dialogRef.close(true);
            } else {
                this.error.set('No active platform session found. Sign in on the platform and try again.');
            }
        });
    }

    private async restoreApplication() {
        await this.recovery.resume();
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
