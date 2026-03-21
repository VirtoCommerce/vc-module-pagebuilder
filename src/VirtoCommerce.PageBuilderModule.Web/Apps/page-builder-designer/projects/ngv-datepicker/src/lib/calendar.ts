/**
 * @license
 * Copyright Google LLC All Rights Reserved.
 *
 * Use of this source code is governed by an MIT-style license that can be
 * found in the LICENSE file at https://angular.io/license
 */

import { ComponentPortal, ComponentType, Portal, CdkPortalOutlet } from '@angular/cdk/portal';
import { CdkMonitorFocus } from '@angular/cdk/a11y';
import { MatButton, MatIconButton } from '@angular/material/button';
import {
  afterNextRender,
  afterEveryRender,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  forwardRef,
  HostBinding,
  inject,
  input,
  Input,
  OnChanges,
  output,
  SimpleChanges,
  viewChild,
  ViewEncapsulation,
  isDevMode,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MAT_DATE_FORMATS } from '@angular/material/core';
import { Subject } from 'rxjs';
import {
  DateAdapter,
  DateUnit,
  MatDateFormats,
} from './core';
import { MatCalendarUserEvent, MatCalendarCellClassFunction } from './calendar-body';
import { MatCalendarType, MatCalendarView } from './calendar.types';
import { matDatepickerAnimations } from './datepicker-animations';
import { createMissingDateImplError } from './datepicker-errors';
import { MatDatepickerIntl } from './datepicker-intl';
import { DateFilterFn } from './datepicker-input-base';
import { MatClockView } from './clock-view';
import { MatMonthView } from './month-view';
import {
  getActiveOffset,
  isSameMultiYearView,
  MatMultiYearView,
} from './multi-year-view';
import { MatYearView } from './year-view';
import { MAT_SINGLE_DATE_SELECTION_MODEL_PROVIDER, DateRange } from './date-selection-model';

/** Counter used to generate unique IDs. */
let uniqueId = 0;

/** Default header for MatCalendar */
@Component({
  selector: 'mat-custom-header',
  templateUrl: 'mat-header.html',
  styleUrls: ['./mat-header.scss'],
  imports: [MatButton, MatIconButton],
  exportAs: 'matCalendarHeader',
  animations: [matDatepickerAnimations.controlActive],
  encapsulation: ViewEncapsulation.None,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MatCalendarHeader<D> {
  readonly _intl = inject(MatDatepickerIntl);
  readonly calendar = inject<MatCalendar<D>>(forwardRef(() => MatCalendar));
  private readonly _dateAdapter = inject<DateAdapter<D>>(DateAdapter, { optional: true })!;
  private readonly _dateFormats = inject<MatDateFormats>(MAT_DATE_FORMATS, { optional: true })!;
  private readonly _changeDetectorRef = inject(ChangeDetectorRef);
  private readonly _destroyRef = inject(DestroyRef);

  _buttonDescriptionId = `mat-calendar-button-${uniqueId++}`;

  @HostBinding('class')
  get getCssClasses(): string {
    const cssClasses: string[] = [`type-${this.calendar.type}`];
    return cssClasses.join(' ');
  }

  _yearButtonText!: string;
  _monthButtonText!: string;
  _monthdayButtonText!: string;
  _dayButtonText!: string;
  _hourButtonText!: string;
  _minuteButtonText!: string;
  _isAM!: boolean;

  constructor() {
    this.updateValues();
    this.calendar.stateChanges
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe(() => this.updateValues());
  }

  updateValues() {
    const activeDate = this.calendar.getDate();

    const day = this._dateAdapter.getDayOfWeek(activeDate);
    let hours = this._dateAdapter.getHours(activeDate);
    this._isAM = hours < 12;
    if (this.calendar.twelveHour()) {
      if (hours === 0) hours = 12;
      else if (hours > 12) hours = hours - 12;
    }
    const minutes = this._dateAdapter.getMinutes(activeDate);

    this._yearButtonText = this._dateAdapter.getYear(activeDate).toString();
    this._monthButtonText = this._dateAdapter.format(activeDate,
      this._dateFormats.display.monthLabel);
    this._monthdayButtonText = this._dateAdapter.format(activeDate,
      this._dateFormats.display.monthDayLabel);
    this._dayButtonText = this._dateAdapter.getDayOfWeekNames('short')[day];
    this._hourButtonText = hours.toString();
    this._minuteButtonText = ('00' + minutes).slice(-2);

    this._changeDetectorRef.markForCheck();
  }

  hasPrevNextBlock(): boolean {
    return !['hour', 'minute'].includes(this.calendar.currentView);
  }

  isControlActive(views: MatCalendarView[]): boolean {
    return views.includes(this.calendar.currentView);
  }

  switchToView(view: MatCalendarView): void {
    this.calendar.currentView = view;
  }

  toggleAmPm(am: boolean): void {
    if (this._isAM !== am) {
      this.calendar.setDate(this._dateAdapter.addCalendarHours(
        this.calendar.getDate(), this._isAM ? 12 : -12));
    }
  }

  /** The label for the current calendar view. */
  get periodButtonText(): string {
    if (this.calendar.currentView == 'month') {
      return this._dateAdapter
        .format(this.calendar.activeDate, this._dateFormats.display.monthYearLabel)
        .toLocaleUpperCase();
    }
    if (this.calendar.currentView == 'year') {
      return this._dateAdapter.getYearName(this.calendar.activeDate);
    }

    // The offset from the active year to the "slot" for the starting year is the
    // *actual* first rendered year in the multi-year view, and the last year is
    // just yearsPerPage - 1 away.
    const activeYear = this._dateAdapter.getYear(this.calendar.activeDate);
    const minYearOfPage =
      activeYear -
      getActiveOffset(
        this._dateAdapter,
        this.calendar.activeDate,
        this.calendar.minDate,
        this.calendar.maxDate,
        this.calendar.yearsPerPage(),
      );
    const maxYearOfPage = minYearOfPage + this.calendar.yearsPerPage() - 1;
    const minYearName = this._dateAdapter.getYearName(
      this._dateAdapter.createDate(minYearOfPage, 0, 1),
    );
    const maxYearName = this._dateAdapter.getYearName(
      this._dateAdapter.createDate(maxYearOfPage, 0, 1),
    );
    return this._intl.formatYearRange(minYearName, maxYearName);
  }

  get monthdayButtonLabel(): string {
    return this.calendar.currentView == 'month'
      ? this._intl.switchToYearViewLabel
      : this._intl.switchToMonthViewLabel;
  }

  get periodButtonLabel(): string {
    return this.calendar.currentView == 'month'
      ? this._intl.switchToMultiYearViewLabel
      : this._intl.switchToMonthViewLabel;
  }

  /** The label for the previous button. */
  get prevButtonLabel(): string {
    return {
      'month': this._intl.prevMonthLabel,
      'year': this._intl.prevYearLabel,
      'multi-year': this._intl.prevMultiYearLabel,
      'hour': '',
      'minute': '',
    }[this.calendar.currentView];
  }

  /** The label for the next button. */
  get nextButtonLabel(): string {
    return {
      'month': this._intl.nextMonthLabel,
      'year': this._intl.nextYearLabel,
      'multi-year': this._intl.nextMultiYearLabel,
      'hour': '',
      'minute': '',
    }[this.calendar.currentView];
  }

  monthdayClicked(): void {
    this.calendar.currentView = this.calendar.currentView == 'month' ? 'year' : 'month';
  }

  currentPeriodDisabled(): boolean {
    return ['year', 'month'].includes(this.calendar.type);
  }

  /** Handles user clicks on the period label. */
  currentPeriodClicked(): void {
    this.calendar.currentView = this.calendar.currentView == 'month' ? 'multi-year' : 'month';
  }

  /** Handles user clicks on the previous button. */
  previousClicked(): void {
    const yearsOffset = this.calendar.currentView == 'year' ? -1 : -this.calendar.yearsPerPage();
    const date = this.calendar.currentView == 'month'
      ? this._dateAdapter.addCalendarMonths(this.calendar.activeDate, -1)
      : this._dateAdapter.addCalendarYears(this.calendar.activeDate, yearsOffset);

    this.calendar.setDate(date);
  }

  /** Handles user clicks on the next button. */
  nextClicked(): void {
    const yearsOffset = this.calendar.currentView == 'year' ? 1 : this.calendar.yearsPerPage();
    const date = this.calendar.currentView == 'month'
      ? this._dateAdapter.addCalendarMonths(this.calendar.activeDate, 1)
      : this._dateAdapter.addCalendarYears(this.calendar.activeDate, yearsOffset);

    this.calendar.setDate(date);
  }

  /** Whether the previous period button is enabled. */
  previousEnabled(): boolean {
    return !this.calendar.minDate || !this._isSameView(this.calendar.activeDate, this.calendar.minDate);
  }

  /** Whether the next period button is enabled. */
  nextEnabled(): boolean {
    return (
      !this.calendar.maxDate || !this._isSameView(this.calendar.activeDate, this.calendar.maxDate)
    );
  }

  /** Whether the two dates represent the same view in the current view mode (month or year). */
  private _isSameView(date1: D, date2: D): boolean {
    if (this.calendar.currentView == 'month') {
      return (
        this._dateAdapter.getYear(date1) == this._dateAdapter.getYear(date2) &&
        this._dateAdapter.getMonth(date1) == this._dateAdapter.getMonth(date2)
      );
    }
    if (this.calendar.currentView == 'year') {
      return this._dateAdapter.getYear(date1) == this._dateAdapter.getYear(date2);
    }
    // Otherwise we are in 'multi-year' view.
    return isSameMultiYearView(
      this._dateAdapter,
      date1,
      date2,
      this.calendar.minDate,
      this.calendar.maxDate,
      this.calendar.yearsPerPage(),
    );
  }
}

/** A calendar that is used as part of the datepicker. */
@Component({
  selector: 'mat-calendar',
  templateUrl: 'calendar.html',
  styleUrls: ['calendar.scss'],
  imports: [CdkPortalOutlet, CdkMonitorFocus, MatClockView, MatMonthView, MatYearView, MatMultiYearView],
  host: {
    'class': 'mat-calendar',
  },
  exportAs: 'matCalendar',
  encapsulation: ViewEncapsulation.None,
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [MAT_SINGLE_DATE_SELECTION_MODEL_PROVIDER]
})
export class MatCalendar<D> implements OnChanges {
  private readonly _dateAdapter = inject<DateAdapter<D>>(DateAdapter, { optional: true })!;
  private readonly _dateFormats = inject<MatDateFormats>(MAT_DATE_FORMATS, { optional: true })!;
  private readonly _changeDetectorRef = inject(ChangeDetectorRef);
  private readonly _destroyRef = inject(DestroyRef);

  /** An input indicating the type of the header component, if set. */
  readonly headerComponent = input<ComponentType<any>>();

  /** A portal containing the header component type for this calendar. */
  _calendarHeaderPortal!: Portal<any>;

  /**
   * Used for scheduling that focus should be moved to the active cell on the next tick.
   * We need to schedule it, rather than do it immediately, because we have to wait
   * for Angular to re-evaluate the view children.
   */
  private _moveFocusOnNextTick = false;

  /** A date representing the period (month or year) to start the calendar in. */
  @Input()
  get startAt(): D | null {
    return this._startAt;
  }
  set startAt(value: D | null) {
    this._startAt = this._dateAdapter.getValidDateOrNull(this._dateAdapter.deserialize(value));
  }
  private _startAt: D | null = null;

  /** The type of value handled by the calendar. */
  @HostBinding('class')
  @Input() type: MatCalendarType = 'date';

  /** Whether the calendar should be started in. */
  readonly startView = input<MatCalendarView>('month');

  /** multi-year inputs */
  readonly yearsPerPage = input(24);

  readonly yearsPerRow = input(4);

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

  /** Function used to filter which dates are selectable. */
  @Input() dateFilter!: DateFilterFn<D>;

  /** Function that can be used to add custom CSS classes to dates. */
  readonly dateClass = input<MatCalendarCellClassFunction<D> | null>(null);

  /** Clock interval */
  readonly clockStep = input<number>(1);

  /** Clock hour format */
  readonly twelveHour = input(false);

  /** Start of the comparison range. */
  readonly comparisonStart = input<D | null>(null);

  /** End of the comparison range. */
  readonly comparisonEnd = input<D | null>(null);

  /** Emits when the currently selected date changes. */
  readonly selectedChange = output<D | null>();

  /**
   * Emits the year chosen in multiyear view.
   * This doesn't imply a change on the selected date.
   */
  readonly yearSelected = output<D>();

  /**
   * Emits the month chosen in year view.
   * This doesn't imply a change on the selected date.
   */
  readonly monthSelected = output<D>();

  /**
   * Emits when the date changes.
   */
  readonly dateChanged = output<D>();

  /**
   * Emits when the current view changes.
   */
  readonly viewChanged = output<MatCalendarView>();

  /** Emits when any date is selected. */
  readonly _userSelection = output<MatCalendarUserEvent<D | null>>();

  /** Reference to the current clock view component. */
  readonly clockView = viewChild<MatClockView<D>>(MatClockView);

  /** Reference to the current month view component. */
  readonly monthView = viewChild<MatMonthView<D>>(MatMonthView);

  /** Reference to the current year view component. */
  readonly yearView = viewChild<MatYearView<D>>(MatYearView);

  /** Reference to the current multi-year view component. */
  readonly multiYearView = viewChild<MatMultiYearView<D>>(MatMultiYearView);

  /**
   * The current active date. This determines which time period is shown and which date is
   * highlighted when using keyboard navigation.
   */
  get activeDate(): D {
    return this._clampedActiveDate;
  }
  set activeDate(value: D) {
    this._clampedActiveDate = this._dateAdapter.clampDate(value, this.minDate, this.maxDate);
    this.stateChanges.next();
    this._changeDetectorRef.markForCheck();
  }
  private _clampedActiveDate!: D;

  /** Whether the calendar is in month view. */
  get currentView(): MatCalendarView {
    return this._currentView;
  }
  set currentView(value: MatCalendarView) {
    const viewChangedResult = this._currentView !== value ? value : null;
    this._currentView = value;
    this._moveFocusOnNextTick = true;
    this._changeDetectorRef.markForCheck();
    if (viewChangedResult) {
      this.viewChanged.emit(viewChangedResult);
    }
  }
  private _currentView!: MatCalendarView;

  /**
   * Emits whenever there is a state change that the header may need to respond to.
   */
  readonly stateChanges = new Subject<void>();

  constructor() {
    if (isDevMode()) {
      if (!this._dateAdapter) {
        throw createMissingDateImplError('DateAdapter');
      }

      if (!this._dateFormats) {
        throw createMissingDateImplError('MAT_DATE_FORMATS');
      }
    }

    const _intl = inject(MatDatepickerIntl);
    _intl.changes
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe(() => {
        this._changeDetectorRef.markForCheck();
        this.stateChanges.next();
      });

    this._destroyRef.onDestroy(() => this.stateChanges.complete());

    afterNextRender(() => {
      this._calendarHeaderPortal = new ComponentPortal(this.headerComponent() || MatCalendarHeader);
      this.activeDate = this.startAt || this._dateAdapter.today();

      // Assign to the private property since we don't want to move focus on init.
      this._currentView =
        this.type === 'year'
          ? 'multi-year'
          : this.type === 'month'
            ? 'year'
            : this.type === 'time' && !['hour', 'minute'].includes(this.startView())
              ? 'hour'
              : this.startView();
    });

    afterEveryRender(() => {
      if (this._moveFocusOnNextTick) {
        this._moveFocusOnNextTick = false;
        this.focusActiveCell();
      }
    });
  }

  ngOnChanges(changes: SimpleChanges) {
    const change = changes['minDate'] || changes['maxDate'] || changes['dateFilter'];

    if (change && !change.firstChange) {
      const view = this._getCurrentViewComponent();

      if (view) {
        // We need to `detectChanges` manually here, because the `minDate`, `maxDate` etc. are
        // passed down to the view via data bindings which won't be up-to-date when we call `_init`.
        this._changeDetectorRef.detectChanges();
        view._init();
      }
    }

    this.stateChanges.next();
  }

  /** Focuses the active date. */
  focusActiveCell() {
    this._getCurrentViewComponent()?._focusActiveCell(false);
  }

  hasOutput(type: MatCalendarType): boolean {
    return this.type.indexOf(type) !== -1;
  }

  getDate(): D {
    return !this.selected || this.selected instanceof DateRange
      ? this.activeDate
      : this.selected;
  }

  getUnit(): DateUnit {
    switch (this.type) {
      case 'date':
        return 'day';
      case 'datetime':
      case 'time':
        return 'minute';
      default:
        return this.type;
    }
  }

  setDate(date: D | null): void {
    if (date === null) {
      return;
    }
    if (!(this.selected instanceof DateRange)) {
      this.selected = date;
    }
    this.activeDate = date;

    this.dateChanged.emit(date);
  }

  /** Updates today's date after an update of the active date */
  updateTodaysDate() {
    this._getCurrentViewComponent()?._init();
  }

  /** Handles date selection in the month view. */
  _dateSelected(event: MatCalendarUserEvent<D | null>): void {
    const date = event.value;

    if (
      this.selected instanceof DateRange ||
      (date && !this._dateAdapter.sameDate(date, this.selected, this.getUnit()))
    ) {
      this.selectedChange.emit(date);
    }

    this._userSelection.emit(event);
  }

  _dateEmit(value: D) {
    this.setDate(value);
    this._userSelection.emit(<any>{ value, event: null });
  }

  /** Handles date selection in the clock view. */
  _hourSelectedInClockView(date: D | null): void {
    this.setDate(date);
    this.selectedChange.emit(date);
  }

  _timeSelectedInClockView(event: MatCalendarUserEvent<D | null>): void {
    this.setDate(event.value!);
    this.selectedChange.emit(event.value);
    this._userSelection.emit(event);
  }

  /** Handles user day selection. */
  _daySelected(event: MatCalendarUserEvent<D | null>): void {
    if (!this.hasOutput('time') || this.selected instanceof DateRange) {
      this.setDate(event.value!);
      this._dateSelected(event);
    } else {
      this.selectedChange.emit(event.value);
      this._goToDateInView(event.value!, 'hour');
    }
  }

  /** Handles year selection in the multiyear view. */
  _yearSelectedInMultiYearView(normalizedYear: D) {
    this.yearSelected.emit(normalizedYear);
  }

  /** Handles month selection in the year view. */
  _monthSelectedInYearView(normalizedMonth: D) {
    this.monthSelected.emit(normalizedMonth);
  }

  /** Handles year/month selection in the multi-year/year views. */
  _goToDateInView(date: D, view: MatCalendarView): void {
    this.setDate(date);
    this.currentView = view;
  }

  /** Returns the component instance that corresponds to the current calendar view. */
  private _getCurrentViewComponent(): MatClockView<D> | MatMonthView<D> | MatYearView<D> | MatMultiYearView<D> | undefined {
    // The return type is explicitly written as a union to ensure that the Closure compiler does
    // not optimize calls to _init(). Without the explicit return type, TypeScript narrows it to
    // only the first component type. See https://github.com/angular/components/issues/22996.
    return this.clockView() || this.monthView() || this.yearView() || this.multiYearView();
  }
}
