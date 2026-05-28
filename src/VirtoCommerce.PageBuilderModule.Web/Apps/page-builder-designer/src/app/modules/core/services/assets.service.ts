import { from, map, Observable } from 'rxjs';
import { AssetsRequest, UploadAssetDescriptor } from '@models/index';
import { AppConfig, EvaluatorService } from '@integration/services';
import { inject, Injectable } from '@angular/core';
import { AssetFile } from '../models';
import { DataService } from './data.service';
import { AssetUrlService } from './asset-url.service';

import { appHelpers } from '@integration/helpers';

@Injectable({
    providedIn: 'root'
})
export class AssetsService {

    private readonly data = inject(DataService);
    private readonly appConfig = inject(AppConfig);
    private readonly evaluator = inject(EvaluatorService);
    private readonly urls = inject(AssetUrlService);

    uploadAsset(file: AssetFile, descriptor: UploadAssetDescriptor,
        context: any, _progress: (value: number) => void, overridenRequestProps: Partial<AssetsRequest> | null = null): Observable<any> {
        // todo: progress not works
        let uploadContext = this.getUploadContext(context, file);
        let request = this.getRequest(descriptor, uploadContext);
        if (!request || request === 'inline') {
            // in this case we create data-url
            return from(new Promise<string>((resolve, reject) => {
                const reader = new FileReader();
                reader.readAsDataURL(file);
                reader.onload = () => resolve(<string>reader.result);
                reader.onerror = error => reject(error);
            })).pipe(
                map(dataUrl => {
                    file.url = dataUrl;
                    return file;
                })
            );
        }
        if (overridenRequestProps) {
            request = { ...<AssetsRequest>request, ...overridenRequestProps };
        }
        file.assetName = !!request.randomizeAssetName || !file.name
            ? this.randomizeAssetName(file.name, file.type)
            : file.name;
        uploadContext = this.getUploadContext(context, file);
        request = this.getRequest(descriptor, uploadContext); // do it again, thus asset name can impact to request
        return this.data.doRequest(<AssetsRequest>request, uploadContext, file, { nullWhenError: false }).pipe(
            map(response => {
                const req = <AssetsRequest>request;
                if (req.resultTemplate) {
                    file.url = this.evaluator.evaluate(req.resultTemplate, { ...uploadContext, response });
                } else {
                    file.url = this.resolveUploadResponse(response, uploadContext);
                }
                return { ...file, name: file.assetName };
            })
        );
    }

    getPreviewUrl(file: AssetFile, descriptor: UploadAssetDescriptor, context: any): string | null {
        if (!file.url) {
            return null;
        }
        const absoluteOrRelativeUrl = file.url;
        if (['http://', 'https://', '//', 'data:'].find(x => absoluteOrRelativeUrl.startsWith(x))) {
            return absoluteOrRelativeUrl;
        }
        const request = this.getRequest(descriptor, context);
        if (request && typeof request !== 'string' && request.previewTemplate) {
            return this.evaluator.evaluate(request.previewTemplate, { ...file, url: absoluteOrRelativeUrl });
        }
        return this.adjustUrl(file.url, context) || file.url;
    }

    adjustUrl(absoluteOrRelativeUrl: string | null, context: any): string | null {
        if (!absoluteOrRelativeUrl) {
            return null;
        }
        return this.urls.getPublicAssetUrl(absoluteOrRelativeUrl, context) || absoluteOrRelativeUrl;
    }

    isInlineUpload(descriptor: UploadAssetDescriptor, context: any): boolean {
        const request = this.getRequest(descriptor, context);
        return !request || request === 'inline';
    }

    private getRequest(descriptor: UploadAssetDescriptor, context: any): AssetsRequest | 'inline' | null {
        let request = descriptor.uploadAssetsRequest;
        if (!request) {
            request = this.appConfig.getValue('uploadAssetsRequest', context);
        }
        if (!!request && typeof request === 'string' && request !== 'inline') {
            // in this case request is a name of config property
            request = <AssetsRequest>this.appConfig.getValue(<any>request, context);
        }
        return <any>request;
    }

    private getUploadContext(context: any, file: AssetFile): any {
        const result = { ...context, file };

        if (!result.folderUrl) {
            const rootFolderUrl = this.urls.getRootFolderUrl(context);
            if (rootFolderUrl) {
                result.folderUrl = rootFolderUrl;
            }
        }

        return result;
    }

    private resolveUploadResponse(response: any, context: any): any {
        return typeof response === 'string'
            ? this.adjustUrl(response, context) || response
            : response;
    }

    private randomizeAssetName(name: string | null, contentType: string | null = null): string {
        const filename = this.getFilename(name);
        const extension = this.getExtension(name, contentType);
        const suffix = appHelpers.generateUniqueString(10);
        const uniqueName = `${filename}_${suffix}.${extension}`;
        return encodeURIComponent(uniqueName);
    }

    private getExtension(filename: string | null, contentType: string | null): string {
        if (contentType?.startsWith('image/')) {
            return contentType.substring(contentType.indexOf('/') + 1);
        }
        const parts = filename ? filename.split('.') : [];
        return parts.length > 1 ? parts[1] : '';
    }

    private getFilename(filename: string | null): string {
        const parts = filename ? filename.split('.') : [''];
        if (parts.length > 1) {
            parts.pop();
        }
        return parts.join('.');
    }
}
