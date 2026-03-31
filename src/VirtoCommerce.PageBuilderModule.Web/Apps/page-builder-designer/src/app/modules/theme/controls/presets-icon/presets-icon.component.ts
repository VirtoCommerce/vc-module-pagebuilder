import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { NgStyle } from '@angular/common';
import { IconWithPreviewComponent } from '@core/components/icon-with-preview/icon-with-preview.component';

@Component({
    selector: 'app-presets-icon',
    templateUrl: './presets-icon.component.html',
    styleUrls: ['./presets-icon.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [NgStyle, IconWithPreviewComponent]
})
export class PresetsIconComponent {

    readonly name = input.required<string>();
    readonly icon = input<string | null | undefined>(undefined);
    readonly preview = input<string | null | undefined>(undefined);

    readonly background = computed(() => this.stringToColour());
    readonly color = computed(() => this.getTextColor());
    readonly letter = computed(() => this.name()[0]);

    private stringToColour(): string {
        let hash = 0;
        for (let i = 0; i < this.name().length; i++) {
            hash = this.name().charCodeAt(i) + ((hash << 5) - hash);
        }
        let colour = '#';
        for (let i = 0; i < 3; i++) {
            const value = (hash >> (i * 8)) & 0xFF;
            const stringValue = '00' + value.toString(16);
            colour += stringValue.substring(stringValue.length - 2);
        }
        return colour;
    }

    getIconBackground(): string {
        const url = encodeURI((this.icon() ?? this.preview()) ?? '').replace(/'/g, '%27');
        return `url('${url}')`;
    }

    private getTextColor(): string {
        const hexcolor = this.background().substring(1);
        const r = parseInt(hexcolor.substring(0, 2), 16);
        const g = parseInt(hexcolor.substring(2, 4), 16);
        const b = parseInt(hexcolor.substring(4, 6), 16);
        const yiq = ((r * 299) + (g * 587) + (b * 114)) / 1000;
        return (yiq >= 128) ? 'black' : 'white';
    }
}
