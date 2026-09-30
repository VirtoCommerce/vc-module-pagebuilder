import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Subject } from 'rxjs';

import { AssetLibraryService } from '@core/services/asset-library.service';
import { AssetsService } from '@core/services/assets.service';
import { ClipboardService } from '@core/services/clipboard.service';
import { ModalService } from '@core/services/modal.service';
import { AssetLibraryUploadCoordinatorService } from '@core/services/asset-library-upload-coordinator.service';
import { AssetPickerComponent, AssetPickerDialogResult } from '@core/dialogs/asset-picker/asset-picker.component';
import { FilesDescriptor } from '@models/controls';
import { FilesComponent } from './files/files.component';
import { ImagesComponent } from './images/images.component';

describe.each([
    { name: 'Images', component: ImagesComponent, type: 'images', extension: 'jpg', accept: ['image/*'] },
    { name: 'Files', component: FilesComponent, type: 'files', extension: 'pdf', accept: [] },
])('$name Asset Library contract', ({ component: componentType, type, extension, accept }) => {
    const urls = [`/assets/stores/store/Page Builder/one.${extension}`, `/assets/stores/store/Page Builder/two.${extension}`];
    const results = urls.map(url => ({
        entry: { type: 'blob' as const, name: url.slice(url.lastIndexOf('/') + 1), url },
        url, previewUrl: url,
    }));

    async function setup(multiple = true, descriptorOverrides: Partial<FilesDescriptor> = {}, initialValue: unknown = multiple ? ['/existing.jpg'] : '/existing.jpg') {
        const closed = new Subject<AssetPickerDialogResult | null>();
        const modals = { show: vi.fn(() => closed) };
        const uploads = { uploadFiles: vi.fn() };
        TestBed.configureTestingModule({
            imports: [componentType],
            providers: [
                { provide: ModalService, useValue: modals },
                { provide: AssetsService, useValue: { getPreviewUrl: () => null, isInlineUpload: () => false } },
                { provide: ClipboardService, useValue: {} },
                { provide: AssetLibraryService, useValue: {
                    getLabels: () => AssetLibraryService.prototype.getLabels(),
                    getRootFolderUrl: () => '/stores/store/Page Builder',
                } },
                { provide: AssetLibraryUploadCoordinatorService, useValue: uploads },
            ],
        });
        const fixture: ComponentFixture<FilesComponent | ImagesComponent> = TestBed.createComponent<FilesComponent | ImagesComponent>(componentType);
        fixture.componentRef.setInput('descriptor', { id: 'media', type, element: [], multiple, ...descriptorOverrides });
        fixture.componentRef.setInput('context', {});
        fixture.componentRef.setInput('controlValue', initialValue);
        await fixture.whenStable();
        const valueChanged = vi.fn();
        fixture.componentInstance.valueChanged.subscribe(valueChanged);
        return { fixture, closed, modals, uploads, valueChanged };
    }

    it('passes multiple to the picker and applies the entire set without uploading copies', async () => {
        const { fixture, closed, modals, uploads, valueChanged } = await setup();
        (fixture.nativeElement.querySelector('.choose-from-library button') as HTMLButtonElement).click();
        expect(modals.show).toHaveBeenCalledWith(AssetPickerComponent, expect.objectContaining({
            data: expect.objectContaining({ multiple: true, rootFolderUrl: '/stores/store/Page Builder', accept }),
        }));
        closed.next(results);
        expect(valueChanged).toHaveBeenCalledExactlyOnceWith(['/existing.jpg', ...urls]);
        expect(uploads.uploadFiles).not.toHaveBeenCalled();
    });

    it('passes single selection to the picker and replaces the previous scalar value', async () => {
        const { fixture, closed, modals, valueChanged } = await setup(false);
        fixture.componentInstance.chooseFromLibrary();
        expect(modals.show).toHaveBeenCalledWith(AssetPickerComponent, expect.objectContaining({
            data: expect.objectContaining({ multiple: false }),
        }));
        closed.next(results[1]);
        expect(valueChanged).toHaveBeenCalledExactlyOnceWith(urls[1]);
        expect(fixture.componentInstance.innerValue()).toHaveLength(1);
    });

    it('maps every asset to the configured object fields and preserves element defaults', async () => {
        const { fixture, closed, valueChanged } = await setup(true, {
            urlField: 'src', filenameField: 'name',
            element: [{ id: 'alt', type: 'text', default: 'default alt' }],
        }, []);
        fixture.componentInstance.chooseFromLibrary();
        closed.next(results);
        expect(valueChanged).toHaveBeenCalledExactlyOnceWith(results.map(result => ({
            src: result.url, name: result.entry.name, alt: 'default alt',
        })));
    });

    it('leaves the value unchanged when the picker is cancelled', async () => {
        const { fixture, closed, valueChanged } = await setup();
        fixture.componentInstance.chooseFromLibrary();
        closed.next(null);
        expect(valueChanged).not.toHaveBeenCalled();
        expect(fixture.componentInstance.controlValue()).toEqual(['/existing.jpg']);
    });

    it('defaults to multiple selection when the descriptor omits the flag', async () => {
        const { fixture, closed, modals, valueChanged } = await setup(true, { multiple: undefined }, []);
        (fixture.nativeElement.querySelector('.choose-from-library button') as HTMLButtonElement).click();
        expect(modals.show).toHaveBeenCalledWith(AssetPickerComponent, expect.objectContaining({
            data: expect.objectContaining({ multiple: true, accept }),
        }));
        closed.next(results);
        expect(valueChanged).toHaveBeenCalledExactlyOnceWith(urls);
    });
});
