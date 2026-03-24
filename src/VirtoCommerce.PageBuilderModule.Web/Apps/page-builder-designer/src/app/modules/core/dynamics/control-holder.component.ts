import {
    ChangeDetectionStrategy,
    Component,
    ComponentRef,
    Type,
    afterNextRender,
    effect,
    forwardRef,
    inject,
    input,
    signal,
    untracked,
    viewChild,
} from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR, UntypedFormGroup } from '@angular/forms';

import { ControlHostDirective } from './control-host.directive';
import { ControlsFactory } from '@core/controls/controls.factory';
import { BaseControlDirective } from '@core/controls/base-control.directive';
import { ControlContext } from '@core/models';
import { BaseControlDescriptor } from '@models/controls';

@Component({
    selector: 'app-control-holder',
    template: `<ng-template appControlHost />`,
    providers: [{
        provide: NG_VALUE_ACCESSOR,
        useExisting: forwardRef(() => ControlHolderComponent),
        multi: true,
    }],
    styleUrls: ['./control-holder.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ControlHostDirective]
})
export class ControlHolderComponent implements ControlValueAccessor {

    private readonly controlsFactory = inject(ControlsFactory);
    private readonly host = viewChild.required(ControlHostDirective);

    readonly descriptor = input.required<BaseControlDescriptor>();
    readonly currentForm = input.required<UntypedFormGroup>();
    readonly context = input.required<ControlContext>();

    private readonly _componentRef = signal<ComponentRef<BaseControlDirective<BaseControlDescriptor>> | null>(null);
    private readonly _controlValue = signal<any>(null);
    private _onChange: ((v: any) => void) | null = null;
    private readonly _onTouched = signal<((_: any) => void) | null>(null);

    constructor() {
        afterNextRender(() => {
            const descriptorType = this.descriptor().type;
            if (this.controlsFactory.isLazy(descriptorType)) {
                this.controlsFactory.resolveAsync(descriptorType).then(type => this.createComponent(type));
            } else {
                this.createComponent(this.controlsFactory.resolve(descriptorType));
            }
        });

        effect(() => {
            const ref = this._componentRef();
            if (!ref) return;
            ref.setInput('currentForm', this.currentForm());
            ref.setInput('context', this.context());
        });

        effect(() => {
            const value = this._controlValue();
            const ref = untracked(() => this._componentRef());
            if (!ref) return;
            ref.setInput('controlValue', value);
        });

        effect(() => {
            const ref = this._componentRef();
            if (!ref) return;
            const onTouched = this._onTouched();
            untracked(() => ref.setInput('onControlTouched', onTouched ?? ((_: any) => {})));
        });
    }

    private createComponent(type: Type<any>): void {
        const ref = this.host().viewContainerRef.createComponent(type) as ComponentRef<BaseControlDirective<BaseControlDescriptor>>;
        ref.setInput('descriptor', this.descriptor());
        ref.setInput('currentForm', this.currentForm());
        ref.setInput('context', this.context());
        ref.setInput('onControlTouched', this._onTouched() ?? ((_: any) => {}));
        ref.setInput('controlValue', this._controlValue());
        ref.instance.valueChanged.subscribe((v: any) => this._onChange?.(v));
        this._componentRef.set(ref);
    }

    writeValue(obj: any): void {
        const normalized = (!obj && obj !== 0 && obj !== BigInt(0)) ? null : obj;
        this._controlValue.set(normalized);
    }

    registerOnChange(fn: any): void {
        this._onChange = fn;
    }

    registerOnTouched(fn: any): void {
        this._onTouched.set(fn);
    }
}
