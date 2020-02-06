import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { PlatformService } from '@app/services';
import { BlockValuesModel } from '@shared/models';
import { map } from 'rxjs/operators';
import { PageModel } from '../models';

@Injectable({
    providedIn: 'root'
})
export class PagesService {

    constructor(private platform: PlatformService) { }

    downloadPage(): Observable<PageModel> {
        return this.platform.downloadPage().pipe(
            map(data => {
                const result = <PageModel>{
                    settings: data.find(x => x.type === 'settings') || { },
                    content: data.filter(x => x.type !== 'settings')
                };
                data.forEach((x, index) => x.id = index + 1);
                return result;
            })
        );
    }

    uploadPage(page: BlockValuesModel[]): Observable<any> {
        return this.platform.uploadPage(page);
    }
}
