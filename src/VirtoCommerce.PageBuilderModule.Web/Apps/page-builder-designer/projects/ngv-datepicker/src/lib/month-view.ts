/**
 * @license
 * Copyright Google LLC All Rights Reserved.
 *
 * Use of this source code is governed by an MIT-style license that can be
 * found in the LICENSE file at https://angular.io/license
 */

import {hasModifierKey} from '@angular/cdk/keycodes';
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
import {
  MatDateRangeSelectionStrategy,
  MAT_DATE_RANGE_SELECTION_STRATEGY,
} from './date-range-selection-strategy';
import {DateFilterFn} from './datepicker-input-base';

type CalendarSelection<D> = DateRange<D> | D | null;

const DAYS_PER_WEEK = 7;

/**
 * An internal component used to display a single month in the datepicker.
 * @docs-private
 */
@Component({
    selector: 'mat-month-view',
    templateUrl: 'month-view.html',
    exportAs: 'matMonthView',
    imports: [MatCalendarBody],
    encapsulation: ViewEncapsulation.None,
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class MatMonthView<D> {
  readonly _changeDetectorRef = inject(ChangeDetectorRef);
  private readonly _dateFormats = inject<MatDateFormats>(MAT_DATE_FORMATS, {optional: true})!;
  readonly _dateAdapter = inject<DateAdapter<D>>(DateAdapter)!;
  private readonly _dir = inject(Directionality, {optional: true});
  private readonly _rangeStrategy = inject<MatDateRangeSelectionStrategy<D>>(
    MAT_DATE_RANGE_SELECTION_STRATEGY, {optional: true},
  );
  private readonly _destroyRef = inject(DestroyRef);

  /** Flag used to filter out space/enter keyup events that originated outside of the view. */
  private _selectionKeyPressed!: boolean;

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
  readonly comparisonStart = input<D | null>(null);
  readonly comparisonEnd = input<D | null>(null);

  /** Emits when a new date is selected. */
  readonly selectedChange = output<D | null>();

  /** Emits when any date is selected. */
  readonly _userSelection = output<MatCalendarUserEvent<D | null>>();

  /** Emits when any date is activated. */
  readonly activeDateChange = output<D>();

  /** The body of calendar table */
  readonly _matCalendarBody = viewChild.required(MatCalendarBody);

  /** The label for this month (e.g. "January 2017"). */
  _monthLabel!: string;

  /** Grid of calendar cells representing the dates of the month. */
  _weeks!: MatCalendarCell[][];

  /** The number of blank cells in the first row before the 1st of the month. */
  _firstWeekOffset!: number;

  /** Start value of the currently-shown date range. */
  _rangeStart: number | null = null;

  /** End value of the currently-shown date range. */
  _rangeEnd: number | null = null;

  /** Start value of the currently-shown comparison date range. */
  _comparisonRangeStart: number | null = null;

  /** End value of the currently-shown comparison date range. */
  _comparisonRangeEnd: number | null = null;

  /** Start of the preview range. */
  _previewStart: number | null = null;

  /** End of the preview range. */
  _previewEnd: number | null = null;

  /** Whether the user is currently selecting a range of dates. */
  _isRange: boolean = false;

  /** The date of the month that today falls on. Null if today is in another month. */
  _todayDate: number | null = null;

  /** The names of the weekdays. */
  _weekdays: {long: string; narrow: string}[] | null = null;

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

  /** Handles when a new date is selected. */
  _dateSelected(event: MatCalendarUserEvent<number>) {
    const date = event.value;
    const activeDate = this._effectiveActiveDate();
    const selectedYear = this._dateAdapter.getYear(activeDate);
    const selectedMonth = this._dateAdapter.getMonth(activeDate);
    const selectedDate = this._dateAdapter.createDate(
      selectedYear,
      selectedMonth,
      date,
      this._dateAdapter.getHours(activeDate),
      this._dateAdapter.getMinutes(activeDate),
      this._dateAdapter.getSeconds(activeDate),
      this._dateAdapter.getMilliseconds(activeDate),
    );
    const selected = this.selected();
    let rangeStartDate: number | null;
    let rangeEndDate: number | null;

    if (selected instanceof DateRange) {
      rangeStartDate = this._getDateInCurrentMonth(selected.start);
      rangeEndDate = this._getDateInCurrentMonth(selected.end);
    } else {
      rangeStartDate = rangeEndDate = this._getDateInCurrentMonth(selected);
    }

    if (rangeStartDate !== date || rangeEndDate !== date) {
      this.selectedChange.emit(selectedDate);
    }

    this._userSelection.emit({value: selectedDate, event: event.event});
    this._previewStart = this._previewEnd = null;
    this._changeDetectorRef.markForCheck();
  }

  /** Handles keydown events on the calendar body when calendar is in month view. */
  _handleCalendarBodyKeydown(event: KeyboardEvent): void {
    const oldActiveDate = this._effectiveActiveDate();
    const isRtl = this._isRtl();

    switch (event.key) {
      case 'ArrowLeft':
        this._setActiveDateToOverride(this._dateAdapter.addCalendarDays(oldActiveDate, isRtl ? 1 : -1));
        break;
      case 'ArrowRight':
        this._setActiveDateToOverride(this._dateAdapter.addCalendarDays(oldActiveDate, isRtl ? -1 : 1));
        break;
      case 'ArrowUp':
        this._setActiveDateToOverride(this._dateAdapter.addCalendarDays(oldActiveDate, -7));
        break;
      case 'ArrowDown':
        this._setActiveDateToOverride(this._dateAdapter.addCalendarDays(oldActiveDate, 7));
        break;
      case 'Home':
        this._setActiveDateToOverride(this._dateAdapter.addCalendarDays(
          oldActiveDate,
          1 - this._dateAdapter.getDate(oldActiveDate),
        ));
        break;
      case 'End':
        this._setActiveDateToOverride(this._dateAdapter.addCalendarDays(
          oldActiveDate,
          this._dateAdapter.getNumDaysInMonth(oldActiveDate) -
            this._dateAdapter.getDate(oldActiveDate),
        ));
        break;
      case 'PageUp':
        this._setActiveDateToOverride(event.altKey
          ? this._dateAdapter.addCalendarYears(oldActiveDate, -1)
          : this._dateAdapter.addCalendarMonths(oldActiveDate, -1));
        break;
      case 'PageDown':
        this._setActiveDateToOverride(event.altKey
          ? this._dateAdapter.addCalendarYears(oldActiveDate, 1)
          : this._dateAdapter.addCalendarMonths(oldActiveDate, 1));
        break;
      case 'Enter':
      case ' ':
        this._selectionKeyPressed = true;

        if (this._canSelect(this._effectiveActiveDate())) {
          // Prevent unexpected default actions such as form submission.
          // Note that we only prevent the default action here while the selection happens in
          // `keyup` below. We can't do the selection here, because it can cause the calendar to
          // reopen if focus is restored immediately. We also can't call `preventDefault` on `keyup`
          // because it's too late (see #23305).
          event.preventDefault();
        }
        return;
      case 'Escape':
        // Abort the current range selection if the user presses escape mid-selection.
        if (this._previewEnd != null && !hasModifierKey(event)) {
          this._previewStart = this._previewEnd = null;
          this.selectedChange.emit(null);
          this._userSelection.emit({value: null, event});
          event.preventDefault();
          event.stopPropagation(); // Prevents the overlay from closing.
        }
        return;
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

  /** Handles keyup events on the calendar body when calendar is in month view. */
  _handleCalendarBodyKeyup(event: KeyboardEvent): void {
    if (event.key === ' ' || event.key === 'Enter') {
      const activeDate = this._effectiveActiveDate();
      if (this._selectionKeyPressed && this._canSelect(activeDate)) {
        this._dateSelected({value: this._dateAdapter.getDate(activeDate), event});
      }

      this._selectionKeyPressed = false;
    }
  }

  /** Initializes this month view. */
  _init() {
    const activeDate = this._effectiveActiveDate();
    this._setRanges(this.selected());
    this._todayDate = this._getCellCompareValue(this._dateAdapter.today());
    this._monthLabel = this._dateFormats.display.monthLabel
      ? this._dateAdapter.format(activeDate, this._dateFormats.display.monthLabel)
      : this._dateAdapter
          .getMonthNames('short')
          [this._dateAdapter.getMonth(activeDate)].toLocaleUpperCase();

    let firstOfMonth = this._dateAdapter.createDate(
      this._dateAdapter.getYear(activeDate),
      this._dateAdapter.getMonth(activeDate),
      1,
    );
    this._firstWeekOffset =
      (DAYS_PER_WEEK +
        this._dateAdapter.getDayOfWeek(firstOfMonth) -
        this._dateAdapter.getFirstDayOfWeek()) %
      DAYS_PER_WEEK;

    this._initWeekdays();
    this._createWeekCells();
    this._changeDetectorRef.markForCheck();
  }

  /** Focuses the active cell after the microtask queue is empty. */
  _focusActiveCell(movePreview?: boolean) {
    this._matCalendarBody()._focusActiveCell(movePreview);
  }

  /** Called when the user has activated a new cell and the preview needs to be updated. */
  _previewChanged({event, value: cell}: MatCalendarUserEvent<MatCalendarCell<D> | null>) {
    if (this._rangeStrategy) {
      // We can assume that this will be a range, because preview
      // events aren't fired for single date selections.
      const value = cell ? cell.rawValue! : null;
      const previewRange = this._rangeStrategy.createPreview(
        value,
        this.selected() as DateRange<D>,
        event,
      );
      this._previewStart = this._getCellCompareValue(previewRange.start);
      this._previewEnd = this._getCellCompareValue(previewRange.end);

      // Note that here we need to use `detectChanges`, rather than `markForCheck`, because
      // the way `_focusActiveCell` is set up at the moment makes it fire at the wrong time
      // when navigating one month back using the keyboard which will cause this handler
      // to throw a "changed after checked" error when updating the preview state.
      this._changeDetectorRef.detectChanges();
    }
  }

  /** Initializes the weekdays. */
  private _initWeekdays() {
    const firstDayOfWeek = this._dateAdapter.getFirstDayOfWeek();
    const narrowWeekdays = this._dateAdapter.getDayOfWeekNames('narrow');
    const longWeekdays = this._dateAdapter.getDayOfWeekNames('long');

    // Rotate the labels for days of the week based on the configured first day of the week.
    let weekdays = longWeekdays.map((long, i) => {
      return {long, narrow: narrowWeekdays[i]};
    });
    this._weekdays = weekdays.slice(firstDayOfWeek).concat(weekdays.slice(0, firstDayOfWeek));
  }

  /** Creates MatCalendarCells for the dates in this month. */
  private _createWeekCells() {
    const activeDate = this._effectiveActiveDate();
    const daysInMonth = this._dateAdapter.getNumDaysInMonth(activeDate);
    const dateNames = this._dateAdapter.getDateNames();
    this._weeks = [[]];
    for (let i = 0; i < daysInMonth; i++) {
      const totalOffset = i + this._firstWeekOffset;
      if (totalOffset > 0 && totalOffset % DAYS_PER_WEEK === 0) {
        this._weeks.push([]);
      }
      const date = this._dateAdapter.createDate(
        this._dateAdapter.getYear(activeDate),
        this._dateAdapter.getMonth(activeDate),
        i + 1,
      );
      const enabled = this._shouldEnableDate(date);
      const ariaLabel = this._dateAdapter.format(date, this._dateFormats.display.dateA11yLabel);
      const dateClass = this.dateClass();
      const cellClasses = dateClass ? dateClass(date, 'month') : undefined;

      this._weeks[this._weeks.length - 1].push(
        new MatCalendarCell<D>(
          i + 1,
          dateNames[i],
          ariaLabel,
          enabled,
          cellClasses,
          this._getCellCompareValue(date)!,
          date,
        ),
      );
    }
  }

  /** Date filter for the month */
  private _shouldEnableDate(date: D): boolean {
    const minDate = this.minDate();
    const maxDate = this.maxDate();
    const dateFilter = this.dateFilter();
    return (
      !!date &&
      (!minDate || this._dateAdapter.compareDate(date, minDate) >= 0) &&
      (!maxDate || this._dateAdapter.compareDate(date, maxDate) <= 0) &&
      (!dateFilter || dateFilter(date, 'day'))
    );
  }

  /**
   * Gets the date in this month that the given Date falls on.
   * Returns null if the given Date is in another month.
   */
  private _getDateInCurrentMonth(date: D | null): number | null {
    return date && this._hasSameMonthAndYear(date, this._effectiveActiveDate())
      ? this._dateAdapter.getDate(date)
      : null;
  }

  /** Checks whether the 2 dates are non-null and fall within the same month of the same year. */
  private _hasSameMonthAndYear(d1: D | null, d2: D | null): boolean {
    return !!(
      d1 &&
      d2 &&
      this._dateAdapter.getMonth(d1) == this._dateAdapter.getMonth(d2) &&
      this._dateAdapter.getYear(d1) == this._dateAdapter.getYear(d2)
    );
  }

  /** Gets the value that will be used to one cell to another. */
  private _getCellCompareValue(date: D | null): number | null {
    if (date) {
      // We use the time since the Unix epoch to compare dates in this view, rather than the
      // cell values, because we need to support ranges that span across multiple months/years.
      const year = this._dateAdapter.getYear(date);
      const month = this._dateAdapter.getMonth(date);
      const day = this._dateAdapter.getDate(date);
      return new Date(year, month, day).getTime();
    }

    return null;
  }

  /** Determines whether the user has the RTL layout direction. */
  private _isRtl() {
    return this._dir && this._dir.value === 'rtl';
  }

  /** Sets the current range based on a model value. */
  private _setRanges(selectedValue: CalendarSelection<D>) {
    if (selectedValue instanceof DateRange) {
      this._rangeStart = this._getCellCompareValue(selectedValue.start);
      this._rangeEnd = this._getCellCompareValue(selectedValue.end);
      this._isRange = true;
    } else {
      this._rangeStart = this._rangeEnd = this._getCellCompareValue(selectedValue);
      this._isRange = false;
    }

    this._comparisonRangeStart = this._getCellCompareValue(this.comparisonStart());
    this._comparisonRangeEnd = this._getCellCompareValue(this.comparisonEnd());
  }

  /** Gets whether a date can be selected in the month view. */
  private _canSelect(date: D) {
    const dateFilter = this.dateFilter();
    return !dateFilter || dateFilter(date);
  }

  /** Sets the active date via keyboard navigation with clamping. */
  private _setActiveDateToOverride(date: D): void {
    const valid = this._dateAdapter.getValidDateOrNull(this._dateAdapter.deserialize(date)) || this._dateAdapter.today();
    const clamped = this._dateAdapter.clampDate(valid, this.minDate(), this.maxDate());
    this._overrideActiveDate.set(clamped);
  }
}
