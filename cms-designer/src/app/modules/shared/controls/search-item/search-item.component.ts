import { Component, ElementRef, ViewChild } from '@angular/core';
import { WindowRef } from '@app/services';
import { SearchControlDescriptor } from '@shared/models';
import { RequestItemsService } from '@shared/services';
import { Subject } from 'rxjs';
import { debounceTime, map } from 'rxjs/operators';
import { BaseControlDirective } from '../base-control.component';

@Component({
    selector: 'app-search-item',
    templateUrl: './search-item.component.html',
    styleUrls: ['./search-item.component.scss']
})
export class SearchItemComponent extends BaseControlDirective<SearchControlDescriptor> {

    private searchEvent$ = new Subject<string>();

    searchQuery: string;

    @ViewChild('control') control: ElementRef;

    setValue(value: any) {
        if (!value) {
            value = { __nodata: true, __searchQuery: null };
        }
        super.setValue(value);
    }

    constructor(private windowRef: WindowRef, private requestItemsService: RequestItemsService) {
        super();
        this.searchEvent$.pipe(
            debounceTime(1000)
        ).subscribe(searchQuery => {
            this.searchModel(searchQuery);
        });
    }

    initContent() {
        if (this.value) {
            this.searchQuery = this.value.__searchQuery;
        }
    }

    private searchModel(query: string) {
        this.requestItemsService.doSearchRequest(this.descriptor.request, query).pipe(
            map(items => Array.isArray(items) && items.length ? items[0] : { __nodata: true })
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
