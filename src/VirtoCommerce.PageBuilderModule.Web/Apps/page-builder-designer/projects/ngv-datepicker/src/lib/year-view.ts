/**
 * @license
 * Copyright Google LLC All Rights Reserved.
 *
 * Use of this source code is governed by an MIT-style license that can be
 * found in the LICENSE file at https://angular.io/license
 */

import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  ViewEncapsulation,
  computed,
  effect,
  inject,
  input,
  isDevMode,
  output,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {MAT_DATE_FORMATS} from '@angular/material/core';
import {Directionality} from '@angular/cdk/bidi';
import {DateAdapter, MatDateFormats} from './core';
import {
  MatCalendarBody,
  MatCalendarCell,
  MatCalendarUserEvent,
  MatCalendarCellClassFunction,
} from './calendar-body';
import {createMissingDateImplError} from './datepicker-errors';
import {DateRange} from './date-selection-model';
import {DateFilterFn} from './datepicker-input-base';

type CalendarSelection<D> = DateRange<D> | D | null;

/**
 * An internal component used to display a single year in the datepicker.
 * @docs-private
 */
@Component({
    selector: 'mat-year-view',
    templateUrl: 'year-view.html',
    exportAs: 'matYearView',
    imports: [MatCalendarBody],
    encapsulation: ViewEncapsulation.None,
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class MatYearView<D> {
  readonly _changeDetectorRef = inject(ChangeDetectorRef);
  private readonly _dateFormats = inject<MatDateFormats>(MAT_DATE_FORMATS, {optional: true})!;
  readonly _dateAdapter = inject<DateAdapter<D>>(DateAdapter)!;
  private readonly _dir = inject(Directionality, {optional: true});
  private readonly _destroyRef = inject(DestroyRef);

  /** Flag used to filter out space/enter keyup events that originated outside of the view. */
  private _selectionKeyPressed: boolean = false;

  readonly activeDate = input.required<D>();

  readonly selected = input<CalendarSelection<D>, CalendarSelection<D>>(null, {
    transform: (v: CalendarSelection<D>) => {
      if (v instanceof DateRange) return v;
      return this._dateAdapter.getValidDateOrNull(this._dateAdapter.deserialize(v as D | null));
    }
  });

  readonly minDate = input<D | null, D | null>(null, {
    transform: (v: D | null) => this._dateAdapter.getValidDateOrNull(this._dateAdapter.deserialize(v))
  });

  readonly maxDate = input<D | null, D | null>(null, {
    transform: (v: D | null) => this._dateAdapter.getValidDateOrNull(this._dateAdapter.deserialize(v))
  });

  readonly dateFilter = input<DateFilterFn<D> | null>(null);
  readonly dateClass = input<MatCalendarCellClassFunction<D> | null>(null);

  /** Emits when a new month is selected. */
  readonly selectedChange = output<D>();

  /** Emits the selected month. This doesn't imply a change on the selected date */
  readonly monthSelected = output<D>();

  /** Emits when any date is activated. */
  readonly activeDateChange = output<D>();

  /** The body of calendar table */
  readonly _matCalendarBody = viewChild.required(MatCalendarBody);

  /** Grid of calendar cells representing the months of the year. */
  _months!: MatCalendarCell[][];

  /** The label for this year (e.g. "2017"). */
  _yearLabel!: string;

  /** The month in this year that today falls on. Null if today is in a different year. */
  _todayMonth: number | null = null;

  /**
   * The month in this year that the selected Date falls on.
   * Null if the selected Date is in a different year.
   */
  _selectedMonth: number | null = null;

  // Date overridden by keyboard navigation; null = use clamped activeDate input.
  private readonly _overrideActiveDate = signal<D | null>(null);

  // Effective date for rendering: keyboard-nav override takes priority over the clamped input.
  readonly _effectiveActiveDate = computed(() => {
    const override = this._overrideActiveDate();
    if (override !== null) return override;
    const raw = this.activeDate();
    const valid = this._dateAdapter.getValidDateOrNull(this._dateAdapter.deserialize(raw)) || this._dateAdapter.today();
    return this._dateAdapter.clampDate(valid, this.minDate(), this.maxDate());
  });

  constructor() {
    if (isDevMode()) {
      if (!this._dateAdapter) {
        throw createMissingDateImplError('DateAdapter');
      }
      if (!this._dateFormats) {
        throw createMissingDateImplError('MAT_DATE_FORMATS');
      }
    }

    // Clear keyboard-nav override whenever the external activeDate input changes.
    effect(() => {
      this.activeDate();
      untracked(() => this._overrideActiveDate.set(null));
    }, { allowSignalWrites: true });

    // Re-initialize whenever effective date or any display input changes.
    effect(() => this._init());

    this._dateAdapter.localeChanges
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe(() => this._init());
  }

  /** Handles when a new month is selected. */
  _monthSelected(event: MatCalendarUserEvent<number>) {
    const month = event.value;
    const activeDate = this._effectiveActiveDate();
    const normalizedDate = this._dateAdapter.createDate(
      this._dateAdapter.getYear(activeDate),
      month,
      1,
    );

    this.monthSelected.emit(normalizedDate);

    const daysInMonth = this._dateAdapter.getNumDaysInMonth(normalizedDate);

    this.selectedChange.emit(
      this._dateAdapter.createDate(
        this._dateAdapter.getYear(activeDate),
        month,
        Math.min(this._dateAdapter.getDate(activeDate), daysInMonth),
        this._dateAdapter.getHours(activeDate),
        this._dateAdapter.getMinutes(activeDate),
        this._dateAdapter.getSeconds(activeDate),
        this._dateAdapter.getMilliseconds(activeDate),
      ),
    );
  }

  /** Handles keydown events on the calendar body when calendar is in year view. */
  _handleCalendarBodyKeydown(event: KeyboardEvent): void {
    const oldActiveDate = this._effectiveActiveDate();
    const isRtl = this._isRtl();

    switch (event.key) {
      case 'ArrowLeft':
        this._setActiveDateToOverride(this._dateAdapter.addCalendarMonths(oldActiveDate, isRtl ? 1 : -1));
        break;
      case 'ArrowRight':
        this._setActiveDateToOverride(this._dateAdapter.addCalendarMonths(oldActiveDate, isRtl ? -1 : 1));
        break;
      case 'ArrowUp':
        this._setActiveDateToOverride(this._dateAdapter.addCalendarMonths(oldActiveDate, -4));
        break;
      case 'ArrowDown':
        this._setActiveDateToOverride(this._dateAdapter.addCalendarMonths(oldActiveDate, 4));
        break;
      case 'Home':
        this._setActiveDateToOverride(this._dateAdapter.addCalendarMonths(
          oldActiveDate,
          -this._dateAdapter.getMonth(oldActiveDate),
        ));
        break;
      case 'End':
        this._setActiveDateToOverride(this._dateAdapter.addCalendarMonths(
          oldActiveDate,
          11 - this._dateAdapter.getMonth(oldActiveDate),
        ));
        break;
      case 'PageUp':
        this._setActiveDateToOverride(this._dateAdapter.addCalendarYears(
          oldActiveDate,
          event.altKey ? -10 : -1,
        ));
        break;
      case 'PageDown':
        this._setActiveDateToOverride(this._dateAdapter.addCalendarYears(
          oldActiveDate,
          event.altKey ? 10 : 1,
        ));
        break;
      case 'Enter':
      case ' ':
        // Note that we only prevent the default action here while the selection happens in
        // `keyup` below. We can't do the selection here, because it can cause the calendar to
        // reopen if focus is restored immediately. We also can't call `preventDefault` on `keyup`
        // because it's too late (see #23305).
        this._selectionKeyPressed = true;
        break;
      default:
        // Don't prevent default or focus active cell on keys that we don't explicitly handle.
        return;
    }

    if (this._dateAdapter.compareDate(oldActiveDate, this._effectiveActiveDate())) {
      this.activeDateChange.emit(this._effectiveActiveDate());
    }

    this._focusActiveCell();
    // Prevent unexpected default actions such as form submission.
    event.preventDefault();
  }

  /** Handles keyup events on the calendar body when calendar is in year view. */
  _handleCalendarBodyKeyup(event: KeyboardEvent): void {
    if (event.key === ' ' || event.key === 'Enter') {
      if (this._selectionKeyPressed) {
        this._monthSelected({value: this._dateAdapter.getMonth(this._effectiveActiveDate()), event});
      }

      this._selectionKeyPressed = false;
    }
  }

  /** Initializes this year view. */
  _init() {
    const activeDate = this._effectiveActiveDate();
    this._setSelectedMonth(this.selected());
    this._todayMonth = this._getMonthInCurrentYear(this._dateAdapter.today());
    this._yearLabel = this._dateAdapter.getYearName(activeDate);

    let monthNames = this._dateAdapter.getMonthNames('short');
    // First row of months only contains 5 elements so we can fit the year label on the same row.
    this._months = [
      [0, 1, 2, 3],
      [4, 5, 6, 7],
      [8, 9, 10, 11],
    ].map(row => row.map(month => this._createCellForMonth(month, monthNames[month])));
    this._changeDetectorRef.markForCheck();
  }

  /** Focuses the active cell after the microtask queue is empty. */
  _focusActiveCell() {
    this._matCalendarBody()._focusActiveCell();
  }

  /**
   * Gets the month in this year that the given Date falls on.
   * Returns null if the given Date is in another year.
   */
  private _getMonthInCurrentYear(date: D | null) {
    return date && this._dateAdapter.getYear(date) == this._dateAdapter.getYear(this._effectiveActiveDate())
      ? this._dateAdapter.getMonth(date)
      : null;
  }

  /** Creates an MatCalendarCell for the given month. */
  private _createCellForMonth(month: number, monthName: string) {
    const activeDate = this._effectiveActiveDate();
    const date = this._dateAdapter.createDate(this._dateAdapter.getYear(activeDate), month, 1);
    const ariaLabel = this._dateAdapter.format(date, this._dateFormats.display.monthYearA11yLabel);
    const dateClass = this.dateClass();
    const cellClasses = dateClass ? dateClass(date, 'year') : undefined;

    return new MatCalendarCell(
      month,
      monthName.toLocaleUpperCase(),
      ariaLabel,
      this._shouldEnableMonth(month),
      cellClasses,
    );
  }

  /** Whether the given month is enabled. */
  private _shouldEnableMonth(month: number) {
    const activeYear = this._dateAdapter.getYear(this._effectiveActiveDate());

    if (
      month === undefined ||
      month === null ||
      this._isYearAndMonthAfterMaxDate(activeYear, month) ||
      this._isYearAndMonthBeforeMinDate(activeYear, month)
    ) {
      return false;
    }

    const dateFilter = this.dateFilter();
    if (!dateFilter) {
      return true;
    }

    const firstOfMonth = this._dateAdapter.createDate(activeYear, month, 1);

    // If any date in the month is enabled count the month as enabled.
    for (
      let date = firstOfMonth;
      this._dateAdapter.getMonth(date) == month;
      date = this._dateAdapter.addCalendarDays(date, 1)
    ) {
      if (dateFilter(date, 'month')) {
        return true;
      }
    }

    return false;
  }

  /**
   * Tests whether the combination month/year is after this.maxDate, considering
   * just the month and year of this.maxDate
   */
  private _isYearAndMonthAfterMaxDate(year: number, month: number) {
    const maxDate = this.maxDate();
    if (maxDate) {
      const maxYear = this._dateAdapter.getYear(maxDate);
      const maxMonth = this._dateAdapter.getMonth(maxDate);

      return year > maxYear || (year === maxYear && month > maxMonth);
    }

    return false;
  }

  /**
   * Tests whether the combination month/year is before this.minDate, considering
   * just the month and year of this.minDate
   */
  private _isYearAndMonthBeforeMinDate(year: number, month: number) {
    const minDate = this.minDate();
    if (minDate) {
      const minYear = this._dateAdapter.getYear(minDate);
      const minMonth = this._dateAdapter.getMonth(minDate);

      return year < minYear || (year === minYear && month < minMonth);
    }

    return false;
  }

  /** Determines whether the user has the RTL layout direction. */
  private _isRtl() {
    return this._dir && this._dir.value === 'rtl';
  }

  /** Sets the currently-selected month based on a model value. */
  private _setSelectedMonth(value: CalendarSelection<D>) {
    if (value instanceof DateRange) {
      this._selectedMonth =
        this._getMonthInCurrentYear(value.start) || this._getMonthInCurrentYear(value.end);
    } else {
      this._selectedMonth = this._getMonthInCurrentYear(value);
    }
  }

  /** Sets the active date via keyboard navigation with clamping. */
  private _setActiveDateToOverride(date: D): void {
    const valid = this._dateAdapter.getValidDateOrNull(this._dateAdapter.deserialize(date)) || this._dateAdapter.today();
    const clamped = this._dateAdapter.clampDate(valid, this.minDate(), this.maxDate());
    this._overrideActiveDate.set(clamped);
  }
}
