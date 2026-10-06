import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';

import { AppConfig, BuilderHttpClient } from '@integration/services';
import { ServerRequestDescriptor } from '@models/http';

import { AssetLibraryEntry, AssetLibraryReferencesSearchResult, AssetLibrarySearchResult, AssetLibrarySearchOptions } from './asset-library.models';

@Injectable({
    providedIn: 'root'
})
export class AssetLibraryApiService {

    private readonly http = inject(BuilderHttpClient);
    private readonly appConfig = inject(AppConfig);

    search(folderUrl: string, keyword?: string, options: AssetLibrarySearchOptions = {}): Observable<AssetLibrarySearchResult> {
        const searchTerm = keyword?.trim() ?? '';
        return this.doConfiguredRequest<Partial<AssetLibrarySearchResult>>('assetLibrarySearchRequest', {
            folderUrl, keyword: searchTerm, skip: 0, take: 20, sort: 'name', acceptedTypes: [], ...options,
        }).pipe(
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
        let serverRequest = this.http.generateRequest(request, data, context);
        if (property === 'assetLibrarySearchRequest') {
            serverRequest = Array.isArray(serverRequest)
                ? serverRequest.map(item => this.migrateLegacySearchRequest(item, context))
                : this.migrateLegacySearchRequest(serverRequest, context);
        }
        return this.http.doRequest<T>(serverRequest, { nullWhenError: false }, context);
    }

    private migrateLegacySearchRequest(request: string | ServerRequestDescriptor | null, context: any): string | ServerRequestDescriptor | null {
        if (!request || typeof request === 'string' || request.method.toUpperCase() !== 'GET') {
            return request;
        }
        const [url, query] = request.url.split('?');
        if (!/\/api\/assets\/?$/i.test(url)) {
            return request;
        }
        const params = new URLSearchParams(query);
        const folderUrl = params.get('folderUrl') ?? context.folderUrl;
        const keyword = params.get('keyword') ?? context.keyword;
        params.delete('folderUrl');
        params.delete('keyword');
        const remainingQuery = params.toString();
        return {
            ...request,
            url: url.replace(/\/api\/assets\/?$/i, '/api/page-builder-assets/search') + (remainingQuery ? `?${remainingQuery}` : ''),
            method: 'POST',
            body: { ...context, folderUrl, keyword },
        };
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
