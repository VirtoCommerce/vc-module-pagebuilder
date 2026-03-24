import { ConnectedPosition, OverlayModule } from '@angular/cdk/overlay';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { NgStyle } from '@angular/common';
import { ColorSketchModule } from 'ngx-color/sketch';
import { ColorTwitterModule } from 'ngx-color/twitter';

import { BaseControlDirective } from '@core/controls/base-control.directive';
import { ColorDescriptor } from '@models/controls';
import { ColorEvent } from 'ngx-color';

/**
 * https://ngx-color.vercel.app/
 * https://www.npmjs.com/package/ngx-color
 */

@Component({
    selector: 'app-color',
    templateUrl: './color.component.html',
    styleUrls: ['./color.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [NgStyle, OverlayModule, ColorSketchModule, ColorTwitterModule]
})
export class ColorComponent extends BaseControlDirective<ColorDescriptor> {
    isOpen = false;

    positions: ConnectedPosition[] = [{
        originX: 'end',
        originY: 'bottom',
        overlayX: 'end',
        overlayY: 'top',
        weight: 2
    }, {
        originX: 'end',
        originY: 'top',
        overlayX: 'end',
        overlayY: 'bottom',
        weight: 1
    }];

    clearColor() {
        this.onValueChanged(this.descriptor?.clearValue || null);
        this.close();
    }

    changeColor(value: ColorEvent) {
        this.onValueChanged(value.color.hex);
    }

    togglePopover() {
        this.isOpen = !this.isOpen;
    }

    close() {
        this.isOpen = false;
    }

    applyColor() {
        this.close();
    }

    outsideClick(event: MouseEvent) {
        event.stopPropagation();
        this.close();
    }


}
