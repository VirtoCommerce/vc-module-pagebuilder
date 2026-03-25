import {
  AfterContentInit,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  ElementRef,
  ViewEncapsulation,
  afterNextRender,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { MAT_DATE_FORMATS } from '@angular/material/core';
import {
  DateAdapter,
  MatDateFormats,
} from './core';
import { MatCalendarCellClassFunction, MatCalendarUserEvent } from './calendar-body';
import { createMissingDateImplError } from './datepicker-errors';
import { DateFilterFn } from './datepicker-input-base';
import { MatCalendarView } from './calendar.types';

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
    '(mousedown)': '_handleMousedown($event)',
    '(window:resize)': 'updateSize()',
  },
  preserveWhitespaces: false
})
export class MatClockView<D> implements AfterContentInit {
  private readonly _changeDetectorRef = inject(ChangeDetectorRef);
  private readonly _element = inject(ElementRef);
  public readonly _dateAdapter = inject<DateAdapter<D>>(DateAdapter, { optional: true })!;
  private readonly _dateFormats = inject<MatDateFormats>(MAT_DATE_FORMATS, { optional: true })!;
  private readonly _destroyRef = inject(DestroyRef);

  /** The time to display in this clock view. */
  readonly activeDate = input.required<D>();

  readonly selected = input<D | null, D | null>(null, {
    transform: (v: D | null) => this._getValidDateOrNull(this._dateAdapter.deserialize(v))
  });

  readonly minDate = input<D | null, D | null>(null, {
    transform: (v: D | null) => this._getValidDateOrNull(this._dateAdapter.deserialize(v))
  });

  readonly maxDate = input<D | null, D | null>(null, {
    transform: (v: D | null) => this._getValidDateOrNull(this._dateAdapter.deserialize(v))
  });

  readonly dateFilter = input<DateFilterFn<D> | null>(null);
  readonly dateClass = input<MatCalendarCellClassFunction<D> | null>(null);
  readonly clockStep = input(1);
  readonly twelveHour = input(false);
  readonly currentView = input.required<MatCalendarView>();

  readonly currentViewChange = output<MatCalendarView>();
  readonly activeDateChange = output<D>();
  readonly selectedChange = output<D | null>();
  readonly hourSelected = output<D>();
  readonly _userSelection = output<MatCalendarUserEvent<D | null>>();

  _hours: any[] = [];
  _minutes: any[] = [];
  _draggingMouse = false;
  _selectedHour: number | null = null;
  _selectedMinute: number | null = null;
  _anteMeridian = true;
  _size = 0;

  private mouseMoveListener: (event: any) => void;
  private mouseUpListener: (event: MouseEvent | TouchEvent) => void;

  readonly inHourView = computed(() => this.currentView() === 'hour');

  // Date being set by user interaction (drag/click). null = use clamped activeDate input.
  private readonly _dragDate = signal<D | null>(null);

  // Effective date for rendering: drag value takes priority over the clamped input.
  private readonly _effectiveDate = computed(() =>
    this._dragDate() ?? this._dateAdapter.clampDate(this.activeDate(), this.minDate(), this.maxDate())
  );

  get _hand(): any {
    let radius = CLOCK_OUTER_RADIUS;
    let deg = 0;

    if (this.inHourView()) {
      const outer = this.twelveHour() || (this._selectedHour! >= 0 && this._selectedHour! < 12);
      radius = outer ? CLOCK_OUTER_RADIUS : CLOCK_INNER_RADIUS;
      deg = Math.round(this._selectedHour! * (360 / (24 / 2)));
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

    this.mouseMoveListener = (event: any) => this._handleMousemove(event);
    this.mouseUpListener = (event: MouseEvent | TouchEvent) => this._handleMouseup(event);

    // Re-initialize whenever the effective date, filter, or display inputs change.
    effect(() => this._init());

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

  updateSize() {
    const { offsetWidth, offsetHeight } = this._element.nativeElement;
    this._size = (offsetWidth < offsetHeight ? offsetWidth : offsetHeight) * 0.9;
    this._changeDetectorRef.detectChanges();
  }

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
    const activeDate = this._effectiveDate(); // capture before clearing drag state

    if (dateFilter && !dateFilter(activeDate, <any>this.currentView())) {
      this._dragDate.set(null);
      return;
    }

    if (this.inHourView()) {
      // _init() is called before clearing _dragDate so it still uses the selected hour.
      this._init();
      this._dragDate.set(null);
      this.currentViewChange.emit('minute');
      this.selectedChange.emit(activeDate);
      this.hourSelected.emit(activeDate);
    } else {
      this._dragDate.set(null);
      this._userSelection.emit(<any>{ value: activeDate, event });
    }
  }

  _init() {
    const activeDate = this._effectiveDate();
    if (!activeDate) return;
    this._selectedHour = this._dateAdapter.getHours(activeDate);
    this._selectedMinute = this._dateAdapter.getMinutes(activeDate);

    this._hours.length = 0;
    this._minutes.length = 0;

    const dateFilter = this.dateFilter();
    const dateClass = this.dateClass();

    if (this.twelveHour()) {
      this._buildTwelveHourCells(activeDate, dateFilter, dateClass);
    } else {
      this._buildTwentyFourHourCells(activeDate, dateFilter, dateClass);
    }
    this._buildMinuteCells(activeDate, dateFilter, dateClass);

    this._changeDetectorRef.markForCheck();
  }

  private _buildTwelveHourCells(
    activeDate: D,
    dateFilter: DateFilterFn<D> | null,
    dateClass: MatCalendarCellClassFunction<D> | null
  ) {
    const hourNames = this._dateAdapter.getHourNames();
    this._anteMeridian = this._dateAdapter.getHours(activeDate) < 12;

    for (let i = 0; i < hourNames.length / 2; i++) {
      const radian = (i / 6) * Math.PI;
      const hour = this._anteMeridian ? i : i + 12;
      const date = this._dateAdapter.createDate(
        this._dateAdapter.getYear(activeDate),
        this._dateAdapter.getMonth(activeDate),
        this._dateAdapter.getDate(activeDate),
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

  private _buildTwentyFourHourCells(
    activeDate: D,
    dateFilter: DateFilterFn<D> | null,
    dateClass: MatCalendarCellClassFunction<D> | null
  ) {
    const hourNames = this._dateAdapter.getHourNames();

    for (let i = 0; i < hourNames.length; i++) {
      const radian = (i / 6) * Math.PI;
      const outer = i > 0 && i < 13;
      const radius = outer ? CLOCK_OUTER_RADIUS : CLOCK_INNER_RADIUS;
      let hour: number;
      if (i % 12 !== 0) { hour = i; }
      else if (i === 0) { hour = 12; }
      else { hour = 0; }
      const date = this._dateAdapter.createDate(
        this._dateAdapter.getYear(activeDate),
        this._dateAdapter.getMonth(activeDate),
        this._dateAdapter.getDate(activeDate),
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

  private _buildMinuteCells(
    activeDate: D,
    dateFilter: DateFilterFn<D> | null,
    dateClass: MatCalendarCellClassFunction<D> | null
  ) {
    const minuteNames = this._dateAdapter.getMinuteNames();

    for (let i = 0; i < minuteNames.length; i += 5) {
      const radian = (i / 30) * Math.PI;
      const date = this._dateAdapter.createDate(
        this._dateAdapter.getYear(activeDate),
        this._dateAdapter.getMonth(activeDate),
        this._dateAdapter.getDate(activeDate),
        this._dateAdapter.getHours(activeDate),
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
    if (this.inHourView()) { unitDivider = 6; }
    else if (clockStep) { unitDivider = 30 / clockStep; }
    else { unitDivider = 30; }
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

    const dateFilter = this.dateFilter();
    if (dateFilter && !dateFilter(date, <any>this.currentView())) {
      return;
    }

    // Set drag date — _effectiveDate recomputes, effect schedules _init() to move the hand.
    this._dragDate.set(date);
    this.selectedChange.emit(date);
  }

  private _applyHourValue(rawValue: number, outer: boolean): D {
    let value = rawValue === 12 ? 0 : rawValue;
    if (this.twelveHour()) {
      value = this._anteMeridian ? value : value + 12;
    } else {
      value = outer ? value : value + 12;
    }
    return this._dateAdapter.setHours(this._dateAdapter.clone(this._effectiveDate()), value);
  }

  private _applyMinuteValue(rawValue: number, clockStep: number): D {
    let value = clockStep ? rawValue * clockStep : rawValue;
    if (value === 60) value = 0;
    return this._dateAdapter.setMinutes(this._dateAdapter.clone(this._effectiveDate()), value);
  }

  _focusActiveCell() {
    // No-op: clock view has no focusable cells unlike month/year views.
    // Required to satisfy the implicit _focusActiveCell contract shared by all calendar view components.
  }

  private _getValidDateOrNull(obj: any): D | null {
    return this._dateAdapter.isDateInstance(obj) && this._dateAdapter.isValid(obj) ? obj : null;
  }
}
