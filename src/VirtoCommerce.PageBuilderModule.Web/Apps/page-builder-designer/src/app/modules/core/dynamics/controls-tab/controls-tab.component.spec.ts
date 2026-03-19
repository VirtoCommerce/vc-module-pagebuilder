import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ControlsTabComponent } from './controls-tab.component';

xdescribe('ControlsTabComponent', () => {
  let component: ControlsTabComponent;
  let fixture: ComponentFixture<ControlsTabComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ ControlsTabComponent ]
    })
    .compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(ControlsTabComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
