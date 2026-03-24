/**
 * @license
 * Copyright Google LLC All Rights Reserved.
 *
 * Use of this source code is governed by an MIT-style license that can be
 * found in the LICENSE file at https://angular.io/license
 */

import {DestroyRef, Directive, effect, forwardRef, inject, input, untracked} from '@angular/core';
import {NG_VALIDATORS, NG_VALUE_ACCESSOR, ValidatorFn, Validators} from '@angular/forms';
import {ThemePalette} from '@angular/material/core';
import {MatFormField, MAT_FORM_FIELD} from '@angular/material/form-field';
import {MAT_INPUT_VALUE_ACCESSOR} from '@angular/material/input';
import {Subscription} from 'rxjs';
import {MatDatepickerInputBase, DateFilterFn} from './datepicker-input-base';
import {MatDatepickerControl, MatDatepickerPanel} from './datepicker-base';
import {DateSelectionModelChange} from './date-selection-model';

/** @docs-private */
export const MAT_DATEPICKER_VALUE_ACCESSOR: any = {
  provide: NG_VALUE_ACCESSOR,
  useExisting: forwardRef(() => MatDatepickerInput),
  multi: true,
};

/** @docs-private */
export const MAT_DATEPICKER_VALIDATORS: any = {
  provide: NG_VALIDATORS,
  useExisting: forwardRef(() => MatDatepickerInput),
  multi: true,
};

/** Directive used to connect an input to a MatDatepicker. */
@Directive({
  selector: 'input[matDatepicker]',
  providers: [
    MAT_DATEPICKER_VALUE_ACCESSOR,
    MAT_DATEPICKER_VALIDATORS,
    {provide: MAT_INPUT_VALUE_ACCESSOR, useExisting: MatDatepickerInput},
  ],
  host: {
    'class': 'mat-datepicker-input',
    '[attr.aria-haspopup]': '_datepicker ? "dialog" : null',
    '[attr.aria-owns]': '(_datepicker?.opened && _datepicker.id) || null',
    '[attr.min]': 'min ? _dateAdapter.toIso8601(min) : null',
    '[attr.max]': 'max ? _dateAdapter.toIso8601(max) : null',
    // Used by the test harness to tie this input to its calendar. We can't depend on
    // `aria-owns` for this, because it's only defined while the calendar is open.
    '[attr.data-mat-calendar]': '_datepicker ? _datepicker.id : null',
    '[disabled]': 'disabled',
      '(input)': '_onInput($any($event.target).value)',
    '(change)': '_onChange()',
    '(blur)': '_onBlur()',
    '(keydown)': '_onKeydown($event)',
  },
  exportAs: 'matDatepickerInput',
})
export class MatDatepickerInput<D>
  extends MatDatepickerInputBase<D | null, D>
  implements MatDatepickerControl<D | null>
{
  private readonly _formField = inject<MatFormField>(MAT_FORM_FIELD, {optional: true});
  private _closedSubscription: {unsubscribe(): void} = Subscription.EMPTY;

  /** The datepicker that this input is associated with. */
  readonly _datepickerInput = input<
    MatDatepickerPanel<MatDatepickerControl<D>, D | null, D> | undefined
  >(undefined, { alias: 'matDatepicker' });

  _datepicker!: MatDatepickerPanel<MatDatepickerControl<D>, D | null, D>;

  /** The minimum valid date. */
  readonly _minInput = input<D | null, D | null>(null, {
    alias: 'min',
    transform: (v: D | null) => this._dateAdapter.getValidDateOrNull(this._dateAdapter.deserialize(v)),
  });

  get min(): D | null { return this._minInput(); }

  /** The maximum valid date. */
  readonly _maxInput = input<D | null, D | null>(null, {
    alias: 'max',
    transform: (v: D | null) => this._dateAdapter.getValidDateOrNull(this._dateAdapter.deserialize(v)),
  });

  get max(): D | null { return this._maxInput(); }

  /** Function that can be used to filter out dates within the datepicker. */
  readonly _dateFilterInput = input<DateFilterFn<D | null> | null>(null, {
    alias: 'matDatepickerFilter',
  });

  get dateFilter(): DateFilterFn<D | null> { return this._dateFilterInput()!; }

  /** The combined form control validator for this input. */
  protected _validator: ValidatorFn | null;

  constructor() {
    super();
    this._validator = Validators.compose(super._getValidators());

    inject(DestroyRef).onDestroy(() => this._closedSubscription.unsubscribe());

    // Register datepicker and subscribe to its closedStream
    effect(() => {
      const datepicker = this._datepickerInput();
      untracked(() => {
        if (datepicker) {
          this._datepicker = datepicker;
          this._closedSubscription.unsubscribe();
          this._closedSubscription = datepicker.closedStream.subscribe(() => this._onTouched());
          this._registerModel(datepicker.registerInput(this));
        }
      });
    });

    // Re-run validators when min/max change
    effect(() => {
      this._minInput();
      this._maxInput();
      untracked(() => this._validatorOnChange());
    });

    // Re-run validators when dateFilter changes
    effect(() => {
      this._dateFilterInput();
      untracked(() => this._validatorOnChange());
    });
  }

  /**
   * Gets the element that the datepicker popup should be connected to.
   * @return The element to connect the popup to.
   */
  getConnectedOverlayOrigin(): import('@angular/core').ElementRef {
    return this._formField ? this._formField.getConnectedOverlayOrigin() : this._elementRef;
  }

  /** Gets the ID of an element that should be used a description for the calendar overlay. */
  getOverlayLabelId(): string | null {
    if (this._formField) {
      return this._formField.getLabelId();
    }

    return this._elementRef.nativeElement.getAttribute('aria-labelledby');
  }

  /** Returns the palette used by the input's form field, if any. */
  getThemePalette(): ThemePalette {
    return this._formField ? this._formField.color : undefined;
  }

  /** Gets the value at which the calendar should start. */
  getStartValue(): D | null {
    return this.value;
  }

  /** Opens the associated datepicker. */
  protected _openPopup(): void {
    if (this._datepicker) {
      this._datepicker.open();
    }
  }

  protected _getValueFromModel(modelValue: D | null): D | null {
    return modelValue;
  }

  protected _assignValueToModel(value: D | null): void {
    if (this._model) {
      this._model.updateSelection(value, this);
    }
  }

  /** Gets the input's minimum date. */
  _getMinDate() {
    return this._minInput();
  }

  /** Gets the input's maximum date. */
  _getMaxDate() {
    return this._maxInput();
  }

  /** Gets the input's date filtering function. */
  protected _getDateFilter() {
    return this._dateFilterInput() ?? undefined;
  }

  protected _shouldHandleChangeEvent(event: DateSelectionModelChange<D>) {
    return event.source !== this;
  }
}
