import {
    Component,
    Input,
    Type,
    input,
    OnInit,
    viewChild,
    forwardRef,
    ChangeDetectionStrategy,
    // HostBinding,
    ChangeDetectorRef,
    inject
} from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR, UntypedFormGroup } from '@angular/forms';

import { ControlHostDirective } from './control-host.directive';
import { ControlsFactory } from '@core/controls/controls.factory'; import { BaseControlDirective } from '@core/controls/base-control.directive';

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
export class ControlHolderComponent implements OnInit, ControlValueAccessor {

    private readonly controlsFactory = inject(ControlsFactory);
    private readonly cdr = inject(ChangeDetectorRef);


    private component!: BaseControlDirective<BaseControlDescriptor>;
    private _context!: ControlContext;
    private _currentForm!: UntypedFormGroup;
    private _hasPendingValue = false;
    private _pendingValue: any = undefined;
    private _pendingOnChange: ((v: any) => void) | null = null;
    private _pendingOnTouched: ((_: any) => void) | null = null;

    readonly host = viewChild.required(ControlHostDirective);

    readonly descriptor = input.required<BaseControlDescriptor>();
    @Input({ required: true }) get currentForm(): UntypedFormGroup {
        return this._currentForm;
    }
    set currentForm(value: UntypedFormGroup) {
        this._currentForm = value;
        if (this.component) {
            this.component.currentForm = value;
            this.cdr.detectChanges();
        }
    }

    @Input({ required: true }) get context(): ControlContext {
        return this._context;
    }
    set context(value: ControlContext) {
        this._context = value;
        if (this.component) {
            this.component.context = value;
            this.cdr.detectChanges();
        }
    }

    ngOnInit(): void {
        const descriptorType = this.descriptor().type;
        if (this.controlsFactory.isLazy(descriptorType)) {
            this.controlsFactory.resolveAsync(descriptorType).then(type => this.createComponent(type));
        } else {
            this.createComponent(this.controlsFactory.resolve(descriptorType));
        }
    }

    private createComponent(type: Type<any>): void {
        const viewContainerRef = this.host().viewContainerRef;
        const componentRef = viewContainerRef.createComponent(type);
        this.component = componentRef.instance;
        this.component.descriptor = this.descriptor();
        this.component.currentForm = this.currentForm;
        this.component.context = this.context;
        if (this._hasPendingValue) {
            this.component.setControlValue(this._pendingValue);
        }
        if (this._pendingOnChange) {
            this.component.registerOnValueChanged(this._pendingOnChange);
        }
        if (this._pendingOnTouched) {
            this.component.registerOnControlTouched(this._pendingOnTouched);
        }
        this.cdr.detectChanges();
    }

    onChange = (_: any) => { };

    writeValue(obj: any): void {
        if (this.component) {
            this.component.setControlValue(obj);
        } else {
            this._pendingValue = obj;
            this._hasPendingValue = true;
        }
    }

    registerOnChange(fn: any): void {
        if (this.component) {
            this.component.registerOnValueChanged((event) => {
                fn(event);
            });
        } else {
            this._pendingOnChange = fn;
        }
    }

    registerOnTouched(fn: any): void {
        if (this.component) {
            this.component.registerOnControlTouched(fn);
        } else {
            this._pendingOnTouched = fn;
        }
    }
}
