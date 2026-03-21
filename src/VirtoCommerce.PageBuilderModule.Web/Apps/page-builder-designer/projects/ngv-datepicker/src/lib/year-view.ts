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
  Input,
  OnInit,
  ViewEncapsulation,
  inject,
  isDevMode,
  output,
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
import {startWith} from 'rxjs/operators';
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
export class MatYearView<D> implements OnInit {
  readonly _changeDetectorRef = inject(ChangeDetectorRef);
  private readonly _dateFormats = inject<MatDateFormats>(MAT_DATE_FORMATS, {optional: true})!;
  readonly _dateAdapter = inject<DateAdapter<D>>(DateAdapter)!;
  private readonly _dir = inject(Directionality, {optional: true});
  private readonly _destroyRef = inject(DestroyRef);

  /** Flag used to filter out space/enter keyup events that originated outside of the view. */
  private _selectionKeyPressed: boolean = false;

  /** The date to display in this year view (everything other than the year is ignored). */
  @Input()
  get activeDate(): D {
    return this._activeDate;
  }
  set activeDate(value: D) {
    let oldActiveDate = this._activeDate;
    const validDate =
      this._dateAdapter.getValidDateOrNull(this._dateAdapter.deserialize(value)) ||
      this._dateAdapter.today();
    this._activeDate = this._dateAdapter.clampDate(validDate, this.minDate, this.maxDate);
    if (this._dateAdapter.getYear(oldActiveDate) !== this._dateAdapter.getYear(this._activeDate)) {
      this._init();
    }
  }
  private _activeDate: D;

  /** The currently selected date. */
  @Input()
  get selected(): CalendarSelection<D> {
    return this._selected;
  }
  set selected(value: CalendarSelection<D>) {
    if (value instanceof DateRange) {
      this._selected = value;
    } else {
      this._selected = this._dateAdapter.getValidDateOrNull(this._dateAdapter.deserialize(value));
    }

    this._setSelectedMonth(value);
  }
  private _selected: CalendarSelection<D> = null;

  /** The minimum selectable date. */
  @Input()
  get minDate(): D | null {
    return this._minDate;
  }
  set minDate(value: D | null) {
    this._minDate = this._dateAdapter.getValidDateOrNull(this._dateAdapter.deserialize(value));
  }
  private _minDate: D | null = null;

  /** The maximum selectable date. */
  @Input()
  get maxDate(): D | null {
    return this._maxDate;
  }
  set maxDate(value: D | null) {
    this._maxDate = this._dateAdapter.getValidDateOrNull(this._dateAdapter.deserialize(value));
  }
  private _maxDate: D | null = null;

  /** A function used to filter which dates are selectable. */
  @Input() dateFilter!: DateFilterFn<D>;

  /** Function that can be used to add custom CSS classes to date cells. */
  @Input() dateClass: MatCalendarCellClassFunction<D> | null = null;

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

  constructor() {
    if (isDevMode()) {
      if (!this._dateAdapter) {
        throw createMissingDateImplError('DateAdapter');
      }
      if (!this._dateFormats) {
        throw createMissingDateImplError('MAT_DATE_FORMATS');
      }
    }

    this._activeDate = this._dateAdapter.today();
  }

  ngOnInit(): void {
    this._dateAdapter.localeChanges
      .pipe(startWith(null), takeUntilDestroyed(this._destroyRef))
      .subscribe(() => this._init());
  }

  /** Handles when a new month is selected. */
  _monthSelected(event: MatCalendarUserEvent<number>) {
    const month = event.value;
    const normalizedDate = this._dateAdapter.createDate(
      this._dateAdapter.getYear(this.activeDate),
      month,
      1,
    );

    this.monthSelected.emit(normalizedDate);

    const daysInMonth = this._dateAdapter.getNumDaysInMonth(normalizedDate);

    this.selectedChange.emit(
      this._dateAdapter.createDate(
        this._dateAdapter.getYear(this.activeDate),
        month,
        Math.min(this._dateAdapter.getDate(this.activeDate), daysInMonth),
        this._dateAdapter.getHours(this.activeDate),
        this._dateAdapter.getMinutes(this.activeDate),
        this._dateAdapter.getSeconds(this.activeDate),
        this._dateAdapter.getMilliseconds(this.activeDate),
      ),
    );
  }

  /** Handles keydown events on the calendar body when calendar is in year view. */
  _handleCalendarBodyKeydown(event: KeyboardEvent): void {
    const oldActiveDate = this._activeDate;
    const isRtl = this._isRtl();

    switch (event.key) {
      case 'ArrowLeft':
        this.activeDate = this._dateAdapter.addCalendarMonths(this._activeDate, isRtl ? 1 : -1);
        break;
      case 'ArrowRight':
        this.activeDate = this._dateAdapter.addCalendarMonths(this._activeDate, isRtl ? -1 : 1);
        break;
      case 'ArrowUp':
        this.activeDate = this._dateAdapter.addCalendarMonths(this._activeDate, -4);
        break;
      case 'ArrowDown':
        this.activeDate = this._dateAdapter.addCalendarMonths(this._activeDate, 4);
        break;
      case 'Home':
        this.activeDate = this._dateAdapter.addCalendarMonths(
          this._activeDate,
          -this._dateAdapter.getMonth(this._activeDate),
        );
        break;
      case 'End':
        this.activeDate = this._dateAdapter.addCalendarMonths(
          this._activeDate,
          11 - this._dateAdapter.getMonth(this._activeDate),
        );
        break;
      case 'PageUp':
        this.activeDate = this._dateAdapter.addCalendarYears(
          this._activeDate,
          event.altKey ? -10 : -1,
        );
        break;
      case 'PageDown':
        this.activeDate = this._dateAdapter.addCalendarYears(
          this._activeDate,
          event.altKey ? 10 : 1,
        );
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

    if (this._dateAdapter.compareDate(oldActiveDate, this.activeDate)) {
      this.activeDateChange.emit(this.activeDate);
    }

    this._focusActiveCell();
    // Prevent unexpected default actions such as form submission.
    event.preventDefault();
  }

  /** Handles keyup events on the calendar body when calendar is in year view. */
  _handleCalendarBodyKeyup(event: KeyboardEvent): void {
    if (event.key === ' ' || event.key === 'Enter') {
      if (this._selectionKeyPressed) {
        this._monthSelected({value: this._dateAdapter.getMonth(this._activeDate), event});
      }

      this._selectionKeyPressed = false;
    }
  }

  /** Initializes this year view. */
  _init() {
    this._setSelectedMonth(this.selected);
    this._todayMonth = this._getMonthInCurrentYear(this._dateAdapter.today());
    this._yearLabel = this._dateAdapter.getYearName(this.activeDate);

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
    return date && this._dateAdapter.getYear(date) == this._dateAdapter.getYear(this.activeDate)
      ? this._dateAdapter.getMonth(date)
      : null;
  }

  /** Creates an MatCalendarCell for the given month. */
  private _createCellForMonth(month: number, monthName: string) {
    const date = this._dateAdapter.createDate(this._dateAdapter.getYear(this.activeDate), month, 1);
    const ariaLabel = this._dateAdapter.format(date, this._dateFormats.display.monthYearA11yLabel);
    const cellClasses = this.dateClass ? this.dateClass(date, 'year') : undefined;

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
    const activeYear = this._dateAdapter.getYear(this.activeDate);

    if (
      month === undefined ||
      month === null ||
      this._isYearAndMonthAfterMaxDate(activeYear, month) ||
      this._isYearAndMonthBeforeMinDate(activeYear, month)
    ) {
      return false;
    }

    if (!this.dateFilter) {
      return true;
    }

    const firstOfMonth = this._dateAdapter.createDate(activeYear, month, 1);

    // If any date in the month is enabled count the month as enabled.
    for (
      let date = firstOfMonth;
      this._dateAdapter.getMonth(date) == month;
      date = this._dateAdapter.addCalendarDays(date, 1)
    ) {
      if (this.dateFilter(date, 'month')) {
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
    if (this.maxDate) {
      const maxYear = this._dateAdapter.getYear(this.maxDate);
      const maxMonth = this._dateAdapter.getMonth(this.maxDate);

      return year > maxYear || (year === maxYear && month > maxMonth);
    }

    return false;
  }

  /**
   * Tests whether the combination month/year is before this.minDate, considering
   * just the month and year of this.minDate
   */
  private _isYearAndMonthBeforeMinDate(year: number, month: number) {
    if (this.minDate) {
      const minYear = this._dateAdapter.getYear(this.minDate);
      const minMonth = this._dateAdapter.getMonth(this.minDate);

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
}
