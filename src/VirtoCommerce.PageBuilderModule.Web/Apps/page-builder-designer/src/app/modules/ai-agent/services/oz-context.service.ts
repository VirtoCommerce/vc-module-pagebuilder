import { inject, Injectable } from '@angular/core';
import { Store } from '@ngrx/store';
import { AppConfig, JwtStorageService } from '@integration/services';
import { BuilderState, selectGroupIdParameter, selectPathParameter } from '@shared/routing';
import type { OzBladeContext, OzInitContextPayload } from '../types';

// OZ requires a non-empty accessToken (Zod min(1)); keep handshake working in dev
// even when the user is not logged in yet.
const FALLBACK_ACCESS_TOKEN = 'dev';
const FALLBACK_USER_ID = 'pagebuilder-user';

// Object types the agent's pagebuilder tools match on — the same ones the shell sends,
// so a tool cannot tell which app the context came from.
const STORE_OBJECT_TYPE = 'pagebuilder.store';
const PAGE_OBJECT_TYPE = 'pagebuilder.page';

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
    private readonly appConfig = inject(AppConfig);
    private readonly store = inject(Store<BuilderState>);

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
            items: this.buildItems(),
        };
    }

    // Store first, page second. Without the store item the agent has no store id at all and has to
    // ask for one (or guess); without the page item "edit this page" has nothing to act on. Both are
    // omitted when unknown rather than sent empty — an item with no id is worse than no item.
    private buildItems(): Record<string, unknown>[] {
        const items: Record<string, unknown>[] = [];

        const storeId = this.getStoreId();
        if (storeId) {
            items.push({
                id: storeId,
                objectType: STORE_OBJECT_TYPE,
                name: `Current store: ${storeId}`,
            });
        }

        // The router feature lives in the root store, so this works before the lazy editor module
        // is loaded — the chat can be opened from anywhere in the app.
        const path = this.store.selectSignal(selectPathParameter)();
        const groupId = this.store.selectSignal(selectGroupIdParameter)();
        const pageId = groupId || path;
        if (pageId) {
            items.push({
                id: pageId,
                objectType: PAGE_OBJECT_TYPE,
                name: this.pageNameFrom(path) || pageId,
            });
        }

        return items;
    }

    private getStoreId(): string | null {
        const context = this.appConfig.getContext() as { location?: { params?: { storeId?: string } } } | null;
        return context?.location?.params?.storeId || null;
    }

    // "pages/blogs/foo.page" -> "foo"; the designer knows the page by its path, and the bare file
    // name is what the editor shows the user.
    private pageNameFrom(path: string): string {
        return path?.split('/').pop()?.split('.').slice(0, -1).join('.') || '';
    }
}
