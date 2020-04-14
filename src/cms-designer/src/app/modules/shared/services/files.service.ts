import { ApiUrlsService } from 'src/app/services/api-url.service';
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map } from 'rxjs/operators';
import { AppSettings } from '@app/services';

@Injectable({
    providedIn: 'root'
})
export class FilesService {

    constructor(private http: HttpClient, private urls: ApiUrlsService) { }

    uploadFile(file: File, name: string): Observable<string> {
        const safeName = this.urls.generateUniqueSafeFileName(name);
        const url = this.urls.generateUploadAssetUrl(safeName);
        const form = new FormData();
        form.append('uploadedFile', file, safeName);
        return this.http.post<FileDescriptor[]>(url, form).pipe(
            map(x => AppSettings.useGlobalAssets ? x[0].url : this.urls.getAssetsRelativeUrl(safeName))
        );
    }

}
interface FileDescriptor {
    contentType: string;
    fileName: string;
    key: string;
    mimeType: string;
    name: string;
    relativeUrl: string;
    size: number;
    url: string;
}
