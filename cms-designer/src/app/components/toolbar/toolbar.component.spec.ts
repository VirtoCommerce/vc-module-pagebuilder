import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ToolbarComponent } from './toolbar.component';
import { By } from '@angular/platform-browser';

describe('ToolbarComponent', () => {
    let fixture: ComponentFixture<ToolbarComponent>;

    beforeEach(() => {
        TestBed.configureTestingModule({
            declarations: [ToolbarComponent]
        });

        fixture = TestBed.createComponent(ToolbarComponent);
    });

    it ('should emit buttonClick event when click on item', (done) => {
        fixture.componentInstance.buttonClick.subscribe(() => {
            expect(true).toBe(true);
            done();
        });
        fixture.componentInstance.buttons = ['item1', 'item2'];
        fixture.detectChanges();
        const el = fixture.debugElement.query(By.css('a'));
        (<HTMLAnchorElement>el.nativeElement).click();
    });

    it ('should use active class for current active button only', () => {
        fixture.componentInstance.buttons = ['item1', 'item2'];
        fixture.componentInstance.activeButton = 'item2';
        fixture.detectChanges();
        const elements = fixture.debugElement.queryAll(By.css('.tabs__item--active'));
        expect(elements.length).toEqual(1);
        expect((<HTMLAnchorElement>elements[0].nativeElement).innerHTML).toContain('tabs__icon--item2');
    });

    it ('should create the only one element for each item', () => {
        fixture.componentInstance.buttons = ['item1', 'item2'];
        fixture.detectChanges();
        const elements = fixture.debugElement.queryAll(By.css('a'));
        expect(elements.length).toEqual(2);
        expect((<HTMLAnchorElement>elements[0].nativeElement).innerHTML).toContain('tabs__icon--item1');
        expect((<HTMLAnchorElement>elements[1].nativeElement).innerHTML).toContain('tabs__icon--item2');
    });
});
