import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';

import { AppConfig, BuilderHttpClient } from '@integration/services';

import { AssetLibraryEntry, AssetLibraryReferencesSearchResult, AssetLibrarySearchResult } from './asset-library.models';

@Injectable({
    providedIn: 'root'
})
export class AssetLibraryApiService {

    private readonly http = inject(BuilderHttpClient);
    private readonly appConfig = inject(AppConfig);

    search(folderUrl: string, keyword?: string): Observable<AssetLibrarySearchResult> {
        const searchTerm = keyword?.trim() ?? '';
        return this.doConfiguredRequest<Partial<AssetLibrarySearchResult>>('assetLibrarySearchRequest', { folderUrl, keyword: searchTerm }).pipe(
            map(response => this.toSearchResult(response ?? {}))
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
        const request = this.appConfig.getValue(property, context);
        const serverRequest = this.http.generateRequest(request, data, context);
        return this.http.doRequest<T>(serverRequest, { nullWhenError: false }, context);
    }

    private toSearchResult(response: Partial<AssetLibrarySearchResult>): AssetLibrarySearchResult {
        const results = (response.results ?? [])
            .map(entry => this.normalizeEntry(entry))
            .filter((entry): entry is AssetLibraryEntry => !!entry);

        return {
            totalCount: response.totalCount ?? results.length,
            results
        };
    }

    private normalizeEntry(entry: AssetLibraryEntry | null | undefined): AssetLibraryEntry | null {
        if (!entry) {
            return null;
        }

        return entry;
    }
}
