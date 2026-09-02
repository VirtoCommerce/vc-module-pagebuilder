import { inject, Injectable } from '@angular/core';
import { firstValueFrom, from, Observable } from 'rxjs';

import {
  AssetOverwriteComponent,
  AssetOverwriteDialogResult,
} from '@core/dialogs/asset-overwrite/asset-overwrite.component';

import { ModalService } from './modal.service';
import { AssetLibraryContext, AssetLibraryEntry, AssetLibraryReference } from './asset-library.models';
import { AssetLibraryService } from './asset-library.service';

@Injectable({
  providedIn: 'root',
})
export class AssetLibraryUploadCoordinatorService {
  private readonly assets = inject(AssetLibraryService);
  private readonly modals = inject(ModalService);

  uploadFiles(
    folderUrl: string,
    files: File[],
    context: AssetLibraryContext | null = null,
  ): Observable<AssetLibraryEntry[]> {
    return from(this.prepareAndUpload(folderUrl, files, context));
  }

  private async prepareAndUpload(
    folderUrl: string,
    files: File[],
    context: AssetLibraryContext | null,
  ): Promise<AssetLibraryEntry[]> {
    const preparedFiles: File[] = [];

    for (const file of files) {
      const storedEntry = await firstValueFrom(this.assets.findByName(folderUrl, file.name));
      const reservedEntry = preparedFiles.some((item) => normalizeFileName(item.name) === normalizeFileName(file.name))
        ? createPlannedEntry(folderUrl, file.name)
        : null;
      const existingEntry = storedEntry ?? reservedEntry;

      if (!existingEntry) {
        preparedFiles.push(file);
        continue;
      }

      const reference = storedEntry ? await this.getReference(context, storedEntry) : emptyReference(existingEntry);
      const decision = await firstValueFrom(
        this.modals.show<AssetOverwriteDialogResult | null>(AssetOverwriteComponent, {
          autoFocus: '#asset-overwrite-file-name',
          panelClass: 'asset-overwrite-dialog',
          data: {
            file,
            folderUrl,
            context,
            existingEntry,
            reference,
            source: getConflictSource(storedEntry),
            reservedNames: preparedFiles.map((item) => item.name),
            labels: this.assets.getLabels(),
          },
        }),
      );

      if (!decision) {
        return [];
      }

      preparedFiles.push(decision.action === 'replace' ? file : renameFile(file, decision.fileName));
    }

    const uploaded: AssetLibraryEntry[] = [];

    for (const file of preparedFiles) {
      const entry = await firstValueFrom(this.assets.upload(folderUrl, file));

      if (entry) {
        uploaded.push(entry);
      }
    }

    return uploaded;
  }

  private async getReference(
    context: AssetLibraryContext | null,
    entry: AssetLibraryEntry,
  ): Promise<AssetLibraryReference> {
    const result = await firstValueFrom(this.assets.searchReferences(context, entry));
    return result.results[0] ?? emptyReference(entry);
  }
}

function renameFile(file: File, fileName: string): File {
  return new File([file], fileName, {
    type: file.type,
    lastModified: file.lastModified,
  });
}

function createPlannedEntry(folderUrl: string, fileName: string): AssetLibraryEntry {
  return {
    type: 'blob',
    name: fileName,
    relativeUrl: `${folderUrl.replace(/\/$/, '')}/${fileName}`,
  };
}

function emptyReference(entry: AssetLibraryEntry): AssetLibraryReference {
  return {
    assetUrl: entry.relativeUrl || entry.url,
    referencesCount: 0,
    pages: [],
  };
}

function normalizeFileName(value: string): string {
  return value.trim().normalize('NFC');
}

function getConflictSource(storedEntry: AssetLibraryEntry | null): 'stored' | 'batch' {
  return storedEntry ? 'stored' : 'batch';
}
