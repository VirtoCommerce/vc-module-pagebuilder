/**
 * @license
 * Copyright Google LLC All Rights Reserved.
 *
 * Use of this source code is governed by an MIT-style license that can be
 * found in the LICENSE file at https://angular.io/license
 */

import {coerceBooleanProperty} from '@angular/cdk/coercion';
import {
    ChangeDetectionStrategy,
    ChangeDetectorRef,
    Component,
    DestroyRef,
    Directive,
    HostAttributeToken,
    Input,
    ViewEncapsulation,
    contentChild,
    inject,
    viewChild,
} from '@angular/core';
import {outputToObservable} from '@angular/core/rxjs-interop';
import {MatButton, MatIconButton} from '@angular/material/button';
import {merge, Observable, of as observableOf, Subscription} from 'rxjs';
import {MatDatepickerIntl} from './datepicker-intl';
import {MatDatepickerControl, MatDatepickerPanel} from './datepicker-base';

/** Can be used to override the icon of a `matDatepickerToggle`. */
@Directive({
  selector: '[matDatepickerToggleIcon]',
})
export class MatDatepickerToggleIcon {}

@Component({
    selector: 'mat-datepicker-toggle',
    templateUrl: 'datepicker-toggle.html',
    styleUrls: ['datepicker-toggle.scss'],
    imports: [MatIconButton],
    host: {
        'class': 'mat-datepicker-toggle',
        '[attr.tabindex]': 'null',
        '[class.mat-datepicker-toggle-active]': 'datepicker && datepicker.opened',
        '[class.mat-accent]': 'datepicker && datepicker.color === "accent"',
        '[class.mat-warn]': 'datepicker && datepicker.color === "warn"',
        // Used by the test harness to tie this toggle to its datepicker.
        '[attr.data-mat-calendar]': 'datepicker ? datepicker.id : null',
        // Bind the `click` on the host, rather than the inner `button`, so that we can call
        // `stopPropagation` on it without affecting the user's `click` handlers. We need to stop
        // it so that the input doesn't get focused automatically by the form field (See #21836).
        '(click)': '_open($event)',
    },
    exportAs: 'matDatepickerToggle',
    encapsulation: ViewEncapsulation.None,
    changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MatDatepickerToggle<D> {
  readonly _intl = inject(MatDatepickerIntl);
  private readonly _changeDetectorRef = inject(ChangeDetectorRef);
  private readonly _destroyRef = inject(DestroyRef);

  private _stateChanges = Subscription.EMPTY;
  private _datepicker!: MatDatepickerPanel<MatDatepickerControl<any>, D>;

  /** Datepicker instance that the button will toggle. */
  @Input('for')
  get datepicker(): MatDatepickerPanel<MatDatepickerControl<any>, D> {
    return this._datepicker;
  }
  set datepicker(value: MatDatepickerPanel<MatDatepickerControl<any>, D>) {
    this._datepicker = value;
    this._watchStateChanges();
  }

  /** Tabindex for the toggle. */
  @Input() tabIndex: number | null;

  /** Screenreader label for the button. */
  @Input('aria-label') ariaLabel!: string;

  /** Whether the toggle button is disabled. */
  @Input()
  get disabled(): boolean {
    if (this._disabled === undefined && this.datepicker) {
      return this.datepicker.disabled;
    }
    return !!this._disabled;
  }
  set disabled(value: boolean) {
    this._disabled = coerceBooleanProperty(value);
  }
  private _disabled: boolean = false;

  /** Whether ripples on the toggle should be disabled. */
  @Input() disableRipple!: boolean;

  /** Custom icon set by the consumer. */
  readonly _customIcon = contentChild(MatDatepickerToggleIcon);

  /** Underlying button element. */
  readonly _button = viewChild.required<MatButton>('button');

  constructor() {
    const defaultTabIndex = inject(new HostAttributeToken('tabindex'), { optional: true });
    const parsedTabIndex = Number(defaultTabIndex);
    this.tabIndex = parsedTabIndex || parsedTabIndex === 0 ? parsedTabIndex : null;

    this._destroyRef.onDestroy(() => this._stateChanges.unsubscribe());
  }

  _open(event: Event): void {
    if (this.datepicker && !this.disabled) {
      this.datepicker.open();
      event.stopPropagation();
    }
  }

  private _watchStateChanges() {
    const datepickerStateChanged = this.datepicker ? this.datepicker.stateChanges : observableOf();
    const inputStateChanged =
      this.datepicker && this.datepicker.datepickerInput
        ? this.datepicker.datepickerInput.stateChanges
        : observableOf();
    const datepickerToggled = this.datepicker
      ? merge(outputToObservable(this.datepicker.openedStream), outputToObservable(this.datepicker.closedStream))
      : observableOf();

    this._stateChanges.unsubscribe();
    this._stateChanges = merge(
      this._intl.changes,
      datepickerStateChanged as Observable<void>,
      inputStateChanged,
      datepickerToggled,
    ).subscribe(() => this._changeDetectorRef.markForCheck());
  }
}
