import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogActions, MatDialogContent, MatDialogRef } from '@angular/material/dialog';
import { FormField, form, required, submit, validate } from '@angular/forms/signals';
import { firstValueFrom } from 'rxjs';

import { IconButtonComponent } from '@core/components/icon-button/icon-button.component';
import {
  AssetLibraryContext,
  AssetLibraryEntry,
  AssetLibraryLabels,
  AssetLibraryReference,
} from '../../services/asset-library.models';
import { AssetLibraryService } from '../../services/asset-library.service';

export interface AssetOverwriteDialogData {
  file: File;
  folderUrl: string;
  context: AssetLibraryContext | null;
  existingEntry: AssetLibraryEntry;
  reference: AssetLibraryReference;
  source: 'stored' | 'batch';
  reservedNames: string[];
  labels: AssetLibraryLabels;
}

export type AssetOverwriteDialogResult = { action: 'replace' } | { action: 'upload-as'; fileName: string };

@Component({
  selector: 'app-asset-overwrite',
  templateUrl: './asset-overwrite.component.html',
  styleUrls: ['./asset-overwrite.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormField, MatDialogActions, MatDialogContent, IconButtonComponent],
})
export class AssetOverwriteComponent {
  private readonly data = inject<AssetOverwriteDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<AssetOverwriteComponent, AssetOverwriteDialogResult | null>);
  private readonly assets = inject(AssetLibraryService);

  readonly labels = this.data.labels;
  readonly checkingName = signal(false);
  readonly serverError = signal('');
  readonly fileNameModel = signal({ fileName: this.data.file.name });
  readonly fileNameForm = form(this.fileNameModel, (path) => {
    required(path.fileName, { message: this.labels.fileNameRequired });
    validate(path.fileName, ({ value }) =>
      /[\\/]/.test(value().trim()) ? { kind: 'invalid', message: this.labels.fileNameInvalid } : undefined,
    );
  });
  readonly pageNames = computed(() =>
    this.data.source === 'batch'
      ? []
      : [
          ...new Set(
            (this.data.reference.pages ?? [])
              .map((page) => page.name || page.permalink || page.id)
              .filter((name): name is string => !!name),
          ),
        ],
  );
  readonly consequenceMessage = computed(() => getAssetOverwriteConsequenceMessage(this.data));

  replace() {
    this.dialogRef.close({ action: 'replace' });
  }

  cancel() {
    this.dialogRef.close(null);
  }

  uploadAs() {
    this.serverError.set('');
    submit(this.fileNameForm, async () => {
      const fileName = this.fileNameModel().fileName.trim();
      this.checkingName.set(true);

      try {
        const reserved = this.data.reservedNames.some(
          (name) => normalizeFileName(name) === normalizeFileName(fileName),
        );
        const existing = reserved ? null : await firstValueFrom(this.assets.findByName(this.data.folderUrl, fileName));

        if (reserved || existing) {
          this.serverError.set(formatLabel(this.labels.fileNameCollision, { name: fileName }));
          return;
        }

        this.dialogRef.close({ action: 'upload-as', fileName });
      } catch (error) {
        this.serverError.set(error instanceof Error ? error.message : this.labels.loadError);
      } finally {
        this.checkingName.set(false);
      }
    });
  }
}

export function getAssetOverwriteConsequenceMessage(
  data: Pick<AssetOverwriteDialogData, 'source' | 'existingEntry' | 'reference' | 'labels'>,
): string {
  const count = data.reference.referencesCount ?? 0;
  let template = data.labels.overwriteUsedMany;

  if (data.source === 'batch') {
    template = data.labels.overwriteBatchDuplicate;
  } else if (count === 0) {
    template = data.labels.overwriteUnused;
  } else if (count === 1) {
    template = data.labels.overwriteUsedOne;
  }

  return formatLabel(template, {
    name: data.existingEntry.name,
    count: count.toString(),
  });
}

function formatLabel(template: string, values: Record<string, string>): string {
  return Object.entries(values).reduce((result, [key, value]) => result.replaceAll(`{${key}}`, value), template);
}

function normalizeFileName(value: string): string {
  return value.trim().normalize('NFC');
}
