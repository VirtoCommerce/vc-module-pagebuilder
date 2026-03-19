import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CollapsibleListItemComponent } from './collapsible-list-item.component';

xdescribe('CollapsibleListItemComponent', () => {
  let component: CollapsibleListItemComponent;
  let fixture: ComponentFixture<CollapsibleListItemComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ CollapsibleListItemComponent ]
    })
    .compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(CollapsibleListItemComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
