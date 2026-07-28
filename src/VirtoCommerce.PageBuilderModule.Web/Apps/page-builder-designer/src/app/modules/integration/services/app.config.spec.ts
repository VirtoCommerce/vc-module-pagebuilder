import { TestBed } from '@angular/core/testing';

import { CookieService } from 'ngx-cookie-service';

import { AppConfig } from './app.config';
import { EnvironmentRef } from './environment.ref';
import { EvaluatorService } from './evaluator.service';

describe('AppConfig', () => {
  let appConfig: AppConfig;
  let location: Record<string, string>;

  beforeEach(() => {
    location = {
      search: '?storeId=store-1',
      hash: '#/pages?type=linked-components&linkedComponentId=component-1',
      href: 'https://localhost/designer#/pages?type=linked-components&linkedComponentId=component-1',
      host: 'localhost',
      protocol: 'https:',
      pathname: '/designer',
      origin: 'https://localhost',
    };

    TestBed.configureTestingModule({
      providers: [
        AppConfig,
        EvaluatorService,
        { provide: EnvironmentRef, useValue: { nativeWindow: { location } } },
        { provide: CookieService, useValue: {} },
      ],
    });

    appConfig = TestBed.inject(AppConfig);
    appConfig.initConfigWith({
      saveGroupedPage: {
        url: '/api/page-builder-pages/grouped/{{location.params.groupId}}/content',
      },
    });
  });

  it('refreshes hash parameters after client-side navigation', () => {
    expect(appConfig.getValue('saveGroupedPage').url).toBe('/api/page-builder-pages/grouped//content');

    location['hash'] = '#/pages?type=pages&groupId=page-1';
    location['href'] = 'https://localhost/designer#/pages?type=pages&groupId=page-1';

    expect(appConfig.getValue('saveGroupedPage').url).toBe('/api/page-builder-pages/grouped/page-1/content');
    expect(appConfig.getContext().location).toMatchObject({
      hash: '#/pages?type=pages&groupId=page-1',
      hashPath: '#/pages',
      params: {
        storeId: 'store-1',
        type: 'pages',
        groupId: 'page-1',
      },
    });
  });
});
