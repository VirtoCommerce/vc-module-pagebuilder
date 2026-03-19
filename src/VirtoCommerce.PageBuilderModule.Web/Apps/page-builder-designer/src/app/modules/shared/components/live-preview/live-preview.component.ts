import { Component, DestroyRef, ElementRef, OnInit, signal, viewChild, inject } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { Store } from '@ngrx/store';

import { EventsBusService } from '@core/services';
import { AppConfig } from '@integration/services';

import { BuilderState } from '@shared/store';
import * as fromRoute from '@shared/routing';
import { NgClass } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';

@Component({
    selector: 'app-live-preview',
    templateUrl: './live-preview.component.html',
    styleUrls: ['./live-preview.component.scss'],
    imports: [NgClass]
})
export class LivePreviewComponent implements OnInit {

    private readonly destroyRef = inject(DestroyRef);
    private readonly store = inject(Store<BuilderState>);
    private readonly sanitizer = inject(DomSanitizer);
    private readonly eventsBus = inject(EventsBusService);
    private readonly config = inject(AppConfig);

    readonly frame = viewChild<ElementRef>('frame');

    private readonly previewLoaded = signal(false);
    private readonly pendingMessages: any[] = [];


    isPresetPreviewMode = toSignal(this.store.select(fromRoute.isPresetPreviewMode), { initialValue: false });
    previewPresetName = toSignal(this.store.select(fromRoute.selectPresetParameter), { initialValue: null });
    previewMode = toSignal(this.store.select(fromRoute.selectPreviewModeParameter), { initialValue: null });

    previewUrl!: SafeResourceUrl;
    url!: string;

    ngOnInit(): void {

        // + page - refresh whole page
        // + preview
        // + select
        // + changed - update
        // + add
        // + remove
        // + hover
        // + clone
        // + insert
        // + swap
        // + reload
        // + hide
        // + show

        const sub = this.eventsBus.on(args => args.target === 'preview', msg => {
            switch (msg.payload?.type) {
                case 'preview-loaded':
                    this.previewLoaded.set(true);
                    this.pendingMessages.splice(0).forEach(msg => this.doSend(msg));
                    break;
                default:
                    this.sendMessage(msg.payload);
                    break;
            }
        });
        this.destroyRef.onDestroy(() => sub.unsubscribe());
        this.url = this.config.getValue('fullPreviewUrl');
        this.previewUrl = this.sanitizer.bypassSecurityTrustResourceUrl(this.url);
    }

    private sendMessage(msg: any) {
        if (this.previewLoaded()) {
            this.doSend(msg);
        } else {
            this.pendingMessages.push(msg);
        }
    }

    private doSend(msg: any) {
        const frame = this.frame()?.nativeElement as HTMLIFrameElement | undefined;
        if (!frame) return;
        const message = { ...msg, source: 'builder' };
        if (message.type !== 'hover') {
            console.log(message);
        }
        frame.contentWindow?.postMessage(message, this.url);
    }
}
