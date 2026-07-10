import { TestBed } from "@angular/core/testing";
import { provideHttpClient } from "@angular/common/http";
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';

import { CookieService } from 'ngx-cookie-service';

import { AppConfig } from './app.config';
import { EnvironmentRef } from './environment.ref';
import { BuilderHttpClient } from "./builder-http.client";
import { EvaluatorService } from "./evaluator.service";
import { AppInitializator } from "./app.initializator";

describe('app initializator', () => {
    let initializator: AppInitializator;
    let envRef: any; // EnvironmentRef;
    let cookies: CookieService;
    // let http: BuilderHttpClient;
    let evaluator: EvaluatorService;
    let httpController: HttpTestingController;

    // The platform shell leaves a token here before the app boots. Without one, init() asks
    // /connect/token for a fresh one first, and the config request these tests expect never goes out.
    const storeValidToken = () => localStorage.setItem('ls.authenticationData', JSON.stringify({
        token: 'test-token',
        expiresAt: Date.now() + 3600_000,
    }));

    beforeEach(() => {
        cookies = <any>{};
        // evaluator = <any>jasmine.createSpyObj('evaluator', ['evaluate']);
        // evaluator.evaluate.and.callFake((x: any) => x);

        storeValidToken();

        envRef = <any>{
            nativeWindow: {
                location: {
                    search: ''
                }
            }
        };

        TestBed.configureTestingModule({
            providers: [
                provideHttpClient(),
                provideHttpClientTesting(),
                { provide: EnvironmentRef, useValue: envRef },
                { provide: CookieService, useValue: cookies },
                EvaluatorService,
                AppConfig
            ]
        });

        httpController = TestBed.inject(HttpTestingController);
        initializator = TestBed.inject(AppInitializator);

    });

    afterEach(() => {
        localStorage.removeItem('ls.authenticationData');
    });

    it('simple scenario', () => {
        const response = {
            key: "value"
        };

        initializator.init().then(result => {
            expect(result).toBeTruthy();
            expect(result).toEqual({ key: "value" });
        });

        const request = httpController.expectOne('data/settings.json');
        expect(request.request.method).toBe('GET');
        request.flush(response);
    });

    describe('bearer token', () => {
        it('is obtained from the cookie session when the shell left none', async () => {
            localStorage.removeItem('ls.authenticationData');

            const done = initializator.init();

            const tokenRequest = httpController.expectOne('/connect/token');
            expect(tokenRequest.request.method).toBe('POST');
            tokenRequest.flush({ access_token: 'fresh', expires_in: 3600 });

            // the config is only fetched once the app can authenticate for it
            httpController.expectOne('data/settings.json').flush({ key: 'value' });
            httpController.expectOne(r => r.url.startsWith('/api/pagebuilder/settings')).flush({});

            await expect(done).resolves.toEqual({ key: 'value' });
            expect(JSON.parse(localStorage.getItem('ls.authenticationData')!).token).toBe('fresh');
        });

        it('is reused while it is still valid', () => {
            storeValidToken();

            initializator.init();

            httpController.expectNone('/connect/token');
            httpController.expectOne('data/settings.json').flush({});
        });
    });

    it('override config via get parameter', () => {
        const response = {
            key: "value"
        };

        envRef.nativeWindow = {
            location: {
                search: "?configUrl=data/override-settings.json"
            }
        };

        initializator.init().then(result => {
            expect(result).toBeTruthy();
            expect(result).toEqual({ key: "value" });
        });

        const request = httpController.expectOne('data/override-settings.json');
        expect(request.request.method).toBe('GET');
        request.flush(response);
    });

    describe('use ref property to load other file with settings', () => {
        it('simple case', () => {
            const firstResponse = {
                ref: "data/ref-settings.json"
            };
            const finalResponse = {
                key: "value"
            };

            initializator.init().then(result => {
                expect(result).toBeTruthy();
                expect(result).toEqual({ key: "value" });
            });

            const firstRequest = httpController.expectOne('data/settings.json');
            expect(firstRequest.request.method).toBe('GET');
            firstRequest.flush(firstResponse);

            const secondRequest = httpController.expectOne('data/ref-settings.json');
            expect(secondRequest.request.method).toBe('GET');
            secondRequest.flush(finalResponse);
        });

        it('request depends on storeId', () => {
            const firstResponse = {
                ref: "data/{{location.params.storeId}}-settings.json"
            };
            const finalResponse = {
                key: "value"
            };
            envRef.nativeWindow = {
                location: {
                    search: "?storeId=store1"
                }
            };

            initializator.init().then(result => {
                expect(result).toBeTruthy();
                expect(result).toEqual({ key: "value" });
            });

            const firstRequest = httpController.expectOne('data/settings.json');
            expect(firstRequest.request.method).toBe('GET');
            firstRequest.flush(firstResponse);

            const secondRequest = httpController.expectOne('data/store1-settings.json');
            expect(secondRequest.request.method).toBe('GET');
            secondRequest.flush(finalResponse);
        });

        describe('ref is described as request simple object', () => {
            it('simple scenario', () => {
                const firstResponse = {
                    ref: {
                        url: "data/ref-settings.json",
                        method: "post"
                    }
                };
                const finalResponse = {
                    key: "value"
                };

                initializator.init().then(result => {
                    expect(result).toBeTruthy();
                    expect(result).toEqual({ key: "value" });
                });

                const firstRequest = httpController.expectOne('data/settings.json');
                expect(firstRequest.request.method).toBe('GET');
                firstRequest.flush(firstResponse);

                const secondRequest = httpController.expectOne('data/ref-settings.json');
                expect(secondRequest.request.method).toBe('POST');
                secondRequest.flush(finalResponse);
            });

            it('with parameters in url', () => {
                const firstResponse = {
                    parameter: "parameter",
                    ref: {
                        url: "data/{{settings.parameter}}-settings.json"
                    }
                };
                const finalResponse = {
                    key: "value"
                };

                initializator.init().then(result => {
                    expect(result).toBeTruthy();
                    expect(result).toEqual({ key: "value" });
                });

                const firstRequest = httpController.expectOne('data/settings.json');
                expect(firstRequest.request.method).toBe('GET');
                firstRequest.flush(firstResponse);

                const secondRequest = httpController.expectOne('data/parameter-settings.json');
                expect(secondRequest.request.method).toBe('GET');
                secondRequest.flush(finalResponse);
            });

            it('convert response', () => {
                const firstResponse = {
                    ref: {
                        url: "data/ref-settings.json",
                        method: "post",
                        response: {
                            result: "$.value"
                        }
                    }
                };
                const finalResponse = {
                    value: {
                        key: "value"
                    }
                };

                initializator.init().then(result => {
                    expect(result).toBeTruthy();
                    expect(result).toEqual({ key: "value" });
                });

                const firstRequest = httpController.expectOne('data/settings.json');
                expect(firstRequest.request.method).toBe('GET');
                firstRequest.flush(firstResponse);

                const secondRequest = httpController.expectOne('data/ref-settings.json');
                expect(secondRequest.request.method).toBe('POST');
                secondRequest.flush(finalResponse);
            });
        });

        describe('ref is array of requests', () => {
            it('simple scenario array of urls', () => {
                const firstResponse = {
                    ref: [
                        "data/ref-settings.json",
                        "data/second-settings.json"
                    ]
                };
                const finalResponse = {
                    key: "value"
                };

                initializator.init().then(result => {
                    expect(result).toBeTruthy();
                    expect(result).toEqual({ key: "value" });
                });

                const firstRequest = httpController.expectOne('data/settings.json');
                expect(firstRequest.request.method).toBe('GET');
                firstRequest.flush(firstResponse);

                const finalRequest = httpController.expectOne('data/ref-settings.json');
                expect(finalRequest.request.method).toBe('GET');
                finalRequest.flush(finalResponse);

                httpController.expectNone('data/second-settings.json');
            });

            it('first request returns 404', () => {
                const firstResponse = {
                    ref: [
                        "data/ref-settings.json",
                        "data/second-settings.json"
                    ]
                };

                const finalResponse = {
                    key: "value"
                };

                initializator.init().then(result => {
                    expect(result).toBeTruthy();
                    expect(result).toEqual({ key: "value" });
                });

                const firstRequest = httpController.expectOne('data/settings.json');
                expect(firstRequest.request.method).toBe('GET');
                firstRequest.flush(firstResponse);

                const secondRequest = httpController.expectOne('data/ref-settings.json');
                expect(secondRequest.request.method).toBe('GET');
                secondRequest.flush(null, { status: 404, statusText: 'Not Found' });

                const finalRequest = httpController.expectOne('data/second-settings.json');
                expect(finalRequest.request.method).toBe('GET');
                finalRequest.flush(finalResponse);
            });
        });
    });

    describe('init property', () => {
        it('simple request to url', () => {
            const configResponse = {
                key: {
                    init: true,
                    url: "simple/property/request"
                }
            };

            const propertyResponse = "simple/property/response";

            initializator.init().then(result => {
                expect(result).toBeTruthy();
                expect(result).toEqual({ key: "simple/property/response" });
            });

            const request = httpController.expectOne('data/settings.json');
            expect(request.request.method).toBe('GET');
            request.flush(configResponse);

            const propertyRequest = httpController.expectOne('simple/property/request');
            expect(propertyRequest.request.method).toBe('GET');
            propertyRequest.flush(propertyResponse);
        });

        it('complex request', () => {
            const configResponse = {
                key: {
                    init: true,
                    url: "complex/property/request",
                    method: "post",
                    response: {
                        result: "$.value"
                    }
                }
            };

            const propertyResponse = {
                value: "complex/property/response"
            };

            initializator.init().then(result => {
                expect(result).toBeTruthy();
                expect(result).toEqual({ key: "complex/property/response" });
            });

            const request = httpController.expectOne('data/settings.json');
            expect(request.request.method).toBe('GET');
            request.flush(configResponse);

            const propertyRequest = httpController.expectOne('complex/property/request');
            expect(propertyRequest.request.method).toBe('POST');
            propertyRequest.flush(propertyResponse);
        });

        it('requests array with first result', () => {
            const configResponse = {
                key: {
                    init: "requests",
                    requests: [
                        {
                            url: "load property url 1",
                            method: "post",
                            response: {
                                result: "$.value"
                            }
                        },
                        "load property url 2"
                    ]
                }
            };

            const propertyResponse = {
                value: "result from first url"
            };

            initializator.init().then(result => {
                expect(result).toBeTruthy();
                expect(result).toEqual({ key: "result from first url" });
            });

            const request = httpController.expectOne('data/settings.json');
            expect(request.request.method).toBe('GET');
            request.flush(configResponse);

            const propertyRequest = httpController.expectOne('load property url 1');
            expect(propertyRequest.request.method).toBe('POST');
            propertyRequest.flush(propertyResponse);

            httpController.expectNone('load property url 2');
        });

        it('requests array with second result', () => {
            const configResponse = {
                parameter: "store1",
                key: {
                    init: "requests",
                    requests: [
                        {
                            url: "load property url 1",
                            method: "post",
                            response: {
                                result: "$.value"
                            }
                        },
                        "load property url 2 for {{settings.parameter}}"
                    ]
                }
            };

            const propertyResponse = "result from second url";

            initializator.init().then(result => {
                expect(result).toBeTruthy();
                expect(result).toEqual({ parameter: "store1", key: "result from second url" });
            });

            const configRequest = httpController.expectOne('data/settings.json');
            expect(configRequest.request.method).toBe('GET');
            configRequest.flush(configResponse);

            const firstPropertyRequest = httpController.expectOne('load property url 1');
            expect(firstPropertyRequest.request.method).toBe('POST');
            firstPropertyRequest.flush(null, { status: 404, statusText: 'Not Found' });

            const finalPropertyRequest = httpController.expectOne('load property url 2 for store1');
            expect(finalPropertyRequest.request.method).toBe('GET');
            finalPropertyRequest.flush(propertyResponse);
        });
    });
});
