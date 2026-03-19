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
import {Directionality} from '@angular/cdk/bidi';
import {DateAdapter} from './core';
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

/**
 * An internal component used to display a year selector in the datepicker.
 * @docs-private
 */
@Component({
    selector: 'mat-multi-year-view',
    templateUrl: 'multi-year-view.html',
    exportAs: 'matMultiYearView',
    imports: [MatCalendarBody],
    encapsulation: ViewEncapsulation.None,
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class MatMultiYearView<D> implements OnInit {
  private readonly _changeDetectorRef = inject(ChangeDetectorRef);
  readonly _dateAdapter = inject<DateAdapter<D>>(DateAdapter)!;
  private readonly _dir = inject(Directionality, {optional: true});
  private readonly _destroyRef = inject(DestroyRef);

  /** consts moved as inputs */
  @Input() yearsPerPage = 24;

  @Input() yearsPerRow = 4;

  /** Flag used to filter out space/enter keyup events that originated outside of the view. */
  private _selectionKeyPressed: boolean = false;

  /** The date to display in this multi-year view (everything other than the year is ignored). */
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

    if (
      !isSameMultiYearView(
        this._dateAdapter,
        oldActiveDate,
        this._activeDate,
        this.minDate,
        this.maxDate,
        this.yearsPerPage,
      )
    ) {
      this._init();
    }
  }
  private _activeDate: D;

  /** The currently selected date. */
  @Input()
  get selected(): DateRange<D> | D | null {
    return this._selected;
  }
  set selected(value: DateRange<D> | D | null) {
    if (value instanceof DateRange) {
      this._selected = value;
    } else {
      this._selected = this._dateAdapter.getValidDateOrNull(this._dateAdapter.deserialize(value));
    }

    this._setSelectedYear(value);
  }
  private _selected: DateRange<D> | D | null = null;

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

  /** Emits when a new year is selected. */
  readonly selectedChange = output<D>();

  /** Emits the selected year. This doesn't imply a change on the selected date */
  readonly yearSelected = output<D>();

  /** Emits when any date is activated. */
  readonly activeDateChange = output<D>();

  /** The body of calendar table */
  readonly _matCalendarBody = viewChild.required(MatCalendarBody);

  /** Grid of calendar cells representing the currently displayed years. */
  _years!: MatCalendarCell[][];

  /** The year that today falls on. */
  _todayYear!: number;

  /** The year of the selected date. Null if the selected date is null. */
  _selectedYear!: number | null;

  constructor() {
    if (!this._dateAdapter && isDevMode()) {
      throw createMissingDateImplError('DateAdapter');
    }

    this._activeDate = this._dateAdapter.today();
  }

  ngOnInit(): void {
    this._dateAdapter.localeChanges
      .pipe(startWith(null), takeUntilDestroyed(this._destroyRef))
      .subscribe(() => this._init());
  }

  /** Initializes this multi-year view. */
  _init() {
    this._todayYear = this._dateAdapter.getYear(this._dateAdapter.today());

    // We want a range years such that we maximize the number of
    // enabled dates visible at once. This prevents issues where the minimum year
    // is the last item of a page OR the maximum year is the first item of a page.

    // The offset from the active year to the "slot" for the starting year is the
    // *actual* first rendered year in the multi-year view.
    const activeYear = this._dateAdapter.getYear(this._activeDate);
    const minYearOfPage =
      activeYear - getActiveOffset(this._dateAdapter, this.activeDate, this.minDate, this.maxDate, this.yearsPerPage);

    this._years = [];
    for (let i = 0, row: number[] = []; i < this.yearsPerPage; i++) {
      row.push(minYearOfPage + i);
      if (row.length == this.yearsPerRow) {
        this._years.push(row.map(year => this._createCellForYear(year)));
        row = [];
      }
    }
    this._changeDetectorRef.markForCheck();
  }

  /** Handles when a new year is selected. */
  _yearSelected(event: MatCalendarUserEvent<number>) {
    const year = event.value;
    this.yearSelected.emit(this._dateAdapter.createDate(year, 0, 1));
    let month = this._dateAdapter.getMonth(this.activeDate);
    let daysInMonth = this._dateAdapter.getNumDaysInMonth(
      this._dateAdapter.createDate(year, month, 1),
    );
    this.selectedChange.emit(
      this._dateAdapter.createDate(
        year,
        month,
        Math.min(this._dateAdapter.getDate(this.activeDate), daysInMonth),
        this._dateAdapter.getHours(this.activeDate),
        this._dateAdapter.getMinutes(this.activeDate),
        this._dateAdapter.getSeconds(this.activeDate),
        this._dateAdapter.getMilliseconds(this.activeDate),
      ),
    );
  }

  /** Handles keydown events on the calendar body when calendar is in multi-year view. */
  _handleCalendarBodyKeydown(event: KeyboardEvent): void {
    const oldActiveDate = this._activeDate;
    const isRtl = this._isRtl();

    switch (event.key) {
      case 'ArrowLeft':
        this.activeDate = this._dateAdapter.addCalendarYears(this._activeDate, isRtl ? 1 : -1);
        break;
      case 'ArrowRight':
        this.activeDate = this._dateAdapter.addCalendarYears(this._activeDate, isRtl ? -1 : 1);
        break;
      case 'ArrowUp':
        this.activeDate = this._dateAdapter.addCalendarYears(this._activeDate, -this.yearsPerRow);
        break;
      case 'ArrowDown':
        this.activeDate = this._dateAdapter.addCalendarYears(this._activeDate, this.yearsPerRow);
        break;
      case 'Home':
        this.activeDate = this._dateAdapter.addCalendarYears(
          this._activeDate,
          -getActiveOffset(this._dateAdapter, this.activeDate, this.minDate, this.maxDate, this.yearsPerPage),
        );
        break;
      case 'End':
        this.activeDate = this._dateAdapter.addCalendarYears(
          this._activeDate,
          this.yearsPerPage -
            getActiveOffset(this._dateAdapter, this.activeDate, this.minDate, this.maxDate, this.yearsPerPage) -
            1,
        );
        break;
      case 'PageUp':
        this.activeDate = this._dateAdapter.addCalendarYears(
          this._activeDate,
          event.altKey ? -this.yearsPerPage * 10 : -this.yearsPerPage,
        );
        break;
      case 'PageDown':
        this.activeDate = this._dateAdapter.addCalendarYears(
          this._activeDate,
          event.altKey ? this.yearsPerPage * 10 : this.yearsPerPage,
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

  /** Handles keyup events on the calendar body when calendar is in multi-year view. */
  _handleCalendarBodyKeyup(event: KeyboardEvent): void {
    if (event.key === ' ' || event.key === 'Enter') {
      if (this._selectionKeyPressed) {
        this._yearSelected({value: this._dateAdapter.getYear(this._activeDate), event});
      }

      this._selectionKeyPressed = false;
    }
  }

  _getActiveCell(): number {
    return getActiveOffset(this._dateAdapter, this.activeDate, this.minDate, this.maxDate, this.yearsPerPage);
  }

  /** Focuses the active cell after the microtask queue is empty. */
  _focusActiveCell() {
    this._matCalendarBody()._focusActiveCell();
  }

  /** Creates an MatCalendarCell for the given year. */
  private _createCellForYear(year: number) {
    const date = this._dateAdapter.createDate(year, 0, 1);
    const yearName = this._dateAdapter.getYearName(date);
    const cellClasses = this.dateClass ? this.dateClass(date, 'multi-year') : undefined;

    return new MatCalendarCell(year, yearName, yearName, this._shouldEnableYear(year), cellClasses);
  }

  /** Whether the given year is enabled. */
  private _shouldEnableYear(year: number) {
    // disable if the year is greater than maxDate lower than minDate
    if (
      year === undefined ||
      year === null ||
      (this.maxDate && year > this._dateAdapter.getYear(this.maxDate)) ||
      (this.minDate && year < this._dateAdapter.getYear(this.minDate))
    ) {
      return false;
    }

    // enable if it reaches here and there's no filter defined
    if (!this.dateFilter) {
      return true;
    }

    const firstOfYear = this._dateAdapter.createDate(year, 0, 1);

    // If any date in the year is enabled count the year as enabled.
    for (
      let date = firstOfYear;
      this._dateAdapter.getYear(date) == year;
      date = this._dateAdapter.addCalendarDays(date, 1)
    ) {
      if (this.dateFilter(date, 'year')) {
        return true;
      }
    }

    return false;
  }

  /** Determines whether the user has the RTL layout direction. */
  private _isRtl() {
    return this._dir && this._dir.value === 'rtl';
  }

  /** Sets the currently-highlighted year based on a model value. */
  private _setSelectedYear(value: DateRange<D> | D | null) {
    this._selectedYear = null;

    if (value instanceof DateRange) {
      const displayValue = value.start || value.end;

      if (displayValue) {
        this._selectedYear = this._dateAdapter.getYear(displayValue);
      }
    } else if (value) {
      this._selectedYear = this._dateAdapter.getYear(value);
    }
  }
}

export function isSameMultiYearView<D>(
  dateAdapter: DateAdapter<D>,
  date1: D,
  date2: D,
  minDate: D | null,
  maxDate: D | null,
  yearsPerPage: number
): boolean {
  const year1 = dateAdapter.getYear(date1);
  const year2 = dateAdapter.getYear(date2);
  const startingYear = getStartingYear(dateAdapter, minDate, maxDate, yearsPerPage);
  return (
    Math.floor((year1 - startingYear) / yearsPerPage) ===
    Math.floor((year2 - startingYear) / yearsPerPage)
  );
}

/**
 * When the multi-year view is first opened, the active year will be in view.
 * So we compute how many years are between the active year and the *slot* where our
 * "startingYear" will render when paged into view.
 */
export function getActiveOffset<D>(
  dateAdapter: DateAdapter<D>,
  activeDate: D,
  minDate: D | null,
  maxDate: D | null,
  yearsPerPage: number
): number {
  const activeYear = dateAdapter.getYear(activeDate);
  return euclideanModulo(activeYear - getStartingYear(dateAdapter, minDate, maxDate, yearsPerPage), yearsPerPage);
}

/**
 * We pick a "starting" year such that either the maximum year would be at the end
 * or the minimum year would be at the beginning of a page.
 */
function getStartingYear<D>(
  dateAdapter: DateAdapter<D>,
  minDate: D | null,
  maxDate: D | null,
  yearsPerPage: number,
): number {
  let startingYear = 0;
  if (maxDate) {
    const maxYear = dateAdapter.getYear(maxDate);
    startingYear = maxYear - yearsPerPage + 1;
  } else if (minDate) {
    startingYear = dateAdapter.getYear(minDate);
  }
  return startingYear;
}

/** Gets remainder that is non-negative, even if first number is negative */
function euclideanModulo(a: number, b: number): number {
  return ((a % b) + b) % b;
}
