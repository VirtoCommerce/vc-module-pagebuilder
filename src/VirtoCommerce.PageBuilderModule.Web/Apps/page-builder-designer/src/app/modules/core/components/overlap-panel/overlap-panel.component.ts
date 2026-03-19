import { AfterViewInit, ChangeDetectorRef, DestroyRef, HostBinding, HostListener, inject } from '@angular/core';
import { EnvironmentRef } from '@integration/services';
import { Component, input, ElementRef } from '@angular/core';
import { NgClass } from '@angular/common';
import { IconComponent } from '../icon/icon.component';

@Component({
    selector: 'app-overlap-panel',
    templateUrl: './overlap-panel.component.html',
    styleUrls: ['./overlap-panel.component.scss'],
    imports: [NgClass, IconComponent]
})
export class OverlapPanelComponent implements AfterViewInit {

    private readonly windowRef = inject(EnvironmentRef);
    private readonly cdr = inject(ChangeDetectorRef);
    private readonly elementRef = inject(ElementRef);
    private readonly destroyRef = inject(DestroyRef);

    readonly expandable = input(true);
    readonly skipTranslate = input<boolean | null>(false);

    @HostBinding("class.inplace") get skipTranslateClass() { return this.skipTranslate(); }

    @HostListener('window:resize')
    onResize() {
        this.changeWidth();
    }

    private _interval: any;

    contentWidth: number | null = null;
    expanderPosition: number | null = null;
    isOpened = false; // todo: maybe should be stored in state or in url

    ngAfterViewInit(): void {
        this._interval = setInterval(() => this.changeWidth(), 1000);
        this.destroyRef.onDestroy(() => clearInterval(this._interval));
    }

    toggle() {
        this.isOpened = !this.isOpened;
        this.changeWidth();
    }

    changeWidth() {
        setTimeout(() => {
            if (this.isOpened) {
                this.contentWidth = this.windowRef.nativeWindow.innerWidth / 2;
            } else {
                this.contentWidth = null;
            }
            this.expanderPosition = this.contentWidth || this.elementRef.nativeElement.offsetWidth;
            this.cdr.detectChanges();
        });
    }

}
