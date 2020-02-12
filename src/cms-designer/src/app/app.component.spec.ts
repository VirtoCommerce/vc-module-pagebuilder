import { TestBed, ComponentFixture, inject } from '@angular/core/testing';
import { AppComponent } from './app.component';
import { Component, Input } from '@angular/core';
import { SafeUrl, DomSanitizer, By } from '@angular/platform-browser';
import { ApiUrlsService } from './services/api-url.service';
import { Store } from '@ngrx/store';
import { PageModel } from './modules/editor/models';
import { BlocksSchema, BlockSchema } from './modules/shared/models';

xdescribe('AppComponent', () => {

    let fixture: ComponentFixture<AppComponent>;
    let mockStore;
    let mockUrls: any;

    beforeEach(() => {
        mockUrls = jasmine.createSpyObj(['getStoreUrl']);
        mockStore = jasmine.createSpyObj(['subscribe']);
        mockStore.pipe = jasmine.createSpy().and.returnValue(mockStore);
        TestBed.configureTestingModule({
            declarations: [ AppComponent ],
            providers: [
                { provide: ApiUrlsService, useValue: mockUrls },
                { provide: Store, useValue: mockStore }
            ]
        }).overrideTemplate(AppComponent, '');
        fixture = TestBed.createComponent(AppComponent);
    });

    it('should create the app', () => {
        const component = fixture.componentInstance;
        expect(component).not.toBeNull();
    });

    xit('should get safe store url and pass it to preview', inject([DomSanitizer], (sanitizer: DomSanitizer) => {
        const url = 'http://localhost/';
        const safeUrl = sanitizer.bypassSecurityTrustUrl(url);

        mockUrls.getStoreUrl.and.returnValue(safeUrl);

        fixture.detectChanges();

        // const preview = fixture.debugElement.query(By.directive(PreviewStubComponent));

        // expect(preview.componentInstance.storeUrl).toEqual(safeUrl);

    }));

});
