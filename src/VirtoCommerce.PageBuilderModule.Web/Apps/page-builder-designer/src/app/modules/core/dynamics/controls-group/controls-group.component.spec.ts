import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ControlsGroupComponent } from './controls-group.component';

xdescribe('ControlsGroupComponent', () => {
  let component: ControlsGroupComponent;
  let fixture: ComponentFixture<ControlsGroupComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ ControlsGroupComponent ]
    })
    .compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(ControlsGroupComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
