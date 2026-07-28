import { TestBed } from '@angular/core/testing';
import { MatDialogRef } from '@angular/material/dialog';

import { LinkedComponentNameComponent } from './linked-component-name.component';

describe('LinkedComponentNameComponent', () => {
    const dialogRef = { close: vi.fn() };

    beforeEach(() => {
        dialogRef.close.mockReset();
        TestBed.configureTestingModule({
            imports: [LinkedComponentNameComponent],
            providers: [{ provide: MatDialogRef, useValue: dialogRef }],
        });
    });

    it('uses a Material dialog title as its accessible name', () => {
        const fixture = TestBed.createComponent(LinkedComponentNameComponent);
        fixture.detectChanges();

        expect(fixture.nativeElement.querySelector('[mat-dialog-title]')?.textContent).toContain('Save as Shared Component');
    });

    it('rejects a whitespace-only name and exposes the validation message', () => {
        const fixture = TestBed.createComponent(LinkedComponentNameComponent);
        fixture.componentInstance.name.setValue('   ');

        fixture.componentInstance.confirm();
        fixture.detectChanges();

        expect(fixture.componentInstance.name.invalid).toBe(true);
        expect(fixture.componentInstance.name.touched).toBe(true);
        expect(fixture.nativeElement.querySelector('#linked-component-name-error')).not.toBeNull();
        expect(dialogRef.close).not.toHaveBeenCalled();
    });

    it('returns a trimmed valid name', () => {
        const component = TestBed.createComponent(LinkedComponentNameComponent).componentInstance;
        component.name.setValue('  USP bar  ');

        component.confirm();

        expect(dialogRef.close).toHaveBeenCalledWith({ accept: true, name: 'USP bar' });
    });
});
