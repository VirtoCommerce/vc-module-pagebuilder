import { HttpErrorResponse } from '@angular/common/http';
import { DestroyRef, Directive, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { UntypedFormGroup } from '@angular/forms';
import { moveItemInArray } from '@angular/cdk/drag-drop';
import { FileUploadControl, FileUploadValidators } from '@iplab/ngx-file-upload';
import { Subject, takeUntil } from 'rxjs';

import { ControlContext, AssetFile } from '@core/models';
import { ModalService, AssetsService, ClipboardService } from '@core/services';
import { BaseControlDirective } from '@core/controls/base-control.directive';
import { FilesDescriptor } from '@models/controls';

import { coreHelpers, formsHelpers } from '@core/helpers';

@Directive({})
export abstract class BaseFilesComponent<T extends FilesDescriptor> extends BaseControlDirective<T> {

  private readonly destroyRef = inject(DestroyRef);
  private readonly modals = inject(ModalService);
  private readonly data = inject(AssetsService);
  private readonly clipboard = inject(ClipboardService);
  private readonly elementReset$ = new Subject<void>();
  private previousExpanded: boolean | null = null;

  readonly elementForm = signal<UntypedFormGroup | null>(null);
  readonly selectedFile = signal<AssetFile | null>(null);
  readonly expanded = signal(false);
  readonly innerValue = signal<AssetFile[]>([]);
  readonly isDrag = signal(false);
  readonly sortable = signal(true);
  readonly multiple = signal(true);
  readonly control = signal<FileUploadControl | null>(null);

  protected override initContent(): void {
    super.initContent();
    this.multiple.set(this.descriptor?.multiple !== false);
    this.sortable.set(this.descriptor?.sortable !== false && this.multiple());
    this.innerValue.set(this.getValue());
    const ctrl = this.createUploadControl();
    this.control.set(ctrl);
    ctrl.valueChanges.pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(items => {
      if (items && items.length) {
        const files = items.map((x, index) => this.convertValueToFile(x, index));
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
      const getName = (x: string) => x.startsWith('data:') ? '[inline data]' : x.substring(x.lastIndexOf('/') + 1);
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
      result = <AssetFile>{
        lastModified: 0,
        name: (item[this.descriptor?.filenameField || 'filename']
          || item[this.descriptor?.urlField || 'url']?.substring(item[this.descriptor?.urlField || 'url'].lastIndexOf('/') + 1)) ?? null,
        webkitRelativePath: item[this.descriptor?.urlField || 'url'],
        data: item,
        url: item[this.descriptor?.urlField || 'url'],
      };
    }
    const context = this.getContext(result);
    result.previewUrl = this.data.getPreviewUrl(result, this.descriptor || {}, context);
    result.uploaded = false;
    result.uploading = false;

    return result;
  }

  private uploadFiles(items: Array<AssetFile>) {
    items.forEach((x: AssetFile) => {
      this.uploadItem(x);
    });
  }

  private createUploadControl(): FileUploadControl {
    return new FileUploadControl(this.getControlOptions(), this.descriptor?.maxFileSize ? FileUploadValidators.fileSize(this.descriptor.maxFileSize) : undefined);
  }

  protected getControlOptions() {
    return {
      listVisible: false,
      accept: this.descriptor?.accept ? this.descriptor.accept.split(',') : [],
      discardInvalid: true,
      disabled: false,
      multiple: !!this.multiple(),
      native: false
    };
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
