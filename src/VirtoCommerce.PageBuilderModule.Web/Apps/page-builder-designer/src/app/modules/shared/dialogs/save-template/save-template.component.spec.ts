import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SaveTemplateComponent } from './save-template.component';

xdescribe('SaveTemplateComponent', () => {
    let component: SaveTemplateComponent;
    let fixture: ComponentFixture<SaveTemplateComponent>;

    beforeEach(async () => {
        await TestBed.configureTestingModule({
            declarations: [SaveTemplateComponent]
        })
            .compileComponents();
    });

    beforeEach(() => {
        fixture = TestBed.createComponent(SaveTemplateComponent);
        component = fixture.componentInstance;
        fixture.detectChanges();
    });

    it('should create', () => {
        expect(component).toBeTruthy();
    });
});
