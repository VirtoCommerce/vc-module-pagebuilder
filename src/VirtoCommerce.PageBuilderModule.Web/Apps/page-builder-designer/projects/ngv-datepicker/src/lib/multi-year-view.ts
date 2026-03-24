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
import {Directionality} from '@angular/cdk/bidi';
import {DateAdapter} from './core';
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
export class MatMultiYearView<D> {
  private readonly _changeDetectorRef = inject(ChangeDetectorRef);
  readonly _dateAdapter = inject<DateAdapter<D>>(DateAdapter)!;
  private readonly _dir = inject(Directionality, {optional: true});
  private readonly _destroyRef = inject(DestroyRef);

  readonly yearsPerPage = input(24);
  readonly yearsPerRow = input(4);

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

  // Date overridden by keyboard navigation; null = use clamped activeDate input.
  private readonly _overrideActiveDate = signal<D | null>(null);

  // Effective date for rendering: keyboard-nav override takes priority over the clamped input.
  private readonly _effectiveActiveDate = computed(() => {
    const override = this._overrideActiveDate();
    if (override !== null) return override;
    const raw = this.activeDate();
    const valid = this._dateAdapter.getValidDateOrNull(this._dateAdapter.deserialize(raw)) || this._dateAdapter.today();
    return this._dateAdapter.clampDate(valid, this.minDate(), this.maxDate());
  });

  constructor() {
    if (!this._dateAdapter && isDevMode()) {
      throw createMissingDateImplError('DateAdapter');
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

  /** Initializes this multi-year view. */
  _init() {
    const activeDate = this._effectiveActiveDate();
    this._setSelectedYear(this.selected());
    this._todayYear = this._dateAdapter.getYear(this._dateAdapter.today());

    // We want a range years such that we maximize the number of
    // enabled dates visible at once. This prevents issues where the minimum year
    // is the last item of a page OR the maximum year is the first item of a page.

    // The offset from the active year to the "slot" for the starting year is the
    // *actual* first rendered year in the multi-year view.
    const activeYear = this._dateAdapter.getYear(activeDate);
    const minYearOfPage =
      activeYear - getActiveOffset(this._dateAdapter, activeDate, this.minDate(), this.maxDate(), this.yearsPerPage());

    this._years = [];
    for (let i = 0, row: number[] = []; i < this.yearsPerPage(); i++) {
      row.push(minYearOfPage + i);
      if (row.length == this.yearsPerRow()) {
        this._years.push(row.map(year => this._createCellForYear(year)));
        row = [];
      }
    }
    this._changeDetectorRef.markForCheck();
  }

  /** Handles when a new year is selected. */
  _yearSelected(event: MatCalendarUserEvent<number>) {
    const year = event.value;
    const activeDate = this._effectiveActiveDate();
    this.yearSelected.emit(this._dateAdapter.createDate(year, 0, 1));
    let month = this._dateAdapter.getMonth(activeDate);
    let daysInMonth = this._dateAdapter.getNumDaysInMonth(
      this._dateAdapter.createDate(year, month, 1),
    );
    this.selectedChange.emit(
      this._dateAdapter.createDate(
        year,
        month,
        Math.min(this._dateAdapter.getDate(activeDate), daysInMonth),
        this._dateAdapter.getHours(activeDate),
        this._dateAdapter.getMinutes(activeDate),
        this._dateAdapter.getSeconds(activeDate),
        this._dateAdapter.getMilliseconds(activeDate),
      ),
    );
  }

  /** Handles keydown events on the calendar body when calendar is in multi-year view. */
  _handleCalendarBodyKeydown(event: KeyboardEvent): void {
    const oldActiveDate = this._effectiveActiveDate();
    const isRtl = this._isRtl();

    switch (event.key) {
      case 'ArrowLeft':
        this._setActiveDateToOverride(this._dateAdapter.addCalendarYears(oldActiveDate, isRtl ? 1 : -1));
        break;
      case 'ArrowRight':
        this._setActiveDateToOverride(this._dateAdapter.addCalendarYears(oldActiveDate, isRtl ? -1 : 1));
        break;
      case 'ArrowUp':
        this._setActiveDateToOverride(this._dateAdapter.addCalendarYears(oldActiveDate, -this.yearsPerRow()));
        break;
      case 'ArrowDown':
        this._setActiveDateToOverride(this._dateAdapter.addCalendarYears(oldActiveDate, this.yearsPerRow()));
        break;
      case 'Home':
        this._setActiveDateToOverride(this._dateAdapter.addCalendarYears(
          oldActiveDate,
          -getActiveOffset(this._dateAdapter, oldActiveDate, this.minDate(), this.maxDate(), this.yearsPerPage()),
        ));
        break;
      case 'End':
        this._setActiveDateToOverride(this._dateAdapter.addCalendarYears(
          oldActiveDate,
          this.yearsPerPage() -
            getActiveOffset(this._dateAdapter, oldActiveDate, this.minDate(), this.maxDate(), this.yearsPerPage()) -
            1,
        ));
        break;
      case 'PageUp':
        this._setActiveDateToOverride(this._dateAdapter.addCalendarYears(
          oldActiveDate,
          event.altKey ? -this.yearsPerPage() * 10 : -this.yearsPerPage(),
        ));
        break;
      case 'PageDown':
        this._setActiveDateToOverride(this._dateAdapter.addCalendarYears(
          oldActiveDate,
          event.altKey ? this.yearsPerPage() * 10 : this.yearsPerPage(),
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

  /** Handles keyup events on the calendar body when calendar is in multi-year view. */
  _handleCalendarBodyKeyup(event: KeyboardEvent): void {
    if (event.key === ' ' || event.key === 'Enter') {
      if (this._selectionKeyPressed) {
        this._yearSelected({value: this._dateAdapter.getYear(this._effectiveActiveDate()), event});
      }

      this._selectionKeyPressed = false;
    }
  }

  _getActiveCell(): number {
    return getActiveOffset(this._dateAdapter, this._effectiveActiveDate(), this.minDate(), this.maxDate(), this.yearsPerPage());
  }

  /** Focuses the active cell after the microtask queue is empty. */
  _focusActiveCell() {
    this._matCalendarBody()._focusActiveCell();
  }

  /** Creates an MatCalendarCell for the given year. */
  private _createCellForYear(year: number) {
    const date = this._dateAdapter.createDate(year, 0, 1);
    const yearName = this._dateAdapter.getYearName(date);
    const dateClass = this.dateClass();
    const cellClasses = dateClass ? dateClass(date, 'multi-year') : undefined;

    return new MatCalendarCell(year, yearName, yearName, this._shouldEnableYear(year), cellClasses);
  }

  /** Whether the given year is enabled. */
  private _shouldEnableYear(year: number) {
    const minDate = this.minDate();
    const maxDate = this.maxDate();
    // disable if the year is greater than maxDate lower than minDate
    if (
      year === undefined ||
      year === null ||
      (maxDate && year > this._dateAdapter.getYear(maxDate)) ||
      (minDate && year < this._dateAdapter.getYear(minDate))
    ) {
      return false;
    }

    // enable if it reaches here and there's no filter defined
    const dateFilter = this.dateFilter();
    if (!dateFilter) {
      return true;
    }

    const firstOfYear = this._dateAdapter.createDate(year, 0, 1);

    // If any date in the year is enabled count the year as enabled.
    for (
      let date = firstOfYear;
      this._dateAdapter.getYear(date) == year;
      date = this._dateAdapter.addCalendarDays(date, 1)
    ) {
      if (dateFilter(date, 'year')) {
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
  private _setSelectedYear(value: CalendarSelection<D>) {
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

  /** Sets the active date via keyboard navigation with clamping. */
  private _setActiveDateToOverride(date: D): void {
    const valid = this._dateAdapter.getValidDateOrNull(this._dateAdapter.deserialize(date)) || this._dateAdapter.today();
    const clamped = this._dateAdapter.clampDate(valid, this.minDate(), this.maxDate());
    this._overrideActiveDate.set(clamped);
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
