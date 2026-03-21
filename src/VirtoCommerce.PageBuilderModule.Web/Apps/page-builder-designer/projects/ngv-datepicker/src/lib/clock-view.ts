import {
  AfterContentInit,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  ElementRef,
  HostListener,
  Input,
  ViewEncapsulation,
  afterNextRender,
  computed,
  inject,
  input,
  output,
} from '@angular/core';
import {MAT_DATE_FORMATS} from '@angular/material/core';
import {
  DateAdapter,
  MatDateFormats,
} from './core';
import {MatCalendarCellClassFunction, MatCalendarUserEvent} from './calendar-body';
import {createMissingDateImplError} from './datepicker-errors';
import {DateFilterFn} from './datepicker-input-base';
import {MatCalendarView} from './calendar.types';

export const CLOCK_RADIUS = 50;
export const CLOCK_INNER_RADIUS = 27.5;
export const CLOCK_OUTER_RADIUS = 41.25;
export const CLOCK_TICK_RADIUS = 7.0833;

export type ClockView = 'hour' | 'minute';

/**
 * A clock that is used as part of the datepicker.
 * @docs-private
 */
@Component({
    selector: 'mat-clock-view',
    templateUrl: 'clock-view.html',
    exportAs: 'matClockView',
    imports: [],
    encapsulation: ViewEncapsulation.None,
    changeDetection: ChangeDetectionStrategy.OnPush,
    host: {
        role: 'clock',
        '(mousedown)': '_handleMousedown($event)'
    },
    preserveWhitespaces: false
})
export class MatClockView<D> implements AfterContentInit {
  private readonly _changeDetectorRef = inject(ChangeDetectorRef);
  private readonly _element = inject(ElementRef);
  public readonly _dateAdapter = inject<DateAdapter<D>>(DateAdapter, {optional: true})!;
  private readonly _dateFormats = inject<MatDateFormats>(MAT_DATE_FORMATS, {optional: true})!;
  private readonly _destroyRef = inject(DestroyRef);

  /**
   * The time to display in this clock view. (the rest is ignored)
   */
  @Input()
  get activeDate(): D {
    return this._activeDate;
  }
  set activeDate(value: D) {
    const oldActiveDate = this._activeDate;
    const validDate =
      this._getValidDateOrNull(this._dateAdapter.deserialize(value)) ||
      this._dateAdapter.today();
    this._activeDate = this._dateAdapter.clampDate(
      validDate,
      this.minDate,
      this.maxDate
    );

    if (
      oldActiveDate &&
      this._dateAdapter.compareDate(oldActiveDate, this._activeDate)
    ) {
      this._init();
    }
  }
  private _activeDate!: D;

  readonly activeDateChange = output<D>();

  // The currently selected date.
  @Input()
  get selected(): D | null {
    return this._selected;
  }
  set selected(value: D | null) {
    this._selected = this._getValidDateOrNull(
      this._dateAdapter.deserialize(value)
    );
  }
  private _selected: D | null = null;

  /** The minimum selectable date. */
  @Input()
  get minDate(): D | null {
    return this._minDate;
  }
  set minDate(value: D | null) {
    this._minDate = this._getValidDateOrNull(
      this._dateAdapter.deserialize(value)
    );
  }
  private _minDate: D | null = null;

  /** The maximum selectable date. */
  @Input()
  get maxDate(): D | null {
    return this._maxDate;
  }
  set maxDate(value: D | null) {
    this._maxDate = this._getValidDateOrNull(
      this._dateAdapter.deserialize(value)
    );
  }
  private _maxDate: D | null = null;

  // A function used to filter which dates are selectable.
  readonly dateFilter = input<DateFilterFn<D> | undefined>(undefined);

  /** Function that can be used to add custom CSS classes to dates. */
  readonly dateClass = input<MatCalendarCellClassFunction<D> | null>(null);

  readonly clockStep = input(1);

  readonly twelveHour = input(false);

  readonly currentView = input.required<MatCalendarView>();

  readonly currentViewChange = output<MatCalendarView>();

  /** Emits when a new date is selected. */
  readonly selectedChange = output<D | null>();

  /** Emits when an hour is selected (before transitioning to minute view). */
  readonly hourSelected = output<D>();

  /** Emits when any date is selected. */
  readonly _userSelection = output<MatCalendarUserEvent<D | null>>();

  @HostListener('window:resize')
  updateSize() {
    const { offsetWidth, offsetHeight } = this._element.nativeElement;
    this._size = (offsetWidth < offsetHeight ? offsetWidth : offsetHeight) * 0.9;
    this._changeDetectorRef.detectChanges();
  }

  // Hours and Minutes representing the clock view.
  _hours: any[] = [];
  _minutes: any[] = [];

  _draggingMouse: boolean = false;
  _selectedHour: number | null = null;
  _selectedMinute: number | null = null;
  _anteMeridian: boolean = true;
  _size: number = 0;

  private mouseMoveListener: (event: any) => void;
  private mouseUpListener: (event: MouseEvent | TouchEvent) => void;

  readonly inHourView = computed(() => this.currentView() === 'hour');

  get _hand(): any {
    this._selectedHour = this._dateAdapter.getHours(this.activeDate);
    this._selectedMinute = this._dateAdapter.getMinutes(this.activeDate);
    let radius = CLOCK_OUTER_RADIUS;
    let deg = 0;

    if (this.inHourView()) {
      const outer = this.twelveHour() || this._selectedHour >= 0 && this._selectedHour < 12;
      radius = outer ? CLOCK_OUTER_RADIUS : CLOCK_INNER_RADIUS;
      deg = Math.round(this._selectedHour * (360 / (24 / 2)));
    } else {
      deg = Math.round(this._selectedMinute! * (360 / 60));
    }

    return {
      transform: `rotate(${deg}deg)`,
      height: `${radius}%`,
      'margin-top': `${50 - radius}%`,
      transition: this._draggingMouse ? 'none' : 'all 300ms ease',
    };
  }

  constructor() {
    if (!this._dateAdapter) {
      throw createMissingDateImplError('DateAdapter');
    }
    if (!this._dateFormats) {
      throw createMissingDateImplError('MAT_DATE_FORMATS');
    }

    this.mouseMoveListener = (event: any) => {
      this._handleMousemove(event);
    };
    this.mouseUpListener = (event: MouseEvent | TouchEvent) => {
      this._handleMouseup(event);
    };

    afterNextRender(() => this.updateSize());

    this._destroyRef.onDestroy(() => {
      document.removeEventListener('mousemove', this.mouseMoveListener);
      document.removeEventListener('touchmove', this.mouseMoveListener);
      document.removeEventListener('mouseup', this.mouseUpListener);
      document.removeEventListener('touchend', this.mouseUpListener);
    });
  }

  ngAfterContentInit() {
    this._init();
  }

  // Handles mousedown events on the clock body.
  _handleMousedown(event: any) {
    this._draggingMouse = true;
    document.addEventListener('mousemove', this.mouseMoveListener);
    document.addEventListener('touchmove', this.mouseMoveListener);
    document.addEventListener('mouseup', this.mouseUpListener);
    document.addEventListener('touchend', this.mouseUpListener);
    this.setTime(event);
  }

  _handleMousemove(event: any) {
    event.preventDefault();
    this.setTime(event);
  }

  _handleMouseup(event: MouseEvent | TouchEvent) {
    this._draggingMouse = false;
    document.removeEventListener('mousemove', this.mouseMoveListener);
    document.removeEventListener('touchmove', this.mouseMoveListener);
    document.removeEventListener('mouseup', this.mouseUpListener);
    document.removeEventListener('touchend', this.mouseUpListener);

    const dateFilter = this.dateFilter();
    if (dateFilter && !dateFilter(this.activeDate, <any>this.currentView())) {
      return;
    }

    if (this.inHourView()) {
      // we refresh the valid minutes
      this.currentViewChange.emit('minute');
      this.selectedChange.emit(this.activeDate);
      this.hourSelected.emit(this.activeDate);
      this._init();
    } else {
      this._userSelection.emit(<any>{ value: this.activeDate, event });
    }
  }

  // Initializes this clock view.
  _init() {
    this._hours.length = 0;
    this._minutes.length = 0;

    const dateFilter = this.dateFilter();
    const dateClass = this.dateClass();

    if (this.twelveHour()) {
      this._buildTwelveHourCells(dateFilter, dateClass);
    } else {
      this._buildTwentyFourHourCells(dateFilter, dateClass);
    }
    this._buildMinuteCells(dateFilter, dateClass);

    this._changeDetectorRef.markForCheck();
  }

  private _buildTwelveHourCells(dateFilter: DateFilterFn<D> | undefined, dateClass: MatCalendarCellClassFunction<D> | null) {
    const hourNames = this._dateAdapter.getHourNames();
    this._anteMeridian = this._dateAdapter.getHours(this.activeDate) < 12;

    for (let i = 0; i < hourNames.length / 2; i++) {
      const radian = (i / 6) * Math.PI;
      const hour = this._anteMeridian ? i : i + 12;
      const date = this._dateAdapter.createDate(
        this._dateAdapter.getYear(this.activeDate),
        this._dateAdapter.getMonth(this.activeDate),
        this._dateAdapter.getDate(this.activeDate),
        hour, 0, 0, 0
      );
      this._hours.push({
        value: hour,
        displayValue: i === 0 ? hourNames[12] : hourNames[i],
        enabled: !dateFilter || dateFilter(date, 'hour'),
        cssClasses: dateClass ? dateClass(date, 'hour') : undefined,
        top: CLOCK_RADIUS - Math.cos(radian) * CLOCK_OUTER_RADIUS - CLOCK_TICK_RADIUS,
        left: CLOCK_RADIUS + Math.sin(radian) * CLOCK_OUTER_RADIUS - CLOCK_TICK_RADIUS,
      });
    }
  }

  private _buildTwentyFourHourCells(dateFilter: DateFilterFn<D> | undefined, dateClass: MatCalendarCellClassFunction<D> | null) {
    const hourNames = this._dateAdapter.getHourNames();

    for (let i = 0; i < hourNames.length; i++) {
      const radian = (i / 6) * Math.PI;
      const outer = i > 0 && i < 13;
      const radius = outer ? CLOCK_OUTER_RADIUS : CLOCK_INNER_RADIUS;
      let hour: number;
      if (i % 12) {
        hour = i;
      } else {
        hour = i === 0 ? 12 : 0;
      }
      const date = this._dateAdapter.createDate(
        this._dateAdapter.getYear(this.activeDate),
        this._dateAdapter.getMonth(this.activeDate),
        this._dateAdapter.getDate(this.activeDate),
        hour, 0, 0, 0
      );
      this._hours.push({
        value: hour,
        displayValue: hourNames[hour],
        enabled: !dateFilter || dateFilter(date, 'hour'),
        cssClasses: dateClass ? dateClass(date, 'hour') : undefined,
        top: CLOCK_RADIUS - Math.cos(radian) * radius - CLOCK_TICK_RADIUS,
        left: CLOCK_RADIUS + Math.sin(radian) * radius - CLOCK_TICK_RADIUS,
        fontSize: outer ? '' : '80%',
      });
    }
  }

  private _buildMinuteCells(dateFilter: DateFilterFn<D> | undefined, dateClass: MatCalendarCellClassFunction<D> | null) {
    const minuteNames = this._dateAdapter.getMinuteNames();

    for (let i = 0; i < minuteNames.length; i += 5) {
      const radian = (i / 30) * Math.PI;
      const date = this._dateAdapter.createDate(
        this._dateAdapter.getYear(this.activeDate),
        this._dateAdapter.getMonth(this.activeDate),
        this._dateAdapter.getDate(this.activeDate),
        this._dateAdapter.getHours(this.activeDate),
        i, 0, 0
      );
      this._minutes.push({
        value: i,
        displayValue: i === 0 ? '00' : minuteNames[i],
        enabled: !dateFilter || dateFilter(date, 'minute'),
        cssClasses: dateClass ? dateClass(date, 'minute') : undefined,
        top: CLOCK_RADIUS - Math.cos(radian) * CLOCK_OUTER_RADIUS - CLOCK_TICK_RADIUS,
        left: CLOCK_RADIUS + Math.sin(radian) * CLOCK_OUTER_RADIUS - CLOCK_TICK_RADIUS,
      });
    }
  }

  // Set Time
  private setTime(event: any) {
    const trigger = this._element.nativeElement;
    const triggerRect = trigger.getBoundingClientRect();
    const width = trigger.offsetWidth;
    const height = trigger.offsetHeight;
    const pageX = event.pageX !== undefined ? event.pageX : event.touches[0].pageX;
    const pageY = event.pageY !== undefined ? event.pageY : event.touches[0].pageY;
    const x = width / 2 - (pageX - triggerRect.left - window.pageXOffset);
    const y = height / 2 - (pageY - triggerRect.top - window.pageYOffset);
    const clockStep = this.clockStep();

    let unitDivider: number;
    if (this.inHourView()) {
      unitDivider = 6;
    } else {
      unitDivider = clockStep ? 30 / clockStep : 30;
    }
    const unit = Math.PI / unitDivider;
    const z = Math.sqrt(x * x + y * y);
    const avg = (width * (CLOCK_OUTER_RADIUS / 100) + width * (CLOCK_INNER_RADIUS / 100)) / 2;
    const outer = this.inHourView() && z > avg - 16 /* button radius */;

    let radian = Math.atan2(-x, y);
    if (radian < 0) {
      radian = Math.PI * 2 + radian;
    }
    const rawValue = Math.round(radian / unit);

    const date = this.inHourView()
      ? this._applyHourValue(rawValue, outer)
      : this._applyMinuteValue(rawValue, clockStep);

    // validate if the resulting value is disabled and do not take action
    const dateFilter = this.dateFilter();
    if (dateFilter && !dateFilter(date, <any>this.currentView())) {
      return;
    }

    // we don't want to re-render the clock
    this._activeDate = date;
    this.selectedChange.emit(this.activeDate);
  }

  private _applyHourValue(rawValue: number, outer: boolean): D {
    let value = rawValue === 12 ? 0 : rawValue;
    if (this.twelveHour()) {
      value = this._anteMeridian ? value : value + 12;
    } else {
      value = outer ? value : value + 12;
    }
    return this._dateAdapter.setHours(this._dateAdapter.clone(this.activeDate), value);
  }

  private _applyMinuteValue(rawValue: number, clockStep: number): D {
    let value = clockStep ? rawValue * clockStep : rawValue;
    if (value === 60) {
      value = 0;
    }
    return this._dateAdapter.setMinutes(this._dateAdapter.clone(this.activeDate), value);
  }

  _focusActiveCell() {
    // duck-typing contract with calendar body, should be removed once we have a common base class for both calendar and clock
  }

  /**
   * @param obj The object to check.
   * @returns The given object if it is both a date instance and valid, otherwise null.
   */
  private _getValidDateOrNull(obj: any): D | null {
    return this._dateAdapter.isDateInstance(obj) && this._dateAdapter.isValid(obj) ? obj : null;
  }
}
