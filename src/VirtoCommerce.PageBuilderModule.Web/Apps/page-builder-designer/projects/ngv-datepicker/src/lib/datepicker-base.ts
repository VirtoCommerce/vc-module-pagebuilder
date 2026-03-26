/**
 * @license
 * Copyright Google LLC All Rights Reserved.
 *
 * Use of this source code is governed by an MIT-style license that can be
 * found in the LICENSE file at https://angular.io/license
 */

import {Directionality} from '@angular/cdk/bidi';
import {BooleanInput, coerceBooleanProperty, coerceStringArray} from '@angular/cdk/coercion';
import {hasModifierKey} from '@angular/cdk/keycodes';
import {
  Overlay,
  OverlayConfig,
  OverlayRef,
  ScrollStrategy,
  FlexibleConnectedPositionStrategy,
} from '@angular/cdk/overlay';
import {ComponentPortal, ComponentType, TemplatePortal, CdkPortalOutlet} from '@angular/cdk/portal';
import {CdkTrapFocus} from '@angular/cdk/a11y';
import {MatButton} from '@angular/material/button';
import {
  AfterViewInit,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ComponentRef,
  DestroyRef,
  Directive,
  ElementRef,
  effect,
  InjectionToken,
  afterNextRender,
  Injector,
  OnInit,
  OutputRef,
  ViewContainerRef,
  ViewEncapsulation,
  inject,
  input,
  isDevMode,
  output,
  untracked,
  viewChild,
} from '@angular/core';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {ThemePalette} from '@angular/material/core';
import {merge, Subject, Observable, Subscription} from 'rxjs';
import {filter, take} from 'rxjs/operators';
import {_getFocusedElementPierceShadowDom} from '@angular/cdk/platform';
import {DateAdapter} from './core';
import {MatCalendar} from './calendar';
import {MatCalendarType, MatCalendarView} from './calendar.types';
import {createMissingDateImplError} from './datepicker-errors';
import {MatCalendarUserEvent, MatCalendarCellClassFunction} from './calendar-body';
import {DateFilterFn} from './datepicker-input-base';
import {
  ExtractDateTypeFromSelection,
  MatDateSelectionModel,
  DateRange,
} from './date-selection-model';
import {
  MAT_DATE_RANGE_SELECTION_STRATEGY,
  MatDateRangeSelectionStrategy,
} from './date-range-selection-strategy';
import {MatDatepickerIntl} from './datepicker-intl';

/** Used to generate a unique ID for each datepicker instance. */
let datepickerUid = 0;

/** Injection token that determines the scroll handling while the calendar is open. */
export const MAT_DATEPICKER_SCROLL_STRATEGY = new InjectionToken<() => ScrollStrategy>(
  'mat-datepicker-scroll-strategy',
);

/** @docs-private */
export function MAT_DATEPICKER_SCROLL_STRATEGY_FACTORY(overlay: Overlay): () => ScrollStrategy {
  return () => overlay.scrollStrategies.reposition();
}

/** Possible positions for the datepicker dropdown along the X axis. */
export type DatepickerDropdownPositionX = 'start' | 'end';

/** Possible positions for the datepicker dropdown along the Y axis. */
export type DatepickerDropdownPositionY = 'above' | 'below';

/** @docs-private */
export const MAT_DATEPICKER_SCROLL_STRATEGY_FACTORY_PROVIDER = {
  provide: MAT_DATEPICKER_SCROLL_STRATEGY,
  deps: [Overlay],
  useFactory: MAT_DATEPICKER_SCROLL_STRATEGY_FACTORY,
};


/**
 * Component used as the content for the datepicker overlay. We use this instead of using
 * MatCalendar directly as the content so we can control the initial focus. This also gives us a
 * place to put additional features of the overlay that are not part of the calendar itself in the
 * future. (e.g. confirmation buttons).
 * @docs-private
 */
@Component({
    selector: 'mat-datepicker-content',
    templateUrl: 'datepicker-content.html',
    styleUrls: ['datepicker-content.scss'],
    imports: [CdkTrapFocus, CdkPortalOutlet, MatButton, MatCalendar],
    host: {
        'class': 'mat-datepicker-content',
        '[class.mat-datepicker-content-touch]': 'datepicker.touchUi',
        '[class.mat-datepicker-content-open]': '_animationState !== "void"',
    },
    exportAs: 'matDatepickerContent',
    encapsulation: ViewEncapsulation.None,
    changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MatDatepickerContent<S, D = ExtractDateTypeFromSelection<S>>
  implements OnInit, AfterViewInit
{
  private readonly _changeDetectorRef = inject(ChangeDetectorRef);
  private readonly _globalModel = inject<MatDateSelectionModel<S, D>>(MatDateSelectionModel);
  private readonly _dateAdapter = inject<DateAdapter<D>>(DateAdapter);
  private readonly _rangeSelectionStrategy = inject<MatDateRangeSelectionStrategy<D>>(
    MAT_DATE_RANGE_SELECTION_STRATEGY, {optional: true});
  private readonly _destroyRef = inject(DestroyRef);

  readonly color = input<ThemePalette>(undefined);
  private _model!: MatDateSelectionModel<S, D>;

  /** Reference to the internal calendar component. */
  readonly _calendar = viewChild.required(MatCalendar<D>);

  /** Reference to the datepicker that created the overlay. */
  datepicker!: MatDatepickerBase<any, S, D>;

  /** Start of the comparison range. */
  comparisonStart: D | null = null;

  /** End of the comparison range. */
  comparisonEnd: D | null = null;

  /** Whether the datepicker is above or below the input. */
  _isAbove: boolean = false;

  /** Current state of the animation. */
  _animationState: 'enter-dropdown' | 'enter-dialog' | 'void' = 'void';

  /** Emits when an animation has finished. */
  readonly _animationDone = new Subject<void>();

  /** Text for the close button. */
  _closeButtonText: string;

  /** Whether the close button currently has focus. */
  _closeButtonFocused: boolean = false;

  /** Portal with projected action buttons. */
  _actionsPortal: TemplatePortal | null = null;

  constructor() {
    this._closeButtonText = inject(MatDatepickerIntl).closeCalendarLabel;
    this._destroyRef.onDestroy(() => {
      this._animationDone.complete();
    });
  }

  ngOnInit() {
    // If we have actions, clone the model so that we have the ability to cancel the selection,
    // otherwise update the global model directly. Note that we want to assign this as soon as
    // possible, but `_actionsPortal` isn't available in the constructor so we do it in `ngOnInit`.
    this._model = this._actionsPortal ? this._globalModel.clone() : this._globalModel;
    this._animationState = this.datepicker.touchUi ? 'enter-dialog' : 'enter-dropdown';
  }

  ngAfterViewInit() {
    this.datepicker.stateChanges
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe(() => this._changeDetectorRef.markForCheck());
    this._calendar().focusActiveCell();
  }

  _queueUserSelection(date: D) {
    this._model.queue(date);
  }

  _handleUserSelection(event: MatCalendarUserEvent<D | null>) {
    const selection = this._model.selection;
    const value = event.value;
    const isRange = selection instanceof DateRange;

    // If we're selecting a range and we have a selection strategy, always pass the value through
    // there. Otherwise don't assign null values to the model, unless we're selecting a range.
    // A null value when picking a range means that the user cancelled the selection (e.g. by
    // pressing escape), whereas when selecting a single value it means that the value didn't
    // change. This isn't very intuitive, but it's here for backwards-compatibility.
    if (isRange && this._rangeSelectionStrategy) {
      const newSelection = this._rangeSelectionStrategy.selectionFinished(
        value,
        selection as unknown as DateRange<D>,
        event.event,
      );
      this._model.updateSelection(newSelection as unknown as S, this);
    } else if (
      value &&
      (isRange || !this._dateAdapter.sameDate(value, selection as unknown as D, this._calendar().getUnit()))
    ) {
      this._model.add(value);
    }

    // Delegate closing the overlay to the actions.
    if ((!this._model || this._model.isComplete()) && !this._actionsPortal) {
      this.datepicker.close();
    }
  }

  _startExitAnimation() {
    this._animationState = 'void';
    this._changeDetectorRef.markForCheck();
    // Without Angular animations the done event never fires, so emit manually
    // after the CSS exit transition duration (100ms).
    setTimeout(() => this._animationDone.next(), 100);
  }

  _getSelected() {
    return this._model.selection as unknown as D | DateRange<D> | null;
  }

  /** Applies the current pending selection to the global model. */
  _applyPendingSelection() {
    this._model.processQueue();
    if (this._model !== this._globalModel) {
      this._globalModel.updateSelection(this._model.selection, this);
    }
  }
}

/** Form control that can be associated with a datepicker. */
export interface MatDatepickerControl<D> {
  type: MatCalendarType;
  getStartValue(): D | null;
  getThemePalette(): ThemePalette;
  min: D | null;
  max: D | null;
  disabled: boolean;
  dateFilter: DateFilterFn<D>;
  getConnectedOverlayOrigin(): ElementRef;
  getOverlayLabelId(): string | null;
  stateChanges: Observable<void>;
}

/** A datepicker that can be attached to a {@link MatDatepickerControl}. */
export interface MatDatepickerPanel<
  C extends MatDatepickerControl<D>,
  S,
  D = ExtractDateTypeFromSelection<S>,
> {
  /** Stream that emits whenever the date picker is closed. */
  closedStream: OutputRef<void>;
  /** The type of value handled by the calendar. */
  type: MatCalendarType;
  /** Color palette to use on the datepicker's calendar. */
  color: ThemePalette;
  /** The input element the datepicker is associated with. */
  datepickerInput: C;
  /** Whether the datepicker pop-up should be disabled. */
  disabled: boolean;
  /** The id for the datepicker's calendar. */
  id: string;
  /** Whether the datepicker is open. */
  opened: boolean;
  /** Stream that emits whenever the date picker is opened. */
  openedStream: OutputRef<void>;
  /** Emits when the datepicker's state changes. */
  stateChanges: Subject<void>;
  /** Opens the datepicker. */
  open(): void;
  /** Register an input with the datepicker. */
  registerInput(input: C): MatDateSelectionModel<S, D>;
}

/** Base class for a datepicker. */
@Directive()
export abstract class MatDatepickerBase<
  C extends MatDatepickerControl<D>,
  S,
  D = ExtractDateTypeFromSelection<S>,
> implements MatDatepickerPanel<C, S, D>
{
  private readonly _overlay = inject(Overlay);
  private readonly _injector = inject(Injector);
  private readonly _viewContainerRef = inject(ViewContainerRef);
  private readonly _dateAdapter = inject<DateAdapter<D>>(DateAdapter, {optional: true})!;
  private readonly _dir = inject(Directionality, {optional: true});
  private readonly _model = inject<MatDateSelectionModel<S, D>>(MatDateSelectionModel);
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _scrollStrategy: () => ScrollStrategy =
    inject<() => ScrollStrategy>(MAT_DATEPICKER_SCROLL_STRATEGY);

  private _inputStateChanges = Subscription.EMPTY;

  /** An input indicating the type of the custom header component for the calendar, if set. */
  readonly calendarHeaderComponent = input<ComponentType<any> | undefined>(undefined);

  /** The date to open the calendar to initially. */
  readonly _startAtInput = input<D | null, D | null>(null, {
    alias: 'startAt',
    transform: (v: D | null) => this._dateAdapter.getValidDateOrNull(this._dateAdapter.deserialize(v)),
  });

  get startAt(): D | null {
    return this._startAtInput() || (this.datepickerInput ? this.datepickerInput.getStartValue() : null);
  }

  /** The type of value handled by the calendar. */
  readonly _typeInput = input<MatCalendarType>('date', { alias: 'type' });

  get type(): MatCalendarType { return this._typeInput(); }

  /** The view that the calendar should start in. */
  readonly startView = input<MatCalendarView>('month');

  /** multi-year inputs */
  readonly yearsPerPage = input(24);

  readonly yearsPerRow = input(4);

  /** Clock interval */
  readonly clockStep = input(1);

  /** Clock hour format */
  readonly twelveHour = input(true);

  /** Color palette to use on the datepicker's calendar. */
  readonly _colorInput = input<ThemePalette>(undefined, { alias: 'color' });

  get color(): ThemePalette {
    return this._colorInput() || (this.datepickerInput ? this.datepickerInput.getThemePalette() : undefined);
  }

  /**
   * Whether the calendar UI is in touch mode. In touch mode the calendar opens in a dialog rather
   * than a dropdown and elements have more padding to allow for bigger touch targets.
   */
  readonly _touchUiInput = input(false, {
    alias: 'touchUi',
    transform: (value: BooleanInput) => coerceBooleanProperty(value),
  });

  get touchUi(): boolean { return this._touchUiInput(); }

  /** Whether the datepicker pop-up should be disabled. */
  readonly _disabledInput = input(false, {
    alias: 'disabled',
    transform: (value: BooleanInput) => coerceBooleanProperty(value),
  });

  get disabled(): boolean { return this._disabledInput(); }

  /** Preferred position of the datepicker in the X axis. */
  readonly _xPositionInput = input<DatepickerDropdownPositionX>('start', { alias: 'xPosition' });

  get xPosition(): DatepickerDropdownPositionX { return this._xPositionInput(); }

  /** Preferred position of the datepicker in the Y axis. */
  readonly _yPositionInput = input<DatepickerDropdownPositionY>('below', { alias: 'yPosition' });

  get yPosition(): DatepickerDropdownPositionY { return this._yPositionInput(); }

  /**
   * Whether to restore focus to the previously-focused element when the calendar is closed.
   * Note that automatic focus restoration is an accessibility feature and it is recommended that
   * you provide your own equivalent, if you decide to turn it off.
   */
  readonly _restoreFocusInput = input(true, {
    alias: 'restoreFocus',
    transform: (value: BooleanInput) => coerceBooleanProperty(value),
  });

  get restoreFocus(): boolean { return this._restoreFocusInput(); }

  /**
   * Emits selected year in multiyear view.
   * This doesn't imply a change on the selected date.
   */
  readonly yearSelected = output<D>();

  /**
   * Emits selected month in year view.
   * This doesn't imply a change on the selected date.
   */
  readonly monthSelected = output<D>();

  /**
   * Emits when the current view changes.
   */
  readonly viewChanged = output<MatCalendarView>();

  /** Function that can be used to add custom CSS classes to dates. */
  readonly dateClass = input<MatCalendarCellClassFunction<D> | null>(null);

  /** Emits when the datepicker has been opened. */
  readonly openedStream = output({alias: 'opened'});

  /** Emits when the datepicker has been closed. */
  readonly closedStream = output({alias: 'closed'});

  /**
   * Classes to be passed to the date picker panel.
   * Supports string and string array values, similar to `ngClass`.
   */
  readonly _panelClassInput = input<string[], string | string[]>([], {
    alias: 'panelClass',
    transform: (v: string | string[]) => coerceStringArray(v),
  });

  get panelClass(): string[] { return this._panelClassInput(); }

  /** Whether the calendar is open. */
  readonly _openedInput = input(false, {
    alias: 'opened',
    transform: (value: BooleanInput) => coerceBooleanProperty(value),
  });

  get opened(): boolean { return this._opened; }
  private _opened = false;

  /** The id for the datepicker calendar. */
  id: string = `mat-datepicker-${datepickerUid++}`;

  /** The minimum selectable date. */
  _getMinDate(): D | null {
    return this.datepickerInput && this.datepickerInput.min;
  }

  /** The maximum selectable date. */
  _getMaxDate(): D | null {
    return this.datepickerInput && this.datepickerInput.max;
  }

  _getDateFilter(): DateFilterFn<D> {
    return this.datepickerInput && this.datepickerInput.dateFilter;
  }

  /** A reference to the overlay into which we've rendered the calendar. */
  private _overlayRef: OverlayRef | null = null;

  /** Reference to the component instance rendered in the overlay. */
  private _componentRef: ComponentRef<MatDatepickerContent<S, D>> | null = null;

  /** The element that was focused before the datepicker was opened. */
  private _focusedElementBeforeOpen: HTMLElement | null = null;

  /** Unique class that will be added to the backdrop so that the test harnesses can look it up. */
  private _backdropHarnessClass = `${this.id}-backdrop`;

  /** Currently-registered actions portal. */
  private _actionsPortal: TemplatePortal | null = null;

  /** The input element this datepicker is associated with. */
  datepickerInput!: C;

  /** Emits when the datepicker's state changes. */
  readonly stateChanges = new Subject<void>();

  constructor() {
    if (!this._dateAdapter && isDevMode()) {
      throw createMissingDateImplError('DateAdapter');
    }
    this._destroyRef.onDestroy(() => {
      this._destroyOverlay();
      this.close();
      this._inputStateChanges.unsubscribe();
      this.stateChanges.complete();
    });

    // Sync type to connected input and emit state changes when it changes
    effect(() => {
      const t = this._typeInput();
      untracked(() => {
        if (this.datepickerInput && this.datepickerInput.type !== t) {
          this.datepickerInput.type = t;
        }
        this.stateChanges.next(undefined);
      });
    });

    // Update overlay position when xPosition/yPosition change
    effect(() => {
      this._xPositionInput();
      this._yPositionInput();
      untracked(() => {
        if (this._overlayRef) {
          const positionStrategy = this._overlayRef.getConfig().positionStrategy;
          if (positionStrategy instanceof FlexibleConnectedPositionStrategy) {
            this._setConnectedPositions(positionStrategy);
            if (this.opened) {
              this._overlayRef.updatePosition();
            }
          }
        }
        this.stateChanges.next(undefined);
      });
    });

    // Emit state changes for remaining inputs
    effect(() => {
      this._startAtInput();
      this._colorInput();
      this._touchUiInput();
      this._disabledInput();
      this._restoreFocusInput();
      this._panelClassInput();
      untracked(() => this.stateChanges.next(undefined));
    });

    // Open/close when the opened input changes
    effect(() => {
      const v = this._openedInput();
      untracked(() => v ? this.open() : this.close());
    });
  }

  /** Selects the given date */
  select(date: D): void {
    this._model.add(date);
  }

  /** Emits the selected year in multiyear view */
  _selectYear(normalizedYear: D): void {
    this.yearSelected.emit(normalizedYear);
  }

  /** Emits selected month in year view */
  _selectMonth(normalizedMonth: D): void {
    this.monthSelected.emit(normalizedMonth);
  }

  /** Emits changed view */
  _viewChanged(view: MatCalendarView): void {
    this.viewChanged.emit(view);
  }

  /**
   * Register an input with this datepicker.
   * @param control The datepicker input to register with this datepicker.
   * @returns Selection model that the input should hook itself up to.
   */
  registerInput(control: C): MatDateSelectionModel<S, D> {
    if (this.datepickerInput && isDevMode()) {
      throw Error('A MatDatepicker can only be associated with a single input.');
    }
    this._inputStateChanges.unsubscribe();
    this.datepickerInput = control;
    this.datepickerInput.type = this.type;
    this._inputStateChanges = control.stateChanges.subscribe(() => this.stateChanges.next(undefined));
    return this._model;
  }

  /**
   * Registers a portal containing action buttons with the datepicker.
   * @param portal Portal to be registered.
   */
  registerActions(portal: TemplatePortal): void {
    if (this._actionsPortal && isDevMode()) {
      throw Error('A MatDatepicker can only be associated with a single actions row.');
    }
    this._actionsPortal = portal;
  }

  /**
   * Removes a portal containing action buttons from the datepicker.
   * @param portal Portal to be removed.
   */
  removeActions(portal: TemplatePortal): void {
    if (portal === this._actionsPortal) {
      this._actionsPortal = null;
    }
  }

  /** Open the calendar. */
  open(): void {
    if (this._opened || this.disabled) {
      return;
    }

    if (!this.datepickerInput && isDevMode()) {
      throw Error('Attempted to open an MatDatepicker with no associated input.');
    }

    this._focusedElementBeforeOpen = _getFocusedElementPierceShadowDom();
    this._openOverlay();
    this._opened = true;
    this.openedStream.emit();
  }

  /** Close the calendar. */
  close(): void {
    if (!this._opened) {
      return;
    }

    if (this._componentRef) {
      const instance = this._componentRef.instance;
      instance._startExitAnimation();
      instance._animationDone.pipe(take(1)).subscribe(() => this._destroyOverlay());
    }

    const completeClose = () => {
      // The `_opened` could've been reset already if
      // we got two events in quick succession.
      if (this._opened) {
        this._opened = false;
        this.closedStream.emit();
        this._focusedElementBeforeOpen = null;
      }
    };

    if (
      this.restoreFocus &&
      this._focusedElementBeforeOpen &&
      typeof this._focusedElementBeforeOpen.focus === 'function'
    ) {
      // Because IE moves focus asynchronously, we can't count on it being restored before we've
      // marked the datepicker as closed. If the event fires out of sequence and the element that
      // we're refocusing opens the datepicker on focus, the user could be stuck with not being
      // able to close the calendar at all. We work around it by making the logic, that marks
      // the datepicker as closed, async as well.
      this._focusedElementBeforeOpen.focus();
      setTimeout(completeClose);
    } else {
      completeClose();
    }
  }

  /** Applies the current pending selection on the overlay to the model. */
  _applyPendingSelection() {
    this._componentRef?.instance?._applyPendingSelection();
  }

  /** Forwards relevant values from the datepicker to the datepicker content inside the overlay. */
  protected _forwardContentValues(instance: MatDatepickerContent<S, D>) {
    instance.datepicker = this;
    this._componentRef!.setInput('color', this.color);
    instance._actionsPortal = this._actionsPortal;
  }

  /** Opens the overlay with the calendar. */
  private _openOverlay(): void {
    this._destroyOverlay();

    const isDialog = this.touchUi;
    const labelId = this.datepickerInput.getOverlayLabelId();
    const portal = new ComponentPortal<MatDatepickerContent<S, D>>(
      MatDatepickerContent,
      this._viewContainerRef,
    );
    const overlayRef = (this._overlayRef = this._overlay.create(
      new OverlayConfig({
        positionStrategy: isDialog ? this._getDialogStrategy() : this._getDropdownStrategy(),
        hasBackdrop: true,
        backdropClass: [
          isDialog ? 'cdk-overlay-dark-backdrop' : 'mat-overlay-transparent-backdrop',
          this._backdropHarnessClass,
        ],
        direction: this._dir ?? undefined,
        scrollStrategy: isDialog ? this._overlay.scrollStrategies.block() : this._scrollStrategy(),
        panelClass: `mat-datepicker-${isDialog ? 'dialog' : 'popup'}`,
      }),
    ));
    const overlayElement = overlayRef.overlayElement;
    overlayElement.setAttribute('role', 'dialog');

    if (labelId) {
      overlayElement.setAttribute('aria-labelledby', labelId);
    }

    if (isDialog) {
      overlayElement.setAttribute('aria-modal', 'true');
    }

    this._getCloseStream(overlayRef).subscribe(event => {
      if (event) {
        event.preventDefault();
      }
      this.close();
    });

    this._componentRef = overlayRef.attach(portal);
    this._forwardContentValues(this._componentRef.instance);

    // Update the position once the calendar has rendered. Only relevant in dropdown mode.
    if (!isDialog) {
      afterNextRender(() => overlayRef.updatePosition(), { injector: this._injector });
    }
  }

  /** Destroys the current overlay. */
  private _destroyOverlay() {
    if (this._overlayRef) {
      this._overlayRef.dispose();
      this._overlayRef = this._componentRef = null;
    }
  }

  /** Gets a position strategy that will open the calendar as a dropdown. */
  private _getDialogStrategy() {
    return this._overlay.position().global().centerHorizontally().centerVertically();
  }

  /** Gets a position strategy that will open the calendar as a dropdown. */
  private _getDropdownStrategy() {
    const strategy = this._overlay
      .position()
      .flexibleConnectedTo(this.datepickerInput.getConnectedOverlayOrigin())
      .withTransformOriginOn('.mat-datepicker-content')
      .withFlexibleDimensions(false)
      .withViewportMargin(8)
      .withLockedPosition();

    return this._setConnectedPositions(strategy);
  }

  /** Sets the positions of the datepicker in dropdown mode based on the current configuration. */
  private _setConnectedPositions(strategy: FlexibleConnectedPositionStrategy) {
    const primaryX = this.xPosition === 'end' ? 'end' : 'start';
    const secondaryX = primaryX === 'start' ? 'end' : 'start';
    const primaryY = this.yPosition === 'above' ? 'bottom' : 'top';
    const secondaryY = primaryY === 'top' ? 'bottom' : 'top';

    return strategy.withPositions([
      {
        originX: primaryX,
        originY: secondaryY,
        overlayX: primaryX,
        overlayY: primaryY,
      },
      {
        originX: primaryX,
        originY: primaryY,
        overlayX: primaryX,
        overlayY: secondaryY,
      },
      {
        originX: secondaryX,
        originY: secondaryY,
        overlayX: secondaryX,
        overlayY: primaryY,
      },
      {
        originX: secondaryX,
        originY: primaryY,
        overlayX: secondaryX,
        overlayY: secondaryY,
      },
    ]);
  }

  /** Gets an observable that will emit when the overlay is supposed to be closed. */
  private _getCloseStream(overlayRef: OverlayRef) {
    return merge(
      overlayRef.backdropClick(),
      overlayRef.detachments(),
      overlayRef.keydownEvents().pipe(
        filter(event => {
          // Closing on alt + up is only valid when there's an input associated with the datepicker.
          return (
            (event.key === 'Escape' && !hasModifierKey(event)) ||
            (this.datepickerInput && hasModifierKey(event, 'altKey') && event.key === 'ArrowUp')
          );
        }),
      ),
    );
  }

}
