import { ComponentFixture, TestBed } from '@angular/core/testing';

import { MultipageSelectComponent } from './multipage-select.component';

xdescribe('MultipageSelectComponent', () => {
    let component: MultipageSelectComponent;
    let fixture: ComponentFixture<MultipageSelectComponent>;

    beforeEach(async () => {
        await TestBed.configureTestingModule({
            declarations: [MultipageSelectComponent]
        })
            .compileComponents();
    });

    beforeEach(() => {
        fixture = TestBed.createComponent(MultipageSelectComponent);
        component = fixture.componentInstance;
        fixture.detectChanges();
    });

    it('should create', () => {
        expect(component).toBeTruthy();
    });
});
