import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';

import { AlertComponent } from './alert.component';

describe('AlertComponent', () => {
    const close = vi.fn();

    beforeEach(() => {
        close.mockReset();
        TestBed.configureTestingModule({
            imports: [AlertComponent],
            providers: [
                { provide: MatDialogRef, useValue: { close } },
                { provide: MAT_DIALOG_DATA, useValue: { title: 'The file could not be uploaded.' } },
            ],
        });
    });

    it('renders the message and a standard primary action that closes the alert', async () => {
        const fixture = TestBed.createComponent(AlertComponent);
        await fixture.whenStable();

        const element: HTMLElement = fixture.nativeElement;
        expect(element.querySelector('mat-dialog-content')?.textContent).toContain('The file could not be uploaded.');
        const button = element.querySelector<HTMLButtonElement>('app-icon-button button');
        expect(button?.textContent).toContain('OK');
        expect(button?.classList.contains('btn-skin-primary')).toBe(true);

        button!.click();
        await fixture.whenStable();
        expect(close).toHaveBeenCalledOnce();
    });

    it('preserves a caller-provided confirmation label', async () => {
        TestBed.overrideProvider(MAT_DIALOG_DATA, { useValue: { title: 'Message', confirmText: 'Понятно' } });
        const fixture = TestBed.createComponent(AlertComponent);
        await fixture.whenStable();

        expect(fixture.nativeElement.querySelector('button').textContent).toContain('Понятно');
    });
});
