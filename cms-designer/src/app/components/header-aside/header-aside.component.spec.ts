import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { HeaderAsideComponent } from './header-aside.component';

describe('HeaderAsideComponent', () => {
    let fixture: ComponentFixture<HeaderAsideComponent>;

    beforeEach(() => {
        TestBed.configureTestingModule({
            declarations: [HeaderAsideComponent]
        });

        fixture = TestBed.createComponent(HeaderAsideComponent);
    });

    it ('should emit buttonClick event when click on header', (done) => {
        fixture.componentInstance.buttonClick.subscribe(() => {
            expect(true).toBe(true);
            done();
        });
        fixture.componentInstance.active = true;
        fixture.detectChanges();
        const el = fixture.debugElement.query(By.css('a.header-link'));
        (<HTMLAnchorElement>el.nativeElement).click();
    });

    it ('should not emit buttonClick event in inactive mode', (done) => {
        fixture.componentInstance.buttonClick.subscribe(() => {
            fail();
        });
        fixture.componentInstance.active = false;
        fixture.detectChanges();
        const el = fixture.debugElement.query(By.css('a.header-link'));
        (<HTMLAnchorElement>el.nativeElement).click();
        setTimeout(() => {
            done();
        }, 10);
    });

    it ('should set correct title', () => {
        fixture.componentInstance.title = 'header title';
        fixture.detectChanges();
        const element = fixture.debugElement.query(By.css('.header-text'));
        expect(element.nativeElement.innerText).toContain('header title');
    });

    it ('should set correct icon', () => {
        fixture.componentInstance.icon = 'block-icon';
        fixture.detectChanges();
        const element = fixture.debugElement.query(By.css('img'));
        expect((<HTMLImageElement>element.nativeElement).src).toContain('block-icon');
    });

    // it ('should create the only one element for each item', () => {
    //     fixture.componentInstance.buttons = ['item1', 'item2'];
    //     fixture.detectChanges();
    //     const elements = fixture.debugElement.queryAll(By.css('a'));
    //     expect(elements.length).toEqual(2);
    //     expect((<HTMLAnchorElement>elements[0].nativeElement).innerHTML).toContain('tabs__icon--item1');
    //     expect((<HTMLAnchorElement>elements[1].nativeElement).innerHTML).toContain('tabs__icon--item2');
    // });
});
