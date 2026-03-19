import { Directive, ViewContainerRef, inject } from '@angular/core';

@Directive({
    selector: '[appControlHost]',
})
export class ControlHostDirective {
    public readonly viewContainerRef = inject(ViewContainerRef);
}
