import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AddSectionItemComponent } from './add-section-item.component';

xdescribe('AddSectionItemComponent', () => {
  let component: AddSectionItemComponent;
  let fixture: ComponentFixture<AddSectionItemComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ AddSectionItemComponent ]
    })
    .compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(AddSectionItemComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
