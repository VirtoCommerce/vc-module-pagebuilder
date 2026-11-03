import { inject } from "@angular/core";
import { CanActivateFn } from "@angular/router";
import { Store } from "@ngrx/store";
import { filter, map, tap, take } from "rxjs";

import { BuilderState } from '@shared/store/state';
import * as actions from '@shared/store/actions';
import * as selectors from '@shared/store/selectors';

export const preloadDataGuard: CanActivateFn = () => {
    const store = inject(Store<BuilderState>);

    return store.select(selectors.selectTemplatesEntriesAsList).pipe(
        tap(templates => {
            if (!templates?.length) {
                store.dispatch(actions.loadTemplateEntries());
            }
        }),
        filter(templates => !!templates?.length),
        take(1),
        map(() => true)
    );
};
