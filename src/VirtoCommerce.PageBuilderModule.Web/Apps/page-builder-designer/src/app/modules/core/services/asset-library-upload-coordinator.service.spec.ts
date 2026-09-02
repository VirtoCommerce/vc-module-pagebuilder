import { TestBed } from '@angular/core/testing';
import { firstValueFrom, of } from 'rxjs';

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
  let modals: { show: ReturnType<typeof vi.fn> };
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
      getLabels: vi.fn(() => ({})),
    };
    modals = { show: vi.fn() };

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
    expect(assets.upload).toHaveBeenCalledOnce();
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
  });

  it('confirms duplicate names inside one batch before uploading', async () => {
    assets.findByName.mockReturnValue(of(null));
    modals.show.mockReturnValue(of({ action: 'upload-as', fileName: 'second-copy.jpg' }));

    const result = await firstValueFrom(
      service.uploadFiles(folderUrl, [createFile('same.jpg'), createFile('same.jpg')]),
    );

    expect(modals.show).toHaveBeenCalledOnce();
    expect(assets.searchReferences).not.toHaveBeenCalled();
    expect(result.map((entry) => entry.name)).toEqual(['same.jpg', 'second-copy.jpg']);
  });
});

function createFile(name: string): File {
  return new File([name], name, { type: 'image/jpeg', lastModified: 123 });
}
