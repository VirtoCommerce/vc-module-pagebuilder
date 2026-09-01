import { TestBed } from "@angular/core/testing";
import { provideHttpClient } from "@angular/common/http";
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';

import { CookieService } from 'ngx-cookie-service';

import { AppConfig } from './app.config';
import { EnvironmentRef } from './environment.ref';
import { BuilderHttpClient } from "./builder-http.client";
import { EvaluatorService } from "./evaluator.service";
import { AppInitializator } from "./app.initializator";
import { SessionService } from "./session.service";

describe('app initializator', () => {
    let initializator: AppInitializator;
    let envRef: any; // EnvironmentRef;
    let cookies: CookieService;
    // let http: BuilderHttpClient;
    let evaluator: EvaluatorService;
    let httpController: HttpTestingController;
    let session: SessionService;

    beforeEach(() => {
        // a valid token keeps init() from asking the platform for one, which these tests do not cover
        localStorage.setItem('ls.authenticationData', JSON.stringify({ token: 'test-token', expiresAt: Date.now() + 60000 }));

        cookies = <any>{};
        // evaluator = <any>jasmine.createSpyObj('evaluator', ['evaluate']);
        // evaluator.evaluate.and.callFake((x: any) => x);

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
        session = TestBed.inject(SessionService);

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

    it('reports an expired session when a token cannot be obtained', () => {
        localStorage.removeItem('ls.authenticationData');

        initializator.init();

        const tokenRequest = httpController.expectOne('/connect/token');
        expect(tokenRequest.request.method).toBe('POST');
        tokenRequest.flush('', { status: 401, statusText: 'Unauthorized' });

        expect(session.expired()).toBe(true);
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
