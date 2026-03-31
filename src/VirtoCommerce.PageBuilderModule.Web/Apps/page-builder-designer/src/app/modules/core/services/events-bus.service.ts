import { Injectable, Predicate, inject } from "@angular/core";
import { Store } from "@ngrx/store";
import { Subject, Subscription, tap, filter } from "rxjs";
import { EventBusArgs } from "../models";

export type Enricher = {
    filter: (item: any) => boolean;
    selector: (item: any) => any;
    reducer: (model: any, item: any) => any;
};

@Injectable({
    providedIn: 'root'
})
export class EventsBusService {
    private store$ = inject(Store);
    private events$ = new Subject<any>();
    // private selectors: Enricher[] = [];

    // addStateSelector(...selectors: Enricher[]) {
    //     this.selectors.push(...selectors);
    // }

    on(matcher: Predicate<EventBusArgs>, handler: (item: EventBusArgs) => void): Subscription {
        return this.events$
            .asObservable()
            .pipe(
                filter(matcher),
                // withLatestFrom(
                //     ...this.selectors.map(x => this.store$.select(x.selector))
                // ),
                // map(items => this.selectors.reduce((acc, x, i) => x.reducer(acc, items[i + 1]), items[0])),
                tap(handler)
            )
            .subscribe();
    }

    emit(item: EventBusArgs) {
        this.events$.next(item);
    }
}
