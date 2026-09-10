import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';

import { SharedComponentInsertModeComponent } from './shared-component-insert-mode.component';

describe('SharedComponentInsertModeComponent', () => {
    const dialogRef = { close: vi.fn() };

    beforeEach(() => {
        dialogRef.close.mockReset();
        TestBed.configureTestingModule({
            imports: [SharedComponentInsertModeComponent],
            providers: [
                { provide: MatDialogRef, useValue: dialogRef },
                {
                    provide: MAT_DIALOG_DATA,
                    useValue: { name: 'USP bar', defaultMode: 'shared', allowShared: true },
                },
            ],
        });
    });

    it('uses a labelled dialog and native radio inputs for both choices', () => {
        const fixture = TestBed.createComponent(SharedComponentInsertModeComponent);
        fixture.detectChanges();

        const choices = fixture.nativeElement.querySelectorAll('input[type="radio"]') as NodeListOf<HTMLInputElement>;
        expect(choices).toHaveLength(2);
        expect(choices[0].checked).toBe(true);
        expect(choices[0].name).toBe(choices[1].name);
        expect(fixture.nativeElement.querySelector('[mat-dialog-title]')).not.toBeNull();
        expect(fixture.nativeElement.textContent).toContain('Insert shared instance');
        expect(fixture.nativeElement.textContent).toContain('Create independent copy');
    });

    it('disables shared mode and defaults to an independent copy when linking is unavailable', () => {
        TestBed.overrideProvider(MAT_DIALOG_DATA, {
            useValue: {
                name: 'USP bar',
                defaultMode: 'shared',
                allowShared: false,
                sharedComponentDisabledReason: 'Shared instances are unavailable here.',
            },
        });
        const fixture = TestBed.createComponent(SharedComponentInsertModeComponent);
        fixture.detectChanges();

        const choices = fixture.nativeElement.querySelectorAll('input[type="radio"]') as NodeListOf<HTMLInputElement>;
        expect(choices[0].disabled).toBe(true);
        expect(choices[0].checked).toBe(false);
        expect(choices[1].checked).toBe(true);
        expect(fixture.nativeElement.textContent).toContain('Shared instances are unavailable here.');
    });

    it('returns the explicitly selected insertion mode', () => {
        const component = TestBed.createComponent(SharedComponentInsertModeComponent).componentInstance;

        component.select('copy');
        component.confirm();

        expect(dialogRef.close).toHaveBeenCalledWith({ accept: true, mode: 'copy' });
    });

    it('closes without an insertion mode when cancelled', () => {
        const component = TestBed.createComponent(SharedComponentInsertModeComponent).componentInstance;

        component.decline();

        expect(dialogRef.close).toHaveBeenCalledWith({ accept: false });
    });
});
