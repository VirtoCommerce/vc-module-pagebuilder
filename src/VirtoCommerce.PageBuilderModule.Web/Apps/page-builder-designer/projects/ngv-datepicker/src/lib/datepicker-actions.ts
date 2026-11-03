/**
 * @license
 * Copyright Google LLC All Rights Reserved.
 *
 * Use of this source code is governed by an MIT-style license that can be
 * found in the LICENSE file at https://angular.io/license
 */

import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  Directive,
  TemplateRef,
  ViewContainerRef,
  ViewEncapsulation,
  afterNextRender,
  inject,
  viewChild,
} from '@angular/core';
import {TemplatePortal} from '@angular/cdk/portal';
import {MatDatepickerBase, MatDatepickerControl} from './datepicker-base';

/** Button that will close the datepicker and assign the current selection to the data model. */
@Directive({
  selector: '[matDatepickerApply], [matDateRangePickerApply]',
  host: {'(click)': '_applySelection()'},
})
export class MatDatepickerApply {
  private readonly _datepicker = inject<MatDatepickerBase<MatDatepickerControl<unknown>, unknown, unknown>>(MatDatepickerBase);

  _applySelection() {
    this._datepicker._applyPendingSelection();
    this._datepicker.close();
  }
}

/** Button that will close the datepicker and discard the current selection. */
@Directive({
  selector: '[matDatepickerCancel], [matDateRangePickerCancel]',
  host: {'(click)': '_datepicker.close()'},
})
export class MatDatepickerCancel {
  readonly _datepicker = inject<MatDatepickerBase<MatDatepickerControl<unknown>, unknown, unknown>>(MatDatepickerBase);
}

/**
 * Container that can be used to project a row of action buttons
 * to the bottom of a datepicker or date range picker.
 */
@Component({
    selector: 'mat-datepicker-actions, mat-date-range-picker-actions',
    styleUrls: ['datepicker-actions.scss'],
    imports: [],
    template: `
    <ng-template>
      <div class="mat-datepicker-actions">
        <ng-content></ng-content>
      </div>
    </ng-template>
  `,
    changeDetection: ChangeDetectionStrategy.OnPush,
    encapsulation: ViewEncapsulation.None
})
export class MatDatepickerActions {
  private readonly _datepicker = inject<MatDatepickerBase<MatDatepickerControl<unknown>, unknown, unknown>>(MatDatepickerBase);
  private readonly _viewContainerRef = inject(ViewContainerRef);
  private readonly _destroyRef = inject(DestroyRef);
  readonly _template = viewChild.required<TemplateRef<unknown>>(TemplateRef);
  private _portal!: TemplatePortal;

  constructor() {
    afterNextRender(() => {
      this._portal = new TemplatePortal(this._template(), this._viewContainerRef);
      this._datepicker.registerActions(this._portal);
    });

    this._destroyRef.onDestroy(() => {
      this._datepicker.removeActions(this._portal);

      // Needs to be null checked since we initialize it in afterNextRender.
      if (this._portal && this._portal.isAttached) {
        this._portal.detach();
      }
    });
  }
}
