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

    async getData(requestPermission = true): Promise<ClipboardModel | null> {
        try {
            // Checking menu availability must not wait for an interactive permission prompt.
            if (!requestPermission) {
                const permissions = this.environment.navigator.permissions;
                if (!permissions) {
                    return null;
                }
                const permission = await permissions.query({ name: 'clipboard-read' as PermissionName });
                if (permission.state !== 'granted') {
                    return null;
                }
            }
            const data = await this.environment.navigator.clipboard.readText();
            if (!data) {
                return null;
            }
            try {
                const result = <ClipboardModel>JSON.parse(data);
                if ((result?.type !== 'section' && result?.type !== 'block')
                    || !result.content || typeof result.content !== 'object' || Array.isArray(result.content)
                    || typeof result.content.type !== 'string' || !result.content.type.trim()) {
                    return { wrongData: true, sourceContent: data };
                }
                return { ...result, sourceContent: data };
            } catch (error) {
                return <ClipboardModel>{ wrongData: true, sourceContent: data };
            }
        } catch (error) {
            console.log(error);
            return null; // can't access clipboard
        }
    }
}
