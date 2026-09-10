import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, computed, effect, inject, input, viewChild } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { OzAgentTransportService } from '../../services/oz-agent-transport.service';
import { OzAgentUiService } from '../../services/oz-agent-ui.service';

@Component({
    selector: 'app-oz-agent-iframe',
    templateUrl: './oz-agent-iframe.component.html',
    styleUrls: ['./oz-agent-iframe.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OzAgentIframeComponent {

    private readonly sanitizer = inject(DomSanitizer);
    private readonly transport = inject(OzAgentTransportService);
    private readonly ui = inject(OzAgentUiService);
    private readonly destroyRef = inject(DestroyRef);

    readonly url = input<string | null>(null);

    // An iframe src cannot go through Angular's sanitizer — it would strip the URL — so framing an
    // external app means bypassing it. The bypass is confined to the OZ deployment the platform setting
    // names: anything not on that origin (or not http(s)) is refused and the frame stays empty, so a
    // poisoned config value cannot turn this into an arbitrary-page frame.
    readonly safeUrl = computed<SafeResourceUrl | null>(() => {
        const raw = this.url();
        return raw && this.isConfiguredOzUrl(raw) ? this.sanitizer.bypassSecurityTrustResourceUrl(raw) : null;
    });

    private isConfiguredOzUrl(raw: string): boolean {
        const configured = this.ui.agentUrl;
        if (!configured) {
            return false;
        }
        try {
            const target = new URL(raw, globalThis.location.href);
            const allowed = new URL(configured, globalThis.location.href);
            return (target.protocol === 'https:' || target.protocol === 'http:') && target.origin === allowed.origin;
        } catch {
            return false;
        }
    }

    private readonly frame = viewChild<ElementRef<HTMLIFrameElement>>('frame');

    constructor() {
        effect(() => {
            const el = this.frame()?.nativeElement ?? null;
            this.transport.setIframe(el);
        });
        this.destroyRef.onDestroy(() => this.transport.setIframe(null));
    }
}
