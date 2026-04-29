import { inject, Injectable } from '@angular/core';
import { JwtStorageService } from '@integration/services';
import type { OzBladeContext, OzInitContextPayload } from '../types';

// OZ requires a non-empty accessToken (Zod min(1)); keep handshake working in dev
// even when the user is not logged in yet.
const FALLBACK_ACCESS_TOKEN = 'dev';
const FALLBACK_USER_ID = 'pagebuilder-user';

// Designer has no blades, but OZ requires the field as a context identifier.
// Static marker saying "I am the page-builder app".
const PAGE_BUILDER_BLADE: OzBladeContext = {
    id: 'page-builder',
    name: 'page-builder',
    title: 'Page Builder',
};

@Injectable({ providedIn: 'root' })
export class OzContextService {

    private readonly jwt = inject(JwtStorageService);

    buildInitPayload(): OzInitContextPayload {
        const info = this.jwt.getInfo();
        const accessToken = (info?.token as string | undefined) || FALLBACK_ACCESS_TOKEN;
        const userId = (info?.userName as string | undefined) || FALLBACK_USER_ID;
        return {
            accessToken,
            userId,
            locale: (navigator.language || 'en').slice(0, 2),
            blade: PAGE_BUILDER_BLADE,
            contextType: 'list',
            items: [],
        };
    }
}
