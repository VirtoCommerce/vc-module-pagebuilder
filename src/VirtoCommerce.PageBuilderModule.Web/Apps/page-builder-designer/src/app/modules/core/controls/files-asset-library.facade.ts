import { inject, Injectable } from '@angular/core';
import { concatMap, from, map, Observable, toArray } from 'rxjs';

import { assetLibraryHelpers, coreHelpers } from '@core/helpers';
import { AssetFile } from '@core/models';
import { AssetLibraryContext, AssetLibraryEntry, AssetLibraryService } from '@core/services';
import { FilesDescriptor } from '@models/controls';

import { AssetPickerDialogItem } from '@core/dialogs';

export interface AssetLibraryDropFilesResult {
  files: File[];
  error: string | null;
}

@Injectable({
  providedIn: 'root'
})
export class FilesAssetLibraryFacade {
  private readonly assetLibrary = inject(AssetLibraryService);

  readonly labels = this.assetLibrary.getLabels();

  getRootFolderUrl(context: any): string | null {
    return this.assetLibrary.getRootFolderUrl(context);
  }

  uploadFiles(folderUrl: string, files: File[], context: AssetLibraryContext | null = null): Observable<AssetPickerDialogItem[]> {
    return from(files).pipe(
      concatMap(file => this.assetLibrary.upload(folderUrl, file)),
      toArray(),
      map(entries => entries
        .filter((entry): entry is AssetLibraryEntry => !!entry)
        .map(entry => this.createPickerResult(entry, context))
        .filter((item): item is AssetPickerDialogItem => !!item))
    );
  }

  getUploadableDroppedFiles(
    files: File[],
    descriptor: FilesDescriptor | null | undefined,
    multiple: boolean,
    acceptedTypes = this.getAcceptedTypes(descriptor)
  ): AssetLibraryDropFilesResult {
    const acceptedFiles = files.filter(file => assetLibraryHelpers.matchesAcceptFile(file, acceptedTypes));
    const oversizedFile = acceptedFiles.find(file => this.isFileTooLarge(file, descriptor));

    if (oversizedFile) {
      return {
        files: [],
        error: this.labels.fileTooLarge.replace('{maxSize}', assetLibraryHelpers.formatAssetSize(descriptor?.maxFileSize ?? 0))
      };
    }

    return {
      files: acceptedFiles.slice(0, multiple ? undefined : 1),
      error: null
    };
  }

  createAssetFile(result: AssetPickerDialogItem, descriptor: FilesDescriptor | null | undefined): AssetFile {
    return <AssetFile>{
      lastModified: 0,
      name: this.decodeAssetName(result.entry.name),
      webkitRelativePath: result.url,
      data: descriptor?.element?.length ? coreHelpers.createDefaultObject(descriptor.element) : undefined,
      url: result.url,
      previewUrl: result.previewUrl,
      uploaded: true,
      uploading: false,
      assetName: result.entry.name,
      error: null,
      size: result.entry.size ?? 0,
      type: result.entry.contentType ?? ''
    };
  }

  private createPickerResult(entry: AssetLibraryEntry, context: AssetLibraryContext | null = null): AssetPickerDialogItem | null {
    const url = this.assetLibrary.getPublicUrl(entry, context);

    return url
      ? {
        entry,
        url,
        previewUrl: this.assetLibrary.getPreviewUrl(entry, context)
      }
      : null;
  }

  private getAcceptedTypes(descriptor: FilesDescriptor | null | undefined): string[] {
    return descriptor?.accept
      ? descriptor.accept.split(',').map(x => x.trim()).filter(Boolean)
      : [];
  }

  private isFileTooLarge(file: File, descriptor: FilesDescriptor | null | undefined): boolean {
    return !!descriptor?.maxFileSize && file.size > descriptor.maxFileSize;
  }

  private decodeAssetName(value: string | null | undefined): string | null {
    if (!value) {
      return null;
    }

    return assetLibraryHelpers.safeDecodeURIComponent(value);
  }
}
