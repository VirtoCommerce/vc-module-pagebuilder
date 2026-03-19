import { from, map, tap } from 'rxjs';
import { AssetsRequest, UploadAssetDescriptor } from '@models/index';
import { AppConfig, EvaluatorService } from '@integration/services';
import { inject, Injectable } from '@angular/core';
import { FilesDescriptor } from '@models/controls';
import { AssetFile } from '../models';
import { DataService } from './data.service';
import { Observable, of } from 'rxjs';

import { appHelpers } from '@integration/helpers';

@Injectable({
    providedIn: 'root'
})
export class AssetsService {

    private readonly data = inject(DataService);
    private readonly appConfig = inject(AppConfig);
    private readonly evaluator = inject(EvaluatorService);

    uploadAsset(file: AssetFile, descriptor: UploadAssetDescriptor,
        context: any, progress: (value: number) => void, overridenRequestProps: Partial<AssetsRequest> | null = null): Observable<any> {
        // todo: progress not works
        let request = this.getRequest(descriptor, { ...context, file });
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
        request = this.getRequest(descriptor, { ...context, file } ); // do it again, thus asset name can impact to request
        return this.data.doRequest(<AssetsRequest>request, context, file, { nullWhenError: false }).pipe(
            map(response => {
                const req = <AssetsRequest>request;
                if (req.resultTemplate) {
                    file.url = this.evaluator.evaluate(req.resultTemplate, { ...context, response });
                } else {
                    file.url = response;
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
        return file.url;
    }

    adjustUrl(absoluteOrRelativeUrl: string | null, context: any): string | null {
        if (!absoluteOrRelativeUrl) {
            return null;
        }
        const url = ['http://', 'https://', '//', 'data:'].find(x => absoluteOrRelativeUrl.startsWith(x))
            ? absoluteOrRelativeUrl
            : this.appConfig.getValue('assetsUrlTemplate', { ...context, assetName: absoluteOrRelativeUrl });
        return url || absoluteOrRelativeUrl;
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

    private randomizeAssetName(name: string | null, contentType: string | null = null): string {
        const filename = this.getFilename(name);
        const extension = this.getExtension(name, contentType);
        const suffix = appHelpers.generateUniqueString(10);
        const uniqueName = `${filename}_${suffix}.${extension}`;
        const safeName = encodeURIComponent(uniqueName);
        return safeName;
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
