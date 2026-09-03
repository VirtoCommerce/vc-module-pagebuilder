import { inject, Injectable } from '@angular/core';
import { firstValueFrom, Observable } from 'rxjs';

import {
  AssetOverwriteComponent,
  AssetOverwriteDialogResult,
} from '@core/dialogs/asset-overwrite/asset-overwrite.component';
import { assetLibraryHelpers } from '@core/helpers';

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
    return new Observable((subscriber) => {
      const controller = new AbortController();

      this.prepareAndUpload(folderUrl, files, context, controller.signal).then(
        (uploaded) => {
          if (!subscriber.closed) {
            subscriber.next(uploaded);
            subscriber.complete();
          }
        },
        (error) => {
          if (!subscriber.closed && !controller.signal.aborted) {
            subscriber.error(error);
          }
        },
      );

      return () => controller.abort();
    });
  }

  private async prepareAndUpload(
    folderUrl: string,
    files: File[],
    context: AssetLibraryContext | null,
    signal: AbortSignal,
  ): Promise<AssetLibraryEntry[]> {
    const preparedFiles = await this.prepareFiles(folderUrl, files, context, signal);

    if (!preparedFiles) {
      this.modals.alert(this.assets.getLabels().uploadCanceled);
      return [];
    }

    return this.uploadPreparedFiles(folderUrl, preparedFiles, signal);
  }

  private async prepareFiles(
    folderUrl: string,
    files: File[],
    context: AssetLibraryContext | null,
    signal: AbortSignal,
  ): Promise<File[] | null> {
    const preparedFiles: File[] = [];

    for (const file of files) {
      signal.throwIfAborted();
      const storedEntry = await firstValueFrom(this.assets.findByName(folderUrl, file.name));
      const reservedEntry = preparedFiles.some(
        (item) =>
          assetLibraryHelpers.normalizeAssetFileName(item.name) ===
          assetLibraryHelpers.normalizeAssetFileName(file.name),
      )
        ? createPlannedEntry(folderUrl, file.name)
        : null;
      const existingEntry = storedEntry ?? reservedEntry;

      if (!existingEntry) {
        preparedFiles.push(file);
        continue;
      }

      const referenceResult = storedEntry
        ? await this.getReference(context, storedEntry)
        : { reference: emptyReference(existingEntry), usageKnown: true };
      const decision = await firstValueFrom(
        this.modals.show<AssetOverwriteDialogResult | null>(AssetOverwriteComponent, {
          autoFocus: '#asset-overwrite-file-name',
          panelClass: 'asset-overwrite-dialog',
          data: {
            file,
            folderUrl,
            context,
            existingEntry,
            reference: referenceResult.reference,
            source: storedEntry ? 'stored' : 'batch',
            usageKnown: referenceResult.usageKnown,
            reservedNames: preparedFiles.map((item) => item.name),
            labels: this.assets.getLabels(),
          },
        }),
      );
      signal.throwIfAborted();

      if (!decision) {
        return null;
      }

      preparedFiles.push(decision.action === 'replace' ? file : renameFile(file, decision.fileName));
    }

    return preparedFiles;
  }

  private async uploadPreparedFiles(
    folderUrl: string,
    preparedFiles: File[],
    signal: AbortSignal,
  ): Promise<AssetLibraryEntry[]> {
    const uploaded: AssetLibraryEntry[] = [];

    for (const file of preparedFiles) {
      signal.throwIfAborted();
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
  ): Promise<{ reference: AssetLibraryReference; usageKnown: boolean }> {
    try {
      const result = await firstValueFrom(this.assets.searchReferences(context, entry));
      return { reference: result.results[0] ?? emptyReference(entry), usageKnown: true };
    } catch {
      return { reference: emptyReference(entry), usageKnown: false };
    }
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
