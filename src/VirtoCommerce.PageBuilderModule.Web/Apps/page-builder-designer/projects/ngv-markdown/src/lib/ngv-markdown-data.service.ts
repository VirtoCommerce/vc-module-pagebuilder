import { InjectionToken } from '@angular/core';
import { Observable } from 'rxjs';

export const MARKDOWN_DATA_SERVICE = new InjectionToken<IMarkdownDataService>('ngv-markdown.data.service');

export interface IMarkdownDataService {
    saveFile(file: File): Observable<{ url: string, name: string }>;
}
