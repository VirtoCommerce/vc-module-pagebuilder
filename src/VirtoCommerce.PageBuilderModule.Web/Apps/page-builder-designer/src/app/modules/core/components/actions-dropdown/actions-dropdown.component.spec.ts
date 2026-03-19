import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ActionsDropdownComponent } from './actions-dropdown.component';

xdescribe('ActionsDropdownComponent', () => {
  let component: ActionsDropdownComponent;
  let fixture: ComponentFixture<ActionsDropdownComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ ActionsDropdownComponent ]
    })
    .compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(ActionsDropdownComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
