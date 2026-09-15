import { TestBed } from '@angular/core/testing';
import { MatDialogRef } from '@angular/material/dialog';

import { SharedComponentNameComponent } from './shared-component-name.component';

describe('SharedComponentNameComponent', () => {
    const dialogRef = { close: vi.fn() };

    beforeEach(() => {
        dialogRef.close.mockReset();
        TestBed.configureTestingModule({
            imports: [SharedComponentNameComponent],
            providers: [{ provide: MatDialogRef, useValue: dialogRef }],
        });
    });

    it('uses a Material dialog title as its accessible name', () => {
        const fixture = TestBed.createComponent(SharedComponentNameComponent);
        fixture.detectChanges();

        expect(fixture.nativeElement.querySelector('[mat-dialog-title]')?.textContent).toContain('Save as Shared Component');
    });

    it('rejects a whitespace-only name and exposes the validation message', () => {
        const fixture = TestBed.createComponent(SharedComponentNameComponent);
        fixture.componentInstance.name.setValue('   ');

        fixture.componentInstance.confirm();
        fixture.detectChanges();

        expect(fixture.componentInstance.name.invalid).toBe(true);
        expect(fixture.componentInstance.name.touched).toBe(true);
        expect(fixture.nativeElement.querySelector('#shared-component-name-error')).not.toBeNull();
        expect(dialogRef.close).not.toHaveBeenCalled();
    });

    it('returns a trimmed valid name', () => {
        const component = TestBed.createComponent(SharedComponentNameComponent).componentInstance;
        component.name.setValue('  USP bar  ');

        component.confirm();

        expect(dialogRef.close).toHaveBeenCalledWith({ accept: true, name: 'USP bar' });
    });
});
