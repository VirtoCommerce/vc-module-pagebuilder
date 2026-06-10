import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, computed, effect, inject, input, viewChild } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { OzAgentTransportService } from '../../services/oz-agent-transport.service';

@Component({
    selector: 'app-oz-agent-iframe',
    templateUrl: './oz-agent-iframe.component.html',
    styleUrls: ['./oz-agent-iframe.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OzAgentIframeComponent {

    private readonly sanitizer = inject(DomSanitizer);
    private readonly transport = inject(OzAgentTransportService);
    private readonly destroyRef = inject(DestroyRef);

    readonly url = input<string | null>(null);

    readonly safeUrl = computed<SafeResourceUrl | null>(() => {
        const raw = this.url();
        return raw ? this.sanitizer.bypassSecurityTrustResourceUrl(raw) : null;
    });

    private readonly frame = viewChild<ElementRef<HTMLIFrameElement>>('frame');

    constructor() {
        effect(() => {
            const el = this.frame()?.nativeElement ?? null;
            this.transport.setIframe(el);
        });
        this.destroyRef.onDestroy(() => this.transport.setIframe(null));
    }
}
