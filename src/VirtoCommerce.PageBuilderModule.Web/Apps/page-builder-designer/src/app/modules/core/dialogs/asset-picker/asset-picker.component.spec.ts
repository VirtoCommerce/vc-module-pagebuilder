import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { of, Subject } from 'rxjs';

import { AssetLibraryEntry, AssetLibraryService, AssetLibraryUploadCoordinatorService } from '@core/services';
import { createAssetLibraryMock } from '@app/testing/asset-library';
import { AssetPickerComponent } from './asset-picker.component';

describe('AssetPickerComponent', () => {
    const root = '/stores/store/Page Builder';
    const images: AssetLibraryEntry[] = ['one.jpg', 'two.jpg'].map(name => ({
        type: 'blob', name, relativeUrl: `${root}/${name}`, contentType: 'image/jpeg',
    }));

    function setup(multiple = true, capabilities: Partial<AssetLibraryService> = {}) {
        const created = new Subject<void>();
        const uploaded = new Subject<AssetLibraryEntry[]>();
        const dialog = { close: vi.fn() };
        TestBed.configureTestingModule({
            imports: [AssetPickerComponent],
            providers: [
                { provide: MAT_DIALOG_DATA, useValue: { rootFolderUrl: root, multiple } },
                { provide: MatDialogRef, useValue: dialog },
                { provide: AssetLibraryService, useValue: createAssetLibraryMock({
                    search: () => of({ results: images, totalCount: images.length }),
                    createFolder: () => created,
                    ...capabilities,
                }) },
                { provide: AssetLibraryUploadCoordinatorService, useValue: { uploadFiles: () => uploaded } },
            ],
        });
        const fixture = TestBed.createComponent(AssetPickerComponent);
        return { fixture, state: fixture.componentInstance.state, created, uploaded, dialog };
    }

    it('submits the form, retains both selections and keeps the picker open', async () => {
        const { fixture, state, created, dialog } = setup();
        await fixture.whenStable();
        const cards = fixture.nativeElement.querySelectorAll('.card') as NodeListOf<HTMLButtonElement>;
        cards[0].click();
        cards[1].click();
        state.openFolderForm();
        await fixture.whenStable();
        const input = fixture.nativeElement.querySelector('.folder-form input') as HTMLInputElement;
        input.value = 'new folder';
        input.dispatchEvent(new Event('input'));
        await fixture.whenStable();
        const submitFolder = vi.spyOn(state, 'createFolder');
        fixture.nativeElement.querySelector('.folder-form').dispatchEvent(new Event('submit', { cancelable: true }));
        await fixture.whenStable();
        expect(state.creatingFolder()).toBe(true);
        expect(fixture.nativeElement.querySelector('.folder-form input').readOnly).toBe(true);
        expect(fixture.nativeElement.querySelector('[mat-dialog-actions] .btn-skin-primary').disabled).toBe(true);
        created.next();
        await submitFolder.mock.results[0].value;
        await fixture.whenStable();
        expect(state.currentFolderUrl()).toBe(`${root}/new folder`);
        expect(state.selectedCount()).toBe(2);
        expect(fixture.nativeElement.querySelector('.folder-form')).toBeNull();
        expect(dialog.close).not.toHaveBeenCalled();
        fixture.nativeElement.querySelector('[mat-dialog-actions] .btn-skin-primary').click();
        expect(dialog.close).toHaveBeenCalledWith(images.map(entry => ({
            entry, url: `/assets${entry.relativeUrl}`, previewUrl: `/assets${entry.relativeUrl}`,
        })));
    });

    it('displays lowercase validation accessibly and rejects form submission', async () => {
        const { fixture, state } = setup();
        state.openFolderForm();
        state.folderName.set('UPPER');
        await fixture.whenStable();
        const input = fixture.nativeElement.querySelector('.folder-form input') as HTMLInputElement;
        expect(input.getAttribute('aria-invalid')).toBe('true');
        expect(fixture.nativeElement.querySelector('.folder-form .btn-skin-primary').disabled).toBe(true);
        expect(input.getAttribute('aria-describedby')).toBe('asset-picker-folder-error');
        expect(fixture.nativeElement.querySelector('[role="alert"]').textContent).toContain('lowercase');
        fixture.nativeElement.querySelector('.folder-form').dispatchEvent(new Event('submit', { cancelable: true }));
        expect(state.creatingFolder()).toBe(false);
    });

    it('shows server errors but keeps Create enabled for retry', async () => {
        const { fixture, state, created } = setup();
        state.openFolderForm();
        state.folderName.set('new folder');
        state.createFolder();
        created.error(new HttpErrorResponse({ status: 400, error: { message: 'Folder already exists.' } }));
        await fixture.whenStable();
        expect(fixture.nativeElement.querySelector('[role="alert"]').textContent).toBe('Folder already exists.');
        expect(fixture.nativeElement.querySelector('.folder-form .btn-skin-primary').disabled).toBe(false);
    });

    it('cancels only the form without discarding selection', async () => {
        const { fixture, state, dialog } = setup();
        state.selectAsset(images[0]);
        state.selectAsset(images[1]);
        state.openFolderForm();
        await fixture.whenStable();
        fixture.nativeElement.querySelector('.folder-form [type="button"]').click();
        await fixture.whenStable();
        expect(fixture.nativeElement.querySelector('.folder-form')).toBeNull();
        expect(state.selectedAssets()).toEqual(images);
        expect(dialog.close).not.toHaveBeenCalled();
    });

    it('opens the folder form from the toolbar and preserves the draft on repeated clicks', async () => {
        const { fixture, state } = setup();
        await fixture.whenStable();
        const newFolder = Array.from(fixture.nativeElement.querySelectorAll('.toolbar button') as NodeListOf<HTMLButtonElement>)
            .find(button => button.textContent?.includes('New folder'))!;
        newFolder.click();
        await fixture.whenStable();
        const input = fixture.nativeElement.querySelector('.folder-form input') as HTMLInputElement;
        input.value = 'my folder';
        input.dispatchEvent(new Event('input'));
        await fixture.whenStable();
        newFolder.click();
        await fixture.whenStable();

        expect(input.value).toBe('my folder');
        expect(state.folderName()).toBe('my folder');
        fixture.nativeElement.querySelector('.folder-form [type="button"]').click();
        await fixture.whenStable();
        newFolder.click();
        await fixture.whenStable();
        expect(state.folderName()).toBe('');
        expect(fixture.nativeElement.querySelector('.folder-form input').value).toBe('');
    });

    it('confirms a single asset after folder creation and the subsequent upload finish', async () => {
        const { fixture, state, created, uploaded, dialog } = setup(false);
        state.openFolderForm();
        state.folderName.set('new folder');
        state.createFolder();
        created.next();
        await fixture.whenStable();
        state.uploadFiles([new File(['image'], 'new.jpg', { type: 'image/jpeg' })]);
        state.selectAsset(images[1]);
        await fixture.whenStable();
        expect(fixture.nativeElement.querySelector('[mat-dialog-actions] .btn-skin-primary').disabled).toBe(true);
        expect(fixture.nativeElement.querySelector('.toolbar button:last-of-type').disabled).toBe(true);

        uploaded.next([images[1]]);
        await fixture.whenStable();
        fixture.nativeElement.querySelector('.card--selected').dispatchEvent(new MouseEvent('dblclick'));

        expect(dialog.close).toHaveBeenCalledExactlyOnceWith({
            entry: images[1], url: `/assets${images[1].relativeUrl}`, previewUrl: `/assets${images[1].relativeUrl}`,
        });
    });

    it.each(['folder', 'upload'])('blocks single-image double-click confirmation during %s mutation', async mutation => {
        const { fixture, state, dialog } = setup(false);
        state.selectAsset(images[0]);
        if (mutation === 'folder') {
            state.folderName.set('new folder');
            state.createFolder();
        } else {
            state.uploadFiles([new File(['image'], 'new.jpg', { type: 'image/jpeg' })]);
        }
        await fixture.whenStable();
        fixture.nativeElement.querySelector('.card').dispatchEvent(new MouseEvent('dblclick'));
        expect(dialog.close).not.toHaveBeenCalled();
        expect(fixture.nativeElement.querySelector('[mat-dialog-actions] .btn-skin-primary').disabled).toBe(true);
    });
    it('keeps Escape inside the form and returns focus without losing the selection', async () => {
        const { fixture, state, dialog } = setup();
        state.selectAsset(images[0]);
        state.selectAsset(images[1]);
        await fixture.whenStable();
        const newFolder = fixture.nativeElement.querySelector('.toolbar button:last-of-type') as HTMLButtonElement;
        newFolder.click();
        await fixture.whenStable();
        const input = fixture.nativeElement.querySelector('.folder-form input') as HTMLInputElement;
        expect(document.activeElement).toBe(input);
        const escaped = vi.fn();
        fixture.nativeElement.addEventListener('keydown', escaped);
        input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true }));
        await fixture.whenStable();
        expect(escaped).not.toHaveBeenCalled();
        expect(state.folderFormOpen()).toBe(false);
        expect(document.activeElement).toBe(newFolder);
        expect(state.selectedAssets()).toEqual(images);
        expect(dialog.close).not.toHaveBeenCalled();
    });

    it('retains input focus and ignores Escape while a folder request is pending', async () => {
        const { fixture, state, created, dialog } = setup();
        state.openFolderForm();
        state.folderName.set('new folder');
        await fixture.whenStable();
        const input = fixture.nativeElement.querySelector('.folder-form input') as HTMLInputElement;
        const submission = state.createFolder();
        await fixture.whenStable();
        expect(input.readOnly).toBe(true);
        expect(input.disabled).toBe(false);
        expect(document.activeElement).toBe(input);
        input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true }));
        await fixture.whenStable();
        expect(state.folderFormOpen()).toBe(true);
        expect(dialog.close).not.toHaveBeenCalled();
        created.next();
        await submission;
        await fixture.whenStable();
        expect(document.activeElement).toBe(fixture.nativeElement.querySelector('.toolbar button:last-of-type'));
    });

    it('hides unavailable mutation actions and blocks their state entry points', async () => {
        const createFolder = vi.fn();
        const { fixture, state } = setup(true, { canCreateFolder: () => false, canUpload: () => false, createFolder });
        await fixture.whenStable();
        expect(fixture.nativeElement.querySelectorAll('.toolbar button')).toHaveLength(0);
        state.openFolderForm();
        state.folderName.set('new folder');
        state.createFolder();
        state.uploadFiles([new File(['image'], 'new.jpg', { type: 'image/jpeg' })]);
        expect(state.folderFormOpen()).toBe(false);
        expect(state.uploading()).toBe(false);
        expect(createFolder).not.toHaveBeenCalled();
    });

});
