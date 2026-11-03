import { RouterStateSerializer } from '@ngrx/router-store';
import { RouterStateSnapshot } from '@angular/router';

import { RouterStateUrl } from '.';

export class RouterSerializer implements RouterStateSerializer<RouterStateUrl> {
    serialize(routerState: RouterStateSnapshot): RouterStateUrl {
        let route = routerState.root;
        while (route.firstChild) {
            route = route.firstChild;
        }
        const { url, root: { queryParams } } = routerState;
        const { params, data } = route;

        // Exclude function/class references from data: NgRx deep-freezes state in dev mode,
        // which would freeze component classes and break Angular's DI metadata assignment.
        const serializableData = Object.fromEntries(
            Object.entries(data).filter(([, v]) => typeof v !== 'function')
        );

        return { url, params, queryParams, data: serializableData, isEmpty: false };
    }
}
