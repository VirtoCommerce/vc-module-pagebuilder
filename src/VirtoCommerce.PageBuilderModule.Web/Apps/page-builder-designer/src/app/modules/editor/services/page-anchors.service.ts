import { Injectable, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Store } from '@ngrx/store';

import { PageAnchor, PageAnchorsProvider } from '@core/services';
import { BuilderState } from '@editor/store/state';
import * as selectors from '@editor/store/selectors';

/**
 * Publishes the anchors of the page being edited to the rich-text controls.
 *
 * Provided by the editor route rather than at root, because it reads the editor feature state that
 * only exists once that route is activated.
 */
@Injectable()
export class PageAnchorsService implements PageAnchorsProvider {

    private readonly store$ = inject(Store<BuilderState>);

    private readonly anchors = toSignal(this.store$.select(selectors.selectPageAnchors), { initialValue: <PageAnchor[]>[] });

    getAnchors(): PageAnchor[] {
        return this.anchors();
    }
}
