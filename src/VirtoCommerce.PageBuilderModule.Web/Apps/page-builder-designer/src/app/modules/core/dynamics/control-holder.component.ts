import {
    ChangeDetectionStrategy,
    Component,
    Type,
    ViewContainerRef,
    afterNextRender,
    forwardRef,
    inject,
    input,
    inputBinding,
    outputBinding,
    signal,
} from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR, UntypedFormGroup } from '@angular/forms';

import { ControlsFactory } from '@core/controls/controls.factory';
import { ControlContext } from '@core/models';
import { BaseControlDescriptor } from '@models/controls';

@Component({
    selector: 'app-control-holder',
    template: ``,
    providers: [{
        provide: NG_VALUE_ACCESSOR,
        useExisting: forwardRef(() => ControlHolderComponent),
        multi: true,
    }],
    changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ControlHolderComponent implements ControlValueAccessor {

    private readonly vcr = inject(ViewContainerRef);
    private readonly controlsFactory = inject(ControlsFactory);

    readonly descriptor = input.required<BaseControlDescriptor>();
    readonly currentForm = input.required<UntypedFormGroup>();
    readonly context = input.required<ControlContext>();

    private readonly _controlValue = signal<any>(null);
    private _onChange: ((v: any) => void) | null = null;
    private readonly _onTouched = signal<(_: any) => void>(() => {});

    constructor() {
        afterNextRender(() => {
            const type = this.descriptor().type;
            const create = (t: Type<any>) => this.vcr.createComponent(t, {
                bindings: [
                    inputBinding('descriptor', this.descriptor),
                    inputBinding('currentForm', this.currentForm),
                    inputBinding('context', this.context),
                    inputBinding('controlValue', this._controlValue),
                    inputBinding('onControlTouched', this._onTouched),
                    outputBinding('valueChanged', (v: any) => this._onChange?.(v)),
                ]
            });
            this.controlsFactory.isLazy(type)
                ? this.controlsFactory.resolveAsync(type).then(create)
                : create(this.controlsFactory.resolve(type));
        });
    }

    writeValue(obj: any): void {
        this._controlValue.set((!obj && obj !== 0 && obj !== BigInt(0)) ? null : obj);
    }

    registerOnChange(fn: any): void {
        this._onChange = fn;
    }

    registerOnTouched(fn: any): void {
        this._onTouched.set(fn);
    }
}
