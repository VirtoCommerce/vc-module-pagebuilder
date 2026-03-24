import { afterNextRender, ChangeDetectionStrategy, Component, ElementRef, signal, viewChild } from '@angular/core';
import { NgScrollbar } from 'ngx-scrollbar';

@Component({
    selector: 'app-panel',
    templateUrl: './panel.component.html',
    styleUrls: ['./panel.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [NgScrollbar]
})
export class PanelComponent {

    readonly panelFooterRef = viewChild.required<ElementRef>('panelFooterRef');
    readonly panelBody = viewChild.required<ElementRef>('panelBody');

    readonly hasFooter = signal(true);

    constructor() {
        afterNextRender(() => {
            this.hasFooter.set((<HTMLDivElement>this.panelFooterRef().nativeElement).children.length > 0);
        });
    }
}
