import { Component, AfterViewInit, ElementRef, ChangeDetectorRef, inject, viewChild } from '@angular/core';
import { NgScrollbar } from 'ngx-scrollbar';

@Component({
    selector: 'app-panel',
    templateUrl: './panel.component.html',
    styleUrls: ['./panel.component.scss'],
    imports: [NgScrollbar]
})
export class PanelComponent implements AfterViewInit {

    private readonly cdr = inject(ChangeDetectorRef);

    readonly panelFooterRef = viewChild.required<ElementRef>('panelFooterRef');
    readonly panelBody = viewChild.required<ElementRef>('panelBody');

    hasFooter = true;

    ngAfterViewInit(): void {
        this.hasFooter = (<HTMLDivElement>this.panelFooterRef().nativeElement).children.length > 0;
        this.cdr.detectChanges();
    }
}
