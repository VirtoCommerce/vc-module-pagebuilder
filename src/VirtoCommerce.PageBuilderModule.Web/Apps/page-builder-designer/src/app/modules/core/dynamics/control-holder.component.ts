import {
    ChangeDetectionStrategy,
    Component,
    Type,
    afterNextRender,
    effect,
    forwardRef,
    inject,
    input,
    signal,
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

    readonly descriptor = input.required<BaseControlDescriptor>();
    readonly currentForm = input.required<UntypedFormGroup>();
    readonly context = input.required<ControlContext>();

    private readonly host = viewChild.required(ControlHostDirective);
    private readonly instance = signal<BaseControlDirective<BaseControlDescriptor> | null>(null);

    private _pendingValue: any = undefined;
    private _hasPendingValue = false;
    private _pendingOnChange: ((v: any) => void) | null = null;
    private _pendingOnTouched: ((_: any) => void) | null = null;

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
            const inst = this.instance();
            if (!inst) return;
            inst.currentForm = this.currentForm();
            inst.context = this.context();
        });
    }

    private createComponent(type: Type<any>): void {
        const ref = this.host().viewContainerRef.createComponent(type);
        const inst = ref.instance as BaseControlDirective<BaseControlDescriptor>;
        inst.descriptor = this.descriptor();
        inst.currentForm = this.currentForm();
        inst.context = this.context();
        if (this._hasPendingValue) {
            inst.setControlValue(this._pendingValue);
            this._hasPendingValue = false;
        }
        if (this._pendingOnChange) {
            inst.registerOnValueChanged(this._pendingOnChange);
            this._pendingOnChange = null;
        }
        if (this._pendingOnTouched) {
            inst.registerOnControlTouched(this._pendingOnTouched);
            this._pendingOnTouched = null;
        }
        this.instance.set(inst);
    }

    writeValue(obj: any): void {
        const inst = this.instance();
        if (inst) {
            inst.setControlValue(obj);
        } else {
            this._pendingValue = obj;
            this._hasPendingValue = true;
        }
    }

    registerOnChange(fn: any): void {
        const inst = this.instance();
        if (inst) {
            inst.registerOnValueChanged(fn);
        } else {
            this._pendingOnChange = fn;
        }
    }

    registerOnTouched(fn: any): void {
        const inst = this.instance();
        if (inst) {
            inst.registerOnControlTouched(fn);
        } else {
            this._pendingOnTouched = fn;
        }
    }
}
