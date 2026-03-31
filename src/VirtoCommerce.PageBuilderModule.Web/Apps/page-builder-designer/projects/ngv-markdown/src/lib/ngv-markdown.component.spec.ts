import { ComponentFixture, TestBed } from '@angular/core/testing';

import { NgvMarkdownComponent } from './ngv-markdown.component';

describe('NgvMarkdownComponent', () => {
    let component: NgvMarkdownComponent;
    let fixture: ComponentFixture<NgvMarkdownComponent>;

    beforeEach(async () => {
        await TestBed.configureTestingModule({
            imports: [NgvMarkdownComponent]
        })
            .compileComponents();
    });

    beforeEach(() => {
        fixture = TestBed.createComponent(NgvMarkdownComponent);
        component = fixture.componentInstance;
        fixture.componentRef.setInput('value', { markdown: '', html: null });
        fixture.detectChanges();
    });

    it('should create', () => {
        expect(component).toBeTruthy();
    });
});
