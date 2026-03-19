import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ControlsTabsComponent } from './controls-tabs.component';

xdescribe('ControlsTabsComponent', () => {
  let component: ControlsTabsComponent;
  let fixture: ComponentFixture<ControlsTabsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ ControlsTabsComponent ]
    })
    .compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(ControlsTabsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
