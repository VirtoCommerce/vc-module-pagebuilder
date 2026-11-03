import { HttpErrorResponse } from '@angular/common/http';
import { computed, DestroyRef, Directive, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { UntypedFormGroup } from '@angular/forms';
import { moveItemInArray } from '@angular/cdk/drag-drop';
import { FileUploadControl, FileUploadValidators } from '@iplab/ngx-file-upload';
import { Subject, takeUntil } from 'rxjs';

import { ControlContext, AssetFile } from '@core/models';
import { ModalService, AssetsService, ClipboardService } from '@core/services';
import { BaseControlDirective } from '@core/controls/base-control.directive';
import { AssetPickerComponent, AssetPickerDialogItem, AssetPickerDialogResult } from '@core/dialogs';
import { FilesDescriptor } from '@models/controls';
import { FilesAssetLibraryFacade } from './files-asset-library.facade';

import { assetLibraryHelpers, coreHelpers, formsHelpers } from '@core/helpers';

@Directive({})
export abstract class BaseFilesComponent<T extends FilesDescriptor> extends BaseControlDirective<T> {

  private readonly destroyRef = inject(DestroyRef);
  private readonly modals = inject(ModalService);
  private readonly data = inject(AssetsService);
  private readonly assetLibrary = inject(FilesAssetLibraryFacade);
  private readonly clipboard = inject(ClipboardService);
  private readonly elementReset$ = new Subject<void>();
  private previousExpanded: boolean | null = null;
  private assetLibraryDragDepth = 0;

  readonly assetLibraryLabels = this.assetLibrary.labels;
  readonly elementForm = signal<UntypedFormGroup | null>(null);
  readonly selectedFile = signal<AssetFile | null>(null);
  readonly expanded = signal(false);
  readonly innerValue = signal<AssetFile[]>([]);
  readonly isDrag = signal(false);
  readonly sortable = signal(true);
  readonly multiple = signal(true);
  readonly assetLibraryDropActive = signal(false);
  readonly assetLibraryUploading = signal(false);
  readonly useLegacyUploadControl = signal(false);
  readonly control = signal<FileUploadControl | null>(null);
  readonly displayLabel = computed(() => this.descriptor?.label || this.defaultLabel);

  protected get defaultLabel(): string {
    return this.assetLibraryLabels.fileLabel;
  }

  protected override initContent(): void {
    super.initContent();
    this.multiple.set(this.descriptor?.multiple !== false);
    this.sortable.set(this.descriptor?.sortable !== false && this.multiple());
    this.innerValue.set(this.getValue());

    this.useLegacyUploadControl.set(this.shouldUseLegacyUploadControl());
    if (this.useLegacyUploadControl()) {
      this.initLegacyUploadControl();
    } else {
      this.control.set(null);
    }
  }

  reorderItems(previous: number, current: number) {
    const arr = [...this.innerValue()];
    moveItemInArray(arr, previous, current);
    this.innerValue.set(arr);
    this.raiseValueChanged();
  }

  toggleList() {
    this.expanded.update(v => !v);
    if (!this.expanded()) {
      this.closeEditor();
    }
  }

  selectFile(file: AssetFile) {
    if (this.descriptor?.element && this.descriptor.element.length) {
      if (file === this.selectedFile()) {
        this.closeEditor();
      } else {
        this.elementReset$.next();
        const form = formsHelpers.generateForm(file.data, this.descriptor.element);
        form.valueChanges.pipe(
          takeUntil(this.elementReset$),
          takeUntilDestroyed(this.destroyRef)
        ).subscribe(value => {
          (<any>this.selectedFile()).data = value;
          this.raiseValueChanged();
        });
        this.elementForm.set(form);
        this.selectedFile.set(file);
        if (this.previousExpanded === null) {
          this.previousExpanded = this.expanded();
        }
        this.expanded.set(true);
      }
    }
  }

  private closeEditor() {
    this.elementReset$.next();
    this.selectedFile.set(null);
    this.elementForm.set(null);
    if (this.previousExpanded !== null) {
      this.expanded.set(this.previousExpanded);
      this.previousExpanded = null;
    }
  }

  deleteFile(file: AssetFile) {
    if (!this.descriptor?.skipRemoveConfirmation) {
      this.modals.confirm(this.descriptor?.removeMessage || 'Do you want to delete this item?').pipe(
        takeUntilDestroyed(this.destroyRef)
      ).subscribe((data: any) => {
        if (data) {
          this.deleteFileInternal(file);
        }
      });
    } else {
      this.deleteFileInternal(file);
    }
  }

  copyUrl(file: AssetFile) {
    const value = this.data.getPreviewUrl(file, this.descriptor || {}, this.getContext(file));
    this.clipboard.copyString(value);
  }

  chooseFromLibrary() {
    const rootFolderUrl = this.assetLibrary.getRootFolderUrl(this.context);
    if (!rootFolderUrl) {
      this.modals.alert(this.assetLibraryLabels.storeRequired);
      return;
    }

    this.modals.show<AssetPickerDialogResult>(AssetPickerComponent, {
      panelClass: 'asset-picker-dialog',
      autoFocus: false,
      data: {
        rootFolderUrl,
        context: this.context,
        accept: this.getControlOptions().accept,
        multiple: this.multiple(),
        maxFileSize: this.descriptor?.maxFileSize
      }
    }).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(result => {
      if (result) {
        this.addAssetsFromLibrary(Array.isArray(result) ? result : [result]);
      }
    });
  }

  uploadItem(file: AssetFile) {
    if (!file.uploaded && !file.uploading) {
      file.uploading = true;
      file.error = null;
      const context = this.getContext(file);
      this.data.uploadAsset(file, this.descriptor || {}, context, value => {
        file.progress = value;
      }).pipe(
        takeUntilDestroyed(this.destroyRef)
      ).subscribe({
        next: (_) => {
          file.uploaded = true;
          file.uploading = false;
          file.previewUrl = this.data.getPreviewUrl(file, this.descriptor || {}, context);
          file.error = null;
          this.raiseValueChanged();
          this.innerValue.update(v => [...v]);
        },
        error: (error: HttpErrorResponse) => {
          file.error = error.message;
          if (error.status === 413) {
            file.error = 'File is too large';
          }
          file.uploading = false;
          this.innerValue.update(v => [...v]);
        }
      });
    }
  }

  onAssetLibraryButtonDragEnter(event: DragEvent) {
    if (!this.hasDraggedFiles(event)) {
      return;
    }

    this.preventDragDefaults(event);
    this.assetLibraryDragDepth += 1;
    this.assetLibraryDropActive.set(true);
  }

  onAssetLibraryButtonDragOver(event: DragEvent) {
    if (!this.hasDraggedFiles(event)) {
      return;
    }

    this.preventDragDefaults(event);
  }

  onAssetLibraryButtonDragLeave(event: DragEvent) {
    if (!this.hasDraggedFiles(event)) {
      return;
    }

    this.preventDragDefaults(event);
    this.assetLibraryDragDepth = Math.max(this.assetLibraryDragDepth - 1, 0);

    if (!this.assetLibraryDragDepth) {
      this.assetLibraryDropActive.set(false);
    }
  }

  onAssetLibraryButtonDrop(event: DragEvent) {
    if (!this.hasDraggedFiles(event)) {
      return;
    }

    this.preventDragDefaults(event);
    this.assetLibraryDragDepth = 0;
    this.assetLibraryDropActive.set(false);

    const rootFolderUrl = this.assetLibrary.getRootFolderUrl(this.context);
    if (!rootFolderUrl) {
      this.modals.alert(this.assetLibraryLabels.storeRequired);
      return;
    }

    this.uploadDroppedAssets(Array.from(event.dataTransfer?.files ?? []), rootFolderUrl);
  }

  getContext(item: AssetFile): ControlContext {
    const index = this.innerValue().indexOf(item);
    const element = this.convertFileToValue(item);
    return {
      ...this.context,
      item: this.controlValue(),
      index,
      element,
      file: item,
      parent: this.context
    };
  }

  onStartDrag() {
    this.isDrag.set(true);
  }

  onReleaseDrag() {
    this.isDrag.set(false);
  }

  private deleteFileInternal(file: AssetFile) {
    if (this.selectedFile() === file) {
      this.selectedFile.set(null);
    }
    this.innerValue.update(v => v.filter(x => x !== file));
    this.raiseValueChanged();
  }

  private raiseValueChanged() {
    const value = this.convertFilesToValue(<any>this.innerValue());
    this.onValueChanged(value);
  }

  private getValue(): AssetFile[] {
    let files = this.controlValue();
    if (!files) {
      return [];
    }

    if (!Array.isArray(files)) {
      files = [files];
    }
    return files.map((x: any, index: number) => this.convertValueToFile(x, index));
  }

  private convertValueToFile(item: any, _index: number): AssetFile {
    let result: AssetFile;
    if (typeof item === 'string') {
      const getName = (x: string) => x.startsWith('data:') ? '[inline data]' : this.decodeAssetName(x.substring(x.lastIndexOf('/') + 1));
      const name = item ? getName(item) : null;
      result = <AssetFile>{
        lastModified: 0,
        name,
        webkitRelativePath: item,
        url: item,
      };
    } else if (item instanceof File) {
      result = <AssetFile>item;
      if (this.descriptor?.element && this.descriptor.element.length) {
        result.data = coreHelpers.createDefaultObject(this.descriptor.element);
      }
    } else {
      const name = item[this.descriptor?.filenameField || 'filename']
        || item[this.descriptor?.urlField || 'url']?.substring(item[this.descriptor?.urlField || 'url'].lastIndexOf('/') + 1);
      result = <AssetFile>{
        lastModified: 0,
        name: this.decodeAssetName(name) ?? null,
        webkitRelativePath: item[this.descriptor?.urlField || 'url'],
        data: item,
        url: item[this.descriptor?.urlField || 'url'],
      };
    }
    const context = this.getContext(result);
    result.previewUrl = this.data.getPreviewUrl(result, this.descriptor || {}, context);
    result.uploaded = !(item instanceof File);
    result.uploading = false;

    return result;
  }

  private addAssetsFromLibrary(results: AssetPickerDialogItem[]) {
    const files = results.map(result => this.assetLibrary.createAssetFile(result, this.descriptor));

    if (this.multiple()) {
      this.innerValue.update(v => [...v, ...files]);
    } else {
      this.innerValue.set(files.slice(-1));
    }
    this.raiseValueChanged();
  }

  private uploadDroppedAssets(files: File[], rootFolderUrl: string) {
    const uploadResult = this.assetLibrary.getUploadableDroppedFiles(files, this.descriptor, this.multiple(), this.getControlOptions().accept);

    if (uploadResult.error) {
      this.modals.alert(uploadResult.error);
      return;
    }

    if (!uploadResult.files.length || this.assetLibraryUploading()) {
      return;
    }

    this.assetLibraryUploading.set(true);

    this.assetLibrary.uploadFiles(rootFolderUrl, uploadResult.files, this.context).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: results => {
        this.assetLibraryUploading.set(false);
        if (results.length) {
          this.addAssetsFromLibrary(results);
        }
      },
      error: error => {
        this.assetLibraryUploading.set(false);
        this.modals.alert(error?.message || this.assetLibraryLabels.uploadError);
      }
    });
  }

  private initLegacyUploadControl() {
    const ctrl = this.createUploadControl();
    this.control.set(ctrl);
    ctrl.valueChanges.pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((items: File[]) => {
      if (items && items.length) {
        const files = items.map((x: File, index: number) => this.convertValueToFile(x, index));
        this.uploadFiles(files);
        if (this.multiple()) {
          this.innerValue.update(v => [...v, ...files]);
        } else {
          this.innerValue.set(files);
        }
        this.raiseValueChanged();
        ctrl.setValue([]);
      }
    });
  }

  private shouldUseLegacyUploadControl(): boolean {
    return !this.assetLibrary.getRootFolderUrl(this.context) || this.data.isInlineUpload(this.descriptor || {}, this.context);
  }

  private uploadFiles(items: Array<AssetFile>) {
    items.forEach((x: AssetFile) => {
      this.uploadItem(x);
    });
  }

  private decodeAssetName(value: string | null | undefined): string | null {
    if (!value) {
      return null;
    }

    return assetLibraryHelpers.safeDecodeURIComponent(value);
  }

  protected getControlOptions() {
    return {
      accept: this.descriptor?.accept
        ? this.descriptor.accept.split(',').map(x => x.trim()).filter(Boolean)
        : [],
      multiple: !!this.multiple()
    };
  }

  private createUploadControl(): FileUploadControl {
    return new FileUploadControl(this.getLegacyControlOptions(), this.descriptor?.maxFileSize ? FileUploadValidators.fileSize(this.descriptor.maxFileSize) : undefined);
  }

  private getLegacyControlOptions() {
    return {
      ...this.getControlOptions(),
      listVisible: false,
      discardInvalid: true,
      disabled: false,
      native: false
    };
  }

  private hasDraggedFiles(event: DragEvent): boolean {
    return Array.from(event.dataTransfer?.types ?? []).includes('Files');
  }

  private preventDragDefaults(event: DragEvent) {
    event.preventDefault();
    event.stopPropagation();

    if (event.dataTransfer) {
      event.dataTransfer.dropEffect = 'copy';
    }
  }

  private convertFilesToValue(items: AssetFile[]): any {

    if (!items || !items.length) {
      return null;
    }

    let result = items.filter(x => x.uploaded).map(x => this.convertFileToValue(x));

    if (!this.multiple()) {
      result = result.length > 0 ? result[0] : null;
    }

    return result;
  }

  private convertFileToValue(item: AssetFile): any {
    const hasElement = !!this.descriptor?.element && this.descriptor.element.length > 0;
    const propertiesSet = this.descriptor?.urlField || this.descriptor?.filenameField;

    let result: any = {};

    if (!hasElement && !propertiesSet) {
      result = item.url || item.name;
    }
    else {
      result = <any>{
        [this.descriptor?.filenameField || 'filename']: item.name,
        ...(item.data || {}),
        [this.descriptor?.urlField || 'url']: item.url || item.webkitRelativePath || item.name
      };
    }

    return result;
  }
}
