import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';

@Component({
    selector: 'app-oz-agent-iframe',
    templateUrl: './oz-agent-iframe.component.html',
    styleUrls: ['./oz-agent-iframe.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OzAgentIframeComponent {

    private readonly sanitizer = inject(DomSanitizer);

    readonly url = input<string | null>(null);

    readonly safeUrl = computed<SafeResourceUrl | null>(() => {
        const raw = this.url();
        return raw ? this.sanitizer.bypassSecurityTrustResourceUrl(raw) : null;
    });
}
