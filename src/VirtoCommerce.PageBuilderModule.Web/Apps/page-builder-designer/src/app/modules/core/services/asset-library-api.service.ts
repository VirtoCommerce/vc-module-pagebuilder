import { inject, Injectable } from '@angular/core';
import { defer, map, Observable, throwError } from 'rxjs';

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

    createFolder(parentUrl: string, name: string): Observable<void> {
        // Request preparation failures must reach the picker's error handler too.
        return defer(() => this.doConfiguredRequest<void>('assetLibraryCreateFolderRequest', { parentUrl, name })).pipe(
            map(() => undefined)
        );
    }

    searchReferences(storeId: string, assetUrls: string[]): Observable<AssetLibraryReferencesSearchResult> {
        return this.doConfiguredRequest<AssetLibraryReferencesSearchResult>('assetLibraryReferencesRequest', { storeId, assetUrls }).pipe(
            map(response => response ?? { totalCount: 0, results: [] })
        );
    }

    private doConfiguredRequest<T>(property: 'assetLibrarySearchRequest' | 'assetLibraryUploadRequest' | 'assetLibraryReferencesRequest' | 'assetLibraryCreateFolderRequest', context: any, data: any = null): Observable<T | null> {
        const request = this.appConfig.getValue(property, context);
        const serverRequest = this.http.generateRequest(request, data, context);
        // A missing request emits null in BuilderHttpClient, indistinguishable from HTTP 204.
        // Require one POST so a no-op or a fallback GET cannot masquerade as folder creation.
        if (property === 'assetLibraryCreateFolderRequest'
            && (!serverRequest || typeof serverRequest === 'string' || Array.isArray(serverRequest)
                || typeof serverRequest.url !== 'string' || !serverRequest.url.trim()
                || typeof serverRequest.method !== 'string' || serverRequest.method.toUpperCase() !== 'POST')) {
            return throwError(() => new Error('Folder creation requires a configured POST request.'));
        }
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
