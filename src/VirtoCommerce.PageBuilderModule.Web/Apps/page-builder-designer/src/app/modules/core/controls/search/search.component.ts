import { Component, DestroyRef, ElementRef, viewChild, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { JsonPipe } from '@angular/common';
import { IconButtonComponent } from '@core/components/icon-button/icon-button.component';

import { from, NextObserver, Subject } from 'rxjs';
import { concatMap, debounceTime, map } from 'rxjs/operators';

import { BaseControlDirective } from '@core/controls/base-control.directive';
import { AssetsService, DataService } from '@core/services';
import { EnvironmentRef } from '@integration/services';
import { DisplaySearchResult, SearchDescriptor } from '@models/controls';

import { appHelpers } from '@integration/helpers';

@Component({
    selector: 'app-search',
    templateUrl: './search.component.html',
    styleUrls: ['./search.component.scss'],
    imports: [JsonPipe, IconButtonComponent]
})
export class SearchComponent extends BaseControlDirective<SearchDescriptor> {
    private readonly destroyRef = inject(DestroyRef);
    private readonly environment = inject(EnvironmentRef);
    private readonly data = inject(DataService);
    private readonly assets = inject(AssetsService);
    private searchEvent$ = new Subject<string | null>();

    readonly control = viewChild.required<ElementRef>('control');

    protected override applyNewValue(): void {
        if (!this.controlValue()) {
            this.controlValue.set({ __nodata: true, __searchQuery: null });
        }
    }

    override initContent() {
        this.searchEvent$.pipe(
            takeUntilDestroyed(this.destroyRef),
            debounceTime(this.descriptor?.debounceTime || 1000) // move to settings
        ).subscribe({
            next: (searchQuery) => {
                this.searchModel(searchQuery);
            }
        });
    }

    getButtonText(): string {
        if (this.descriptor?.button !== true) {
            return <string>this.descriptor?.button;
        }
        return 'Search';
    }

    doRequest() {
        this.searchEvent$.next(null);
    }

    private searchModel(query: string | null) {
        let value = { __nodata: true, __searchQuery: query };
        const context = { ...this.context, __searchQuery: query };

        const observer: NextObserver<any> = {
            next: ({ key, result }) => {
                value = { ...value, __nodata: value.__nodata && !result, [key]: result };
            },
            error: (error) => {
                console.error(error);
                this.setControlValue(null);
                this.onValueChanged(this.controlValue());
            },
            complete: () => {
                this.setControlValue(value);
                this.onValueChanged(this.controlValue());
            }
        };
        if (!!this.descriptor?.request) {
            this.data.doRequest(this.descriptor.request, context).pipe(
                takeUntilDestroyed(this.destroyRef),
                map(result => ({ key: 'value', result }))
            ).subscribe(observer);
        } else if (!!this.descriptor?.requests) {
            const keys = Object.keys(this.descriptor.requests);
            from(keys).pipe(
                takeUntilDestroyed(this.destroyRef),
                concatMap(key => {
                    const request = this.descriptor!.requests[key];
                    context.item = value;
                    return this.data.doRequest(request, context).pipe(
                        takeUntilDestroyed(this.destroyRef),
                        map(result => ({ key, result }))
                    );
                })
            ).subscribe(observer);
        }
    }

    getAssetUrl(info: DisplaySearchResult): string | null {
        if (!info || !info.path) {
            return null;
        }
        return this.assets.adjustUrl(this.getValueByKey(info), this.context);
    }

    getValueByKey(info: DisplaySearchResult): string | null {
        if (!info || !info.path) {
            return null;
        }
        return appHelpers.getValueByPath(this.controlValue()?.value, info.path);
    }

    isArrayDisplayInfo() {
        return !!this.descriptor?.displayInfo && Array.isArray(this.descriptor.displayInfo);
    }

    onPaste(event: ClipboardEvent) {
        const value = (event.clipboardData || this.environment.nativeWindow.clipboardData).getData('text');
        this.onTextChange(value);
    }

    override getFocusableControl(): ElementRef {
        return this.control();
    }

    onTextChange(event: Event) {
        const element = <HTMLInputElement>event.target;
        const value = element.value;
        this.searchEvent$.next(value);
    }
}
