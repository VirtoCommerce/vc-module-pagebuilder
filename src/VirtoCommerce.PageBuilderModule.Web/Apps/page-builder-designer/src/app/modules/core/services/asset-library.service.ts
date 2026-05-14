import { HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { catchError, map, Observable, of, switchMap } from 'rxjs';

import { AppConfig, BuilderHttpClient } from '@integration/services';

export interface AssetLibraryEntry {
    type: 'folder' | 'blob';
    name: string;
    url?: string;
    relativeUrl?: string;
    contentType?: string;
    size?: number;
    referencesCount?: number;
    referencePages?: AssetLibraryReferencePage[];
    modifiedDate?: string;
    createdDate?: string;
}

export interface AssetLibrarySearchResult {
    totalCount: number;
    results: AssetLibraryEntry[];
}

export interface AssetLibraryReferencePage {
    id?: string;
    name?: string;
    permalink?: string;
    cultureName?: string;
    status?: string;
}

interface AssetLibraryReference {
    assetUrl: string;
    normalizedAssetUrl: string;
    referencesCount: number;
    pages?: AssetLibraryReferencePage[];
}

@Injectable({
    providedIn: 'root'
})
export class AssetLibraryService {

    private readonly http = inject(BuilderHttpClient);
    private readonly appConfig = inject(AppConfig);

    getRootFolderUrl(context: any = null): string | null {
        const appContext = this.appConfig.getContext();
        const storeId = context?.location?.params?.storeId
            ?? context?.template?.storeId
            ?? context?.model?.storeId
            ?? appContext?.location?.params?.storeId;

        return storeId ? `/stores/${storeId}/Page Builder` : null;
    }

    search(folderUrl: string, keyword?: string): Observable<AssetLibrarySearchResult> {
        let params = new HttpParams().set('folderUrl', folderUrl);
        if (keyword?.trim()) {
            params = params.set('keyword', keyword.trim());
        }

        return this.http.get<Partial<AssetLibrarySearchResult>>('/api/assets', { params }).pipe(
            map(response => ({
                totalCount: response.totalCount ?? response.results?.length ?? 0,
                results: (response.results ?? []).map(entry => ({
                    ...entry,
                    referencesCount: entry.type === 'blob' ? entry.referencesCount ?? 0 : undefined
                }))
            })),
            switchMap(result => this.enrichReferences(result))
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

    getPublicUrl(entry: AssetLibraryEntry): string | null {
        if (entry.relativeUrl) {
            return this.toAbsoluteUrl(`/assets${this.ensureLeadingSlash(entry.relativeUrl)}`);
        }

        if (!entry.url) {
            return null;
        }

        try {
            return this.toAbsoluteUrl(entry.url);
        } catch {
            return entry.url;
        }
    }

    getPreviewUrl(entry: AssetLibraryEntry): string | null {
        const publicUrl = this.getPublicUrl(entry);
        if (!publicUrl) {
            return null;
        }

        if (!entry.modifiedDate) {
            return publicUrl;
        }

        const separator = publicUrl.includes('?') ? '&' : '?';
        return `${publicUrl}${separator}t=${encodeURIComponent(entry.modifiedDate)}`;
    }

    isImage(entry: AssetLibraryEntry): boolean {
        return entry.contentType?.startsWith('image/') === true
            || /\.(apng|avif|gif|jpg|jpeg|png|svg|webp)$/i.test(entry.name);
    }

    private ensureLeadingSlash(value: string): string {
        return value.startsWith('/') ? value : `/${value}`;
    }

    private toAbsoluteUrl(value: string): string {
        return new URL(value, this.appConfig.getContext().location.origin).toString();
    }

    private normalizeEntry(entry: AssetLibraryEntry | null): AssetLibraryEntry | null {
        if (!entry) {
            return null;
        }

        return {
            ...entry,
            referencesCount: entry.type === 'blob' ? entry.referencesCount ?? 0 : undefined
        };
    }

    private enrichReferences(result: AssetLibrarySearchResult): Observable<AssetLibrarySearchResult> {
        const context = this.appConfig.getContext();
        const storeId = context?.location?.params?.storeId;
        const assetUrls = result.results
            .filter(entry => entry.type === 'blob')
            .map(entry => entry.relativeUrl || entry.url)
            .filter((url): url is string => !!url);

        if (!storeId || !assetUrls.length) {
            return of(result);
        }

        return this.http.post<{ results?: AssetLibraryReference[] }>('/api/page-builder-assets/references', {
            storeId,
            assetUrls,
            includePages: false
        }).pipe(
            map(response => this.applyReferences(result, response.results ?? [])),
            catchError(() => of(result))
        );
    }

    private applyReferences(result: AssetLibrarySearchResult, references: AssetLibraryReference[]): AssetLibrarySearchResult {
        const referencesByUrl = references.reduce<Record<string, AssetLibraryReference>>((map, reference) => {
            this.getReferenceKeys(reference.assetUrl, reference.normalizedAssetUrl).forEach(key => {
                map[key] = reference;
            });
            return map;
        }, {});

        return {
            ...result,
            results: result.results.map(entry => {
                const reference = this.getEntryReferenceKeys(entry)
                    .map(key => referencesByUrl[key])
                    .find(Boolean);

                return reference
                    ? {
                        ...entry,
                        referencesCount: reference.referencesCount,
                        referencePages: reference.pages ?? []
                    }
                    : entry;
            })
        };
    }

    private getEntryReferenceKeys(entry: AssetLibraryEntry): string[] {
        return this.getReferenceKeys(entry.relativeUrl, entry.url, this.getPublicUrl(entry));
    }

    private getReferenceKeys(...values: Array<string | null | undefined>): string[] {
        return Array.from(new Set(values
            .flatMap(value => [value, this.normalizeAssetUrl(value)])
            .filter((value): value is string => !!value)));
    }

    private normalizeAssetUrl(value: string | null | undefined): string | null {
        if (!value) {
            return null;
        }

        let normalized = value.trim();
        if (!normalized) {
            return null;
        }

        try {
            const parsedUrl = new URL(normalized, window.location.origin);
            normalized = parsedUrl.pathname;
        } catch {
            normalized = normalized.split(/[?#]/, 1)[0];
        }

        if (normalized.toLowerCase().startsWith('/assets/')) {
            normalized = normalized.slice('/assets'.length);
        }

        try {
            normalized = decodeURIComponent(normalized);
        } catch {
            // Keep the original value if it contains invalid escape sequences.
        }

        return this.ensureLeadingSlash(normalized);
    }
}
