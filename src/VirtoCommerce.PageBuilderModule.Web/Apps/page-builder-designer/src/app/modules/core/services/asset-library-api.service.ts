import { HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';

import { BuilderHttpClient } from '@integration/services';

import { AssetLibraryEntry, AssetLibrarySearchResult } from './asset-library.models';

@Injectable({
    providedIn: 'root'
})
export class AssetLibraryApiService {

    private readonly http = inject(BuilderHttpClient);

    search(folderUrl: string, keyword?: string): Observable<AssetLibrarySearchResult> {
        return this.http.get<Partial<AssetLibrarySearchResult>>('/api/assets', {
            params: this.createSearchParams(folderUrl, keyword)
        }).pipe(
            map(response => this.toSearchResult(response))
        );
    }

    upload(folderUrl: string, file: File): Observable<AssetLibraryEntry | null> {
        const params = new HttpParams().set('folderUrl', folderUrl);
        const formData = new FormData();
        formData.append('file', file);

        return this.http.post<AssetLibraryEntry[]>('/api/assets', formData, { params }).pipe(
            map(response => this.normalizeEntry(response?.[0] ?? null))
        );
    }

    private createSearchParams(folderUrl: string, keyword?: string): HttpParams {
        let params = new HttpParams().set('folderUrl', folderUrl);
        const searchTerm = keyword?.trim();

        if (searchTerm) {
            params = params.set('keyword', searchTerm);
        }

        return params;
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
