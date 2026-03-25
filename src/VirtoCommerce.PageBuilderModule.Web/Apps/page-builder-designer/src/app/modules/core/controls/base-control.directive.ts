import { AfterContentInit, Directive, ElementRef, effect, input, linkedSignal, OnInit, output, untracked } from "@angular/core";
import { UntypedFormGroup } from "@angular/forms";
import { appHelpers } from "@app/modules/integration/helpers";
import { ControlContext } from '@core/models';
import { BaseControlDescriptor } from '@models/controls';

@Directive({})
export class BaseControlDirective<T extends BaseControlDescriptor> implements OnInit, AfterContentInit {

  readonly _descriptorInput = input<T | null>(null, { alias: 'descriptor' });
  get descriptor(): T | null { return this._descriptorInput(); }

  readonly _contextInput = input<ControlContext | undefined>(undefined, { alias: 'context' });
  get context(): ControlContext { return this._contextInput()!; }

  readonly _currentFormInput = input<UntypedFormGroup | undefined>(undefined, { alias: 'currentForm' });
  get currentForm(): UntypedFormGroup { return this._currentFormInput()!; }

  readonly _onControlTouchedInput = input<(_: any) => void>((_) => { }, { alias: 'onControlTouched' });
  get onControlTouched(): (_: any) => void { return this._onControlTouchedInput(); }

  readonly _controlValueInput = input<any>(null, { alias: 'controlValue' });
  readonly controlValue = linkedSignal(() => this._controlValueInput() ?? null);
  onValueChanged = (value: any) => this.defaultValueChanged(value);

  readonly valueChanged = output<any>();

  constructor() {
    effect(() => {
      this._descriptorInput();
      untracked(() => this.descriptorChanged());
    });

    effect(() => {
      this._controlValueInput(); // track external value changes
      untracked(() => this.applyNewValue());
    });
  }

  ngOnInit(): void {
    this.initContent();
  }

  ngAfterContentInit(): void {
    if (this.descriptor?.autofocus) {
      setTimeout(() => {
        this.setFocus();
      });
    }
  }

  protected setControlValue(value: any) {
    if (!value && value !== 0 && value !== BigInt(0)) {
      value = null;
    }
    this.controlValue.set(value);
    this.applyNewValue();
  }

  onAction(action: { label?: string; icon?: string; execute?: string; }) {
    if (!action.execute) {
      return;
    }
    const result = appHelpers.evalInContext(action.execute, this.context);
    if (result) {
      this.setControlValue(result);
    }
  }

  protected applyNewValue() {
    // Override in subclasses to apply the new control value to the underlying UI element.
  }

  protected setFocus() {
    const control = this.getFocusableControl();
    if (control) {
      control.nativeElement.focus();
    }
  }

  protected getFocusableControl(): ElementRef | null {
    return null;
  }

  protected defaultValueChanged(value: any) {
    this.controlValue.set(value);
    this.valueChanged.emit(value);
  }

  protected initContent() {
    // Override in subclasses to perform initialization logic after content is ready.
  }

  protected descriptorChanged() {
    // Override in subclasses to react when the descriptor input is assigned or updated.
  }
}
