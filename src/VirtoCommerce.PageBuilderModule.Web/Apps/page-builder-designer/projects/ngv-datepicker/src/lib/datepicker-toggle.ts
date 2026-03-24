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
    ViewEncapsulation,
    computed,
    contentChild,
    effect,
    inject,
    input,
    untracked,
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
        '[class.mat-datepicker-toggle-active]': 'datepicker()?.opened',
        '[class.mat-accent]': 'datepicker()?.color === "accent"',
        '[class.mat-warn]': 'datepicker()?.color === "warn"',
        // Used by the test harness to tie this toggle to its datepicker.
        '[attr.data-mat-calendar]': 'datepicker()?.id ?? null',
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

  /** Datepicker instance that the button will toggle. */
  readonly datepicker = input<MatDatepickerPanel<MatDatepickerControl<any>, D> | undefined>(undefined, { alias: 'for' });

  /** Tabindex for the toggle (explicit binding). */
  readonly tabIndex = input<number | null>(null);

  /** Default tabindex from the host element attribute, read once at construction. */
  private _hostTabIndex: number | null = null;

  /** Effective tabindex: explicit binding takes priority over host attribute. */
  readonly _effectiveTabIndex = computed(() => this.tabIndex() ?? this._hostTabIndex);

  /** Screenreader label for the button. */
  readonly ariaLabel = input('', { alias: 'aria-label' });

  /** Whether the toggle button is disabled. */
  protected readonly _disabledInput = input<boolean | undefined, boolean | string | undefined>(
    undefined,
    {
      alias: 'disabled',
      transform: (v): boolean | undefined => v === undefined ? undefined : coerceBooleanProperty(v),
    }
  );

  get disabled(): boolean {
    const explicit = this._disabledInput();
    if (explicit === undefined && this.datepicker()) {
      return this.datepicker()!.disabled;
    }
    return explicit ?? false;
  }

  /** Whether ripples on the toggle should be disabled. */
  readonly disableRipple = input(false);

  /** Custom icon set by the consumer. */
  readonly _customIcon = contentChild(MatDatepickerToggleIcon);

  /** Underlying button element. */
  readonly _button = viewChild.required<MatButton>('button');

  constructor() {
    const defaultTabIndex = inject(new HostAttributeToken('tabindex'), { optional: true });
    const parsedTabIndex = Number(defaultTabIndex);
    this._hostTabIndex = parsedTabIndex || parsedTabIndex === 0 ? parsedTabIndex : null;

    this._destroyRef.onDestroy(() => this._stateChanges.unsubscribe());

    // Re-subscribe to state changes whenever the datepicker input changes.
    effect(() => {
      this.datepicker();
      untracked(() => this._watchStateChanges());
    });
  }

  _open(event: Event): void {
    if (this.datepicker() && !this.disabled) {
      this.datepicker()!.open();
      event.stopPropagation();
    }
  }

  private _watchStateChanges() {
    const dp = this.datepicker();
    const datepickerStateChanged = dp ? dp.stateChanges : observableOf();
    const inputStateChanged =
      dp && dp.datepickerInput
        ? dp.datepickerInput.stateChanges
        : observableOf();
    const datepickerToggled = dp
      ? merge(outputToObservable(dp.openedStream), outputToObservable(dp.closedStream))
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
