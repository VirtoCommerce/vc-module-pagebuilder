import { ChangeDetectionStrategy, Component, computed, DestroyRef, linkedSignal, signal, inject } from '@angular/core';
import { rxResource, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { NgSelectModule } from '@ng-select/ng-select';
import { Observable, Subject, of } from 'rxjs';
import { distinctUntilChanged, map } from 'rxjs/operators';

import { DataService } from '@core/services';
import { BaseControlDirective } from '@core/controls/base-control.directive';
import { SelectDescriptor } from '@models/controls';
import { appHelpers } from '@integration/helpers';

/**
 * https://ng-select.github.io/ng-select#/data-sources
 * https://github.com/ng-select/ng-select
 */

@Component({
    selector: 'app-select',
    templateUrl: './select.component.html',
    styleUrls: ['./select.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [FormsModule, NgSelectModule]
})
export class SelectComponent extends BaseControlDirective<SelectDescriptor> {

    private readonly destroyRef = inject(DestroyRef);
    private readonly data = inject(DataService);

    protected searchEvent$ = new Subject<string>();

    private readonly searchQuery = signal<string | null>(null);
    private readonly isLoaded = signal(false);
    readonly selectedValue = linkedSignal(() => this.selectControlValue);

    readonly optionsResource = rxResource({
      params: () => ({ query: this.searchQuery(), load: this.isLoaded() }),
      stream: ({ params }) => params.load ? this.doRequest(params.query) : of([])
    });

  private readonly _syntheticOptions = computed(() => {
    if (!this.descriptor?.request || !this.controlValue()) {
      return [];
    }
    const values = Array.isArray(this.controlValue()) ? this.controlValue() : [this.controlValue()];
    return values
      .map((x: any) => this.convertItemToOption(x))
      .filter(Boolean)
      .map((opt: any) => ({ ...opt, __synthetic: true }));
  });

    readonly effectiveOptions = computed<any[]>(() => {
      if (this.descriptor?.optionsSelector) {
        return appHelpers.evalInContext(this.descriptor.optionsSelector, this.context) as any[] ?? [];
      }
      if (!this.descriptor?.request) {
        const options = this.descriptor?.options ?? [];
        const query = this.searchQuery();
        if (!query || !this.descriptor?.searchable) {
          return options;
        }
        return options.filter(item =>
          item.label.toLocaleUpperCase().indexOf(query.toLocaleUpperCase()) !== -1
        );
      }
      const loaded = this.optionsResource.value() as any[] ?? [];
      const synthetic = this._syntheticOptions();
      if (this.optionsResource.isLoading()) {
        return [...synthetic, ...this.descriptor?.options ?? []];
      }
      const unmatchedSynthetic = synthetic.filter((s: unknown) => !loaded.some(item => this.compareWith(item, s)));
      return [...unmatchedSynthetic, ...loaded];
    });

    onOpen() {
        this.isLoaded.set(true);
    }

    onChange(value: any) {
      if (value !== null || !this.optionsResource.isLoading()) {
        this.selectedValue.set(value);
        this.onValueChanged(value);
      }
    }

    compareWith = (itemInSelect: any, itemInSource: any) => {
      const vA = (itemInSelect.value || itemInSelect)[this.descriptor?.equalKey || 'value'] || itemInSelect.value || itemInSelect;
      const v0 = (itemInSource.value || itemInSource)[this.descriptor?.equalKey || 'value'] || itemInSource.value || itemInSource;
      return vA === v0;
    }

    override initContent() {
      super.initContent();
      if (this.controlValue() != null) {
        this.isLoaded.set(true);
      }

      if (this.descriptor?.searchable) {
        this.searchEvent$.pipe(
          distinctUntilChanged(),
          takeUntilDestroyed(this.destroyRef)
        ).subscribe(q => this.searchQuery.set(q));
      }
    }

    get selectControlValue(): any {
      if (this.controlValue() && Array.isArray(this.controlValue())) {
        return this.controlValue().map((x: any) => this.convertItemToOption(x)?.value ?? null);
      }
      return this.convertItemToOption(this.controlValue())?.value ?? null;
    }

    unselect(item: any) {
      const newValue = (this.selectedValue() as any[]).filter((x: any) => !this.compareWith(x, item));
      this.selectedValue.set(newValue);
      this.onValueChanged(newValue);
    }

    private convertItemToOption(item: any) {
      if (!item || !this.descriptor) {
        return null;
      }
      const req = this.descriptor.request;
      return {
        label: req?.label ? item[req.label] : item,
        group: req?.group ? item[req.group] : null,
        value: req?.value ? item[req.value] : item,
      };
    }

    private doRequest(filter: string | null): Observable<any[]> {
      let result: Observable<any[]> = of([]);
      if (this.descriptor?.request) {
        const context = { ...this.context, __searchQuery: filter };
        result = this.data.doRequest(this.descriptor.request, context).pipe(
          map(items => items?.map((x: any) => this.convertItemToOption(x)) || []),
        );
      }
      return result.pipe(
        map(items => [...this.descriptor?.options || [], ...items]),
        map(items => !filter || !this.descriptor?.searchable
          ? items
          : items.filter(item => item.label.toLocaleUpperCase().indexOf(filter.toLocaleUpperCase()) !== -1)
        )
      );
    }
}
