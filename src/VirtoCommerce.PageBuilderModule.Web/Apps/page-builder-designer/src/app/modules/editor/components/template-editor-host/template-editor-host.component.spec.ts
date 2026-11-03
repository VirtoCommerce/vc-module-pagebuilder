import { TestBed } from '@angular/core/testing';

import { PAGE_ANCHORS_PROVIDER, PageAnchor, getActivePageAnchors } from '@core/services';

import { TemplateEditorHostComponent } from './template-editor-host.component';

const pageAnchors: PageAnchor[] = [{ value: 'specifications', label: 'Specifications' }];

describe('TemplateEditorHostComponent', () => {

    beforeEach(() => {
        TestBed.configureTestingModule({
            providers: [{ provide: PAGE_ANCHORS_PROVIDER, useValue: { getAnchors: () => pageAnchors } }]
        });

        // The host only exists to lay the editor out; the anchors registration is what is under test.
        TestBed.overrideComponent(TemplateEditorHostComponent, { set: { template: '', imports: [] } });
    });

    it('publishes the page anchors while the editor route is on screen', () => {
        TestBed.createComponent(TemplateEditorHostComponent);

        expect(getActivePageAnchors()).toEqual(pageAnchors);
    });

    it('takes them down again when the route is left', () => {
        const fixture = TestBed.createComponent(TemplateEditorHostComponent);

        // Leaving /pages destroys the routed component — unlike the route injector, which the router
        // keeps alive for the whole application, so a service ngOnDestroy would never run.
        fixture.destroy();

        expect(getActivePageAnchors()).toEqual([]);
    });
});
