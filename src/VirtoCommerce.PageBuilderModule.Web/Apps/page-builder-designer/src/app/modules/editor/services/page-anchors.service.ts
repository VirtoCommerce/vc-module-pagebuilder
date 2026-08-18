import { Injectable, OnDestroy, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Store } from '@ngrx/store';

import { PageAnchor, PageAnchorsProvider, clearActivePageAnchorsProvider } from '@core/services';
import { BuilderState } from '@editor/store/state';
import * as selectors from '@editor/store/selectors';

/**
 * Publishes the anchors of the page being edited to the rich-text controls.
 *
 * Provided by the editor route rather than at root, because it reads the editor feature state that
 * only exists once that route is activated.
 */
@Injectable()
export class PageAnchorsService implements PageAnchorsProvider, OnDestroy {

    private readonly store$ = inject(Store<BuilderState>);

    private readonly anchors = toSignal(this.store$.select(selectors.selectPageAnchors), { initialValue: <PageAnchor[]>[] });

    getAnchors(): PageAnchor[] {
        return this.anchors();
    }

    /**
     * Rich-text controls reach this service through a registry that survives the editor route, so it
     * has to step down explicitly — otherwise a link dialog opened in another module, a theme for
     * instance, would keep listing the anchors of the page that was open last instead of its own.
     */
    ngOnDestroy(): void {
        clearActivePageAnchorsProvider(this);
    }
}
