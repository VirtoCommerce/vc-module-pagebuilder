import { Component, ElementRef, ViewChild, OnDestroy } from '@angular/core';
import { ApiUrlsService, WindowRef } from '@app/services';
import { SearchControlDescriptor } from '@shared/models';
import { RequestItemsService } from '@shared/services';
import { Subject, Subscription } from 'rxjs';
import { debounceTime, map } from 'rxjs/operators';
import { BaseControlDirective } from '../base-control.component';

import { cloneDeep } from 'lodash-es';

@Component({
    selector: 'app-search-item',
    templateUrl: './search-item.component.html',
    styleUrls: ['./search-item.component.scss']
})
export class SearchItemComponent extends BaseControlDirective<SearchControlDescriptor> implements OnDestroy {

    private searchEvent$ = new Subject<string>();

    searchQuery: string;

    @ViewChild('control') control: ElementRef;

    setValue(value: any) {
        if (!value) {
            value = { __nodata: true, __searchQuery: null };
        }
        super.setValue(value);
    }

    private subscription: Subscription = null;

    constructor(
        private windowRef: WindowRef,
        private requestItemsService: RequestItemsService,
        private urls: ApiUrlsService) {
        super();
        this.subscription = this.searchEvent$.pipe(
            debounceTime(1000)
        ).subscribe(searchQuery => {
            this.searchModel(searchQuery);
        });
    }

    ngOnDestroy(): void {
        if (this.subscription && !this.subscription.closed) {
            this.subscription.unsubscribe();
            this.subscription = null;
        }
    }

    initContent() {
        if (this.value) {
            this.searchQuery = this.value.__searchQuery;
        }
    }

    getAssetUrl(value): string {
        if (!value) return null;
        if (typeof value === 'string') {
            return this.urls.getAssetsUrl(value);
        } else if (value.url) {
            return this.urls.getAssetsUrl(value.url);
        }
        return null;
    }

    private searchModel(query: string) {
        const context = cloneDeep(this.context);
        context.__searchQuery = query;
        this.requestItemsService.doSearchRequest(this.descriptor.request, context).pipe(
            map(item => item || { __nodata: true })
        ).subscribe(result => {
            result['__searchQuery'] = query;
            this.setValue(result);
            this.onChange(result);
        });
    }

    onPaste(event) {
        const value = (event.clipboardData || this.windowRef.nativeWindow.clipboardData).getData('text');
        this.onTextChange(value);
    }

    getFocusableControl(): ElementRef {
        return this.control;
    }

    onTextChange(value: string) {
        this.searchEvent$.next(value);
    }
}
