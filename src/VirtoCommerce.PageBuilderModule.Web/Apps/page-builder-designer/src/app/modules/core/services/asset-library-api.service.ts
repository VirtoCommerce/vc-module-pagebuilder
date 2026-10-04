import { inject, Injectable } from '@angular/core';
import { map, Observable, throwError } from 'rxjs';

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
        const context = { parentUrl, name };
        const request = this.http.generateRequest(this.appConfig.getContext().config.assetLibraryCreateFolderRequest, null, context);
        // A fallback chain can repeat the POST after an empty 204 response.
        if (!request || typeof request === 'string' || Array.isArray(request)
            || typeof request.url !== 'string' || !request.url.trim()
            || typeof request.method !== 'string' || request.method.toUpperCase() !== 'POST') {
            return throwError(() => new Error('Folder creation requires a configured POST request.'));
        }
        return this.http.doRequest<void>(request, { nullWhenError: false }, context).pipe(
            map(() => undefined)
        );
    }

    canCreateFolder(): boolean {
        this.appConfig.version();
        const request = this.appConfig.getContext().config.assetLibraryCreateFolderRequest;
        return this.appConfig.getValue('canCreateAssets') === true
            && !!request && !Array.isArray(request)
            && typeof request.url === 'string' && !!request.url.trim()
            && typeof request.method === 'string' && request.method.toUpperCase() === 'POST';
    }

    canUpload(): boolean {
        this.appConfig.version();
        return this.appConfig.getValue('canCreateAssets') === true
            && !!this.appConfig.getContext().config.assetLibraryUploadRequest;
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
