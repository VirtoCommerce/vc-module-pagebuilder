import { TestBed } from '@angular/core/testing';
import { firstValueFrom, of, Subject, throwError } from 'rxjs';

import { assetLibraryHelpers } from '@core/helpers';
import { ModalService } from './modal.service';
import { AssetLibraryService } from './asset-library.service';
import { AssetLibraryUploadCoordinatorService } from './asset-library-upload-coordinator.service';

describe('AssetLibraryUploadCoordinatorService', () => {
  const folderUrl = '/stores/store/Page Builder';
  const storedEntry = {
    type: 'blob' as const,
    name: 'hero.jpg',
    relativeUrl: `${folderUrl}/hero.jpg`,
  };
  let assets: {
    findByName: ReturnType<typeof vi.fn>;
    searchReferences: ReturnType<typeof vi.fn>;
    upload: ReturnType<typeof vi.fn>;
    getLabels: ReturnType<typeof vi.fn>;
  };
  let modals: { show: ReturnType<typeof vi.fn>; alert: ReturnType<typeof vi.fn> };
  let service: AssetLibraryUploadCoordinatorService;

  beforeEach(() => {
    assets = {
      findByName: vi.fn((_folderUrl: string, fileName: string) =>
        of(fileName === storedEntry.name ? storedEntry : null),
      ),
      searchReferences: vi.fn(() =>
        of({
          totalCount: 1,
          results: [{ assetUrl: storedEntry.relativeUrl, referencesCount: 2, pages: [{ id: 'page-1' }] }],
        }),
      ),
      upload: vi.fn((_folderUrl: string, file: File) => of({ ...storedEntry, name: file.name })),
      getLabels: vi.fn(() => ({ uploadCanceled: 'Upload canceled. No files were uploaded.' })),
    };
    modals = { show: vi.fn(), alert: vi.fn(() => of(true)) };

    TestBed.configureTestingModule({
      providers: [
        AssetLibraryUploadCoordinatorService,
        { provide: AssetLibraryService, useValue: assets },
        { provide: ModalService, useValue: modals },
      ],
    });
    service = TestBed.inject(AssetLibraryUploadCoordinatorService);
  });

  it('uploads a different extension without opening the conflict dialog', async () => {
    const result = await firstValueFrom(service.uploadFiles(folderUrl, [createFile('hero.png')]));

    expect(result.map((entry) => entry.name)).toEqual(['hero.png']);
    expect(modals.show).not.toHaveBeenCalled();
    expect(assets.searchReferences).not.toHaveBeenCalled();
  });

  it('loads reference pages and replaces under the same name after confirmation', async () => {
    modals.show.mockReturnValue(of({ action: 'replace' }));

    const result = await firstValueFrom(service.uploadFiles(folderUrl, [createFile('hero.jpg')]));

    expect(result.map((entry) => entry.name)).toEqual(['hero.jpg']);
    expect(assets.searchReferences).toHaveBeenCalledWith(null, storedEntry);
    expect(modals.show.mock.calls[0][1].data.source).toBe('stored');
    expect(modals.show.mock.calls[0][1].data.usageKnown).toBe(true);
    expect(assets.upload).toHaveBeenCalledOnce();
  });

  it('uses the stored name when Replace matches case-insensitively', async () => {
    assets.findByName.mockReturnValue(of(storedEntry));
    modals.show.mockReturnValue(of({ action: 'replace' }));

    await firstValueFrom(service.uploadFiles(folderUrl, [createFile('Hero.jpg')]));

    expect(assets.upload.mock.calls[0][1].name).toBe('hero.jpg');
  });

  it('uploads under the name returned by Upload as', async () => {
    modals.show.mockReturnValue(of({ action: 'upload-as', fileName: 'hero-new.jpg' }));

    const result = await firstValueFrom(service.uploadFiles(folderUrl, [createFile('hero.jpg')]));

    expect(result.map((entry) => entry.name)).toEqual(['hero-new.jpg']);
    expect(assets.upload.mock.calls[0][1].name).toBe('hero-new.jpg');
  });

  it('cancels the complete batch before any upload is written', async () => {
    modals.show.mockReturnValue(of(null));

    const result = await firstValueFrom(
      service.uploadFiles(folderUrl, [createFile('new.jpg'), createFile('hero.jpg')]),
    );

    expect(result).toEqual([]);
    expect(assets.upload).not.toHaveBeenCalled();
    expect(modals.alert).toHaveBeenCalledWith('Upload canceled. No files were uploaded.');
  });

  it('confirms duplicate names inside one batch before uploading', async () => {
    assets.findByName.mockReturnValue(of(null));
    assets.getLabels.mockReturnValue({
      overwriteBatchDuplicate:
        'Another file in this upload is already named {name}. Keeping the same name will upload only the last one.',
      uploadCanceled: 'Upload canceled. No files were uploaded.',
    });
    modals.show.mockReturnValue(of({ action: 'upload-as', fileName: 'second-copy.jpg' }));

    const result = await firstValueFrom(
      service.uploadFiles(folderUrl, [createFile('same.jpg'), createFile('same.jpg')]),
    );

    expect(modals.show).toHaveBeenCalledOnce();
    expect(assets.searchReferences).not.toHaveBeenCalled();
    const dialogData = modals.show.mock.calls[0][1].data;
    expect(dialogData.source).toBe('batch');
    expect(dialogData.usageKnown).toBe(true);
    expect(
      assetLibraryHelpers.getAssetOverwriteConsequenceMessage({
        source: dialogData.source,
        usageKnown: dialogData.usageKnown,
        assetName: dialogData.existingEntry.name,
        referencesCount: dialogData.reference.referencesCount,
        labels: dialogData.labels,
      }),
    ).toBe(
      'Another file in this upload is already named same.jpg. Keeping the same name will upload only the last one.',
    );
    expect(result.map((entry) => entry.name)).toEqual(['same.jpg', 'second-copy.jpg']);
  });

  it('allows the user to continue when reference lookup fails', async () => {
    assets.searchReferences.mockReturnValue(throwError(() => new Error('reference service unavailable')));
    assets.getLabels.mockReturnValue({
      overwriteUsageUnknown:
        'We could not determine whether {name} is used on Page Builder pages. Replacing it may change pages that use this asset.',
      uploadCanceled: 'Upload canceled. No files were uploaded.',
    });
    modals.show.mockReturnValue(of({ action: 'replace' }));

    const result = await firstValueFrom(service.uploadFiles(folderUrl, [createFile('hero.jpg')]));
    const dialogData = modals.show.mock.calls[0][1].data;

    expect(dialogData.usageKnown).toBe(false);
    expect(
      assetLibraryHelpers.getAssetOverwriteConsequenceMessage({
        source: dialogData.source,
        usageKnown: dialogData.usageKnown,
        assetName: dialogData.existingEntry.name,
        referencesCount: dialogData.reference.referencesCount,
        labels: dialogData.labels,
      }),
    ).toContain('could not determine');
    expect(result.map((entry) => entry.name)).toEqual(['hero.jpg']);
  });

  it('stops before the next upload after unsubscription', async () => {
    assets.findByName.mockReturnValue(of(null));
    const firstUpload = new Subject<typeof storedEntry>();
    assets.upload.mockReturnValueOnce(firstUpload).mockReturnValueOnce(of({ ...storedEntry, name: 'second.jpg' }));

    const subscription = service
      .uploadFiles(folderUrl, [createFile('first.jpg'), createFile('second.jpg')])
      .subscribe();

    await vi.waitFor(() => expect(assets.upload).toHaveBeenCalledOnce());
    subscription.unsubscribe();
    firstUpload.next({ ...storedEntry, name: 'first.jpg' });
    firstUpload.complete();
    await Promise.resolve();

    expect(assets.upload).toHaveBeenCalledOnce();
  });
});

function createFile(name: string): File {
  return new File([name], name, { type: 'image/jpeg', lastModified: 123 });
}
