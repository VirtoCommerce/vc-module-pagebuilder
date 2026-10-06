import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';

import { AppConfig, BuilderHttpClient } from '@integration/services';
import { assetLibraryHelpers } from '@core/helpers';

import { AssetLibraryEntry, AssetLibraryReferencesSearchResult, AssetLibrarySearchResult, AssetLibrarySearchOptions } from './asset-library.models';

@Injectable({
    providedIn: 'root'
})
export class AssetLibraryApiService {

    private readonly http = inject(BuilderHttpClient);
    private readonly appConfig = inject(AppConfig);

    search(folderUrl: string, keyword?: string, options: AssetLibrarySearchOptions = {}): Observable<AssetLibrarySearchResult> {
        const searchTerm = keyword?.trim() ?? '';
        const context = { folderUrl, keyword: searchTerm, skip: 0, take: 20, sort: 'name', acceptedTypes: [], ...options };
        return this.doConfiguredRequest<Partial<AssetLibrarySearchResult>>('assetLibrarySearchRequest', context).pipe(
            map(response => this.toSearchResult(response ?? {}, context))
        );
    }

    upload(folderUrl: string, file: File): Observable<AssetLibraryEntry | null> {
        return this.doConfiguredRequest<AssetLibraryEntry[]>('assetLibraryUploadRequest', { folderUrl, file }, file).pipe(
            map(response => this.normalizeEntry(response?.[0] ?? null))
        );
    }

    searchReferences(storeId: string, assetUrls: string[]): Observable<AssetLibraryReferencesSearchResult> {
        return this.doConfiguredRequest<AssetLibraryReferencesSearchResult>('assetLibraryReferencesRequest', { storeId, assetUrls }).pipe(
            map(response => response ?? { totalCount: 0, results: [] })
        );
    }

    private doConfiguredRequest<T>(property: 'assetLibrarySearchRequest' | 'assetLibraryUploadRequest' | 'assetLibraryReferencesRequest', context: any, data: any = null): Observable<T | null> {
        const request = this.appConfig.getContext().config[property];
        const serverRequest = this.http.generateRequest(request, data, context);
        return this.http.doRequest<T>(serverRequest, { nullWhenError: false }, context);
    }

    private toSearchResult(response: Partial<AssetLibrarySearchResult>, options: AssetLibrarySearchOptions & { keyword: string }): AssetLibrarySearchResult {
        const results = response.results ?? [];
        const totalCount = response.totalCount ?? results.length;
        // Old/custom descriptors may return the entire listing. Preserve their route and adapt
        // the complete response before filtering, counting and paging. Paged responses stay intact.
        if (response.skip !== undefined || (results.length <= (options.take ?? 20) && totalCount > results.length)) {
            return { ...response, totalCount, results };
        }
        const filtered = results.filter(entry => this.matchesSearch(entry, options));
        const sorted = filtered.sort((a, b) => this.compareEntries(a, b, options.sort ?? 'name'));
        const take = options.take ?? 20;
        let skip = Math.min(options.skip ?? 0, Math.max(0, Math.ceil(sorted.length / take) - 1) * take);
        const preferredIndex = options.preferredAssetUrl
            ? sorted.findIndex(entry => entry.relativeUrl === options.preferredAssetUrl || entry.url === options.preferredAssetUrl)
            : -1;
        if (preferredIndex >= 0) {
            skip = Math.floor(preferredIndex / take) * take;
        }
        return {
            totalCount: sorted.length,
            fileCount: sorted.filter(entry => entry.type === 'blob').length,
            skip,
            results: sorted.slice(skip, skip + take),
        };
    }

    private matchesSearch(entry: AssetLibraryEntry, options: AssetLibrarySearchOptions & { keyword: string }): boolean {
        if (entry.type !== 'folder' && !assetLibraryHelpers.matchesAcceptFile(
            { name: entry.name, type: entry.contentType ?? '' }, options.acceptedTypes ?? [])) {
            return false;
        }
        if (options.exactName) {
            return entry.type === 'blob' && assetLibraryHelpers.normalizeAssetFileName(entry.name)
                === assetLibraryHelpers.normalizeAssetFileName(options.exactName);
        }
        return !options.keyword || entry.name.toLowerCase().includes(options.keyword.toLowerCase());
    }

    private compareEntries(a: AssetLibraryEntry, b: AssetLibraryEntry, sort: string): number {
        const folderOrder = Number(b.type === 'folder') - Number(a.type === 'folder');
        if (folderOrder) {
            return folderOrder;
        }
        for (const field of sort.split(';')) {
            const [column, direction] = field.split(':');
            const aValue = this.sortValue(a, column);
            const bValue = this.sortValue(b, column);
            const order = aValue < bValue ? -1 : Number(aValue > bValue);
            if (order) {
                return direction?.toLowerCase() === 'desc' ? -order : order;
            }
        }
        const aKey = `${a.name.toLowerCase()}\0${a.relativeUrl ?? a.url ?? ''}`;
        const bKey = `${b.name.toLowerCase()}\0${b.relativeUrl ?? b.url ?? ''}`;
        return aKey < bKey ? -1 : Number(aKey > bKey);
    }

    private sortValue(entry: AssetLibraryEntry, column: string): string | number {
        switch (column.toLowerCase()) {
            case 'size': return entry.size ?? 0;
            case 'modifieddate': return Date.parse(entry.modifiedDate ?? '') || 0;
            case 'type': return entry.type;
            default: return entry.name.toLowerCase();
        }
    }

    private normalizeEntry(entry: AssetLibraryEntry | null | undefined): AssetLibraryEntry | null {
        if (!entry) {
            return null;
        }

        return entry;
    }
}
