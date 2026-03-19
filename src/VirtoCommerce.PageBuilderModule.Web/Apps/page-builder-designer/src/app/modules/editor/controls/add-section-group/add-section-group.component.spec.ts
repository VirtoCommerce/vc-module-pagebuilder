import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AddSectionGroupComponent } from './add-section-group.component';

xdescribe('AddSectionGroupComponent', () => {
  let component: AddSectionGroupComponent;
  let fixture: ComponentFixture<AddSectionGroupComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ AddSectionGroupComponent ]
    })
    .compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(AddSectionGroupComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
