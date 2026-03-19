import { Injectable, inject } from "@angular/core";
import { EventsBusService } from "@app/modules/core/services";
import { Store } from "@ngrx/store";

import * as actions from "@editor/store/actions";

@Injectable({
    providedIn: 'root'
})
export class BroadcastPlatformService {
    private readonly store = inject(Store);
    private readonly eventsBus = inject(EventsBusService);
    private readonly channel = new BroadcastChannel('vc-module-content-channel');

    constructor() {
        this.eventsBus.on(args => args.target === 'platform', (data: any) => {
            this.channel.postMessage(data.payload);
        });

        this.channel.onmessage = (event) => {
            this.store.dispatch(actions.getTemplatePublishStatusSuccess({
                templateKey: event.data.templateKey,
                hasChanges: event.data.hasChanges,
                published: event.data.published
            }));
        };
    }
}
