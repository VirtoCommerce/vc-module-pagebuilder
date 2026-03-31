import { inject, Injectable } from "@angular/core";
import { Clipboard } from '@angular/cdk/clipboard';

import { EnvironmentRef } from '@integration/services'
import { ClipboardModel } from '@core/models';

@Injectable({
    providedIn: 'root'
})
export class ClipboardService {

    private readonly clipboard = inject(Clipboard);
    private readonly environment = inject(EnvironmentRef);

    copy(data: ClipboardModel) {
        this.copyString(JSON.stringify(data));
    }

    copyString(value: string | null) {
        if (value) {
            this.clipboard.copy(value);
        }
    }

    async getData(): Promise<ClipboardModel | null> {
        try {
            const data = await this.environment.navigator.clipboard.readText();
            if (!data) {
                return null;
            }
            try {
                const result = <ClipboardModel>JSON.parse(data);
                result.sourceContent = data;
                return result;
            } catch (error) {
                return <ClipboardModel>{ wrongData: true, sourceContent: data };
            }
        } catch (error) {
            console.log(error);
            return null; // can't access clipboard
        }
    }
}
