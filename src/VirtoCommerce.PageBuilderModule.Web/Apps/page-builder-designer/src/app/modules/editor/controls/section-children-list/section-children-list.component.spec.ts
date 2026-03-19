import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SectionChildrenListComponent } from './section-children-list.component';

xdescribe('SectionChildrenListComponent', () => {
  let component: SectionChildrenListComponent;
  let fixture: ComponentFixture<SectionChildrenListComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ SectionChildrenListComponent ]
    })
    .compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(SectionChildrenListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
