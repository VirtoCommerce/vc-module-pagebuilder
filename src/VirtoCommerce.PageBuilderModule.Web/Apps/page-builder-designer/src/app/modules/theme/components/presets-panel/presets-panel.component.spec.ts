import { ComponentFixture, TestBed } from '@angular/core/testing';

import { PresetsPanelComponent } from './presets-panel.component';

xdescribe('PresetsPanelComponent', () => {
  let component: PresetsPanelComponent;
  let fixture: ComponentFixture<PresetsPanelComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ PresetsPanelComponent ]
    })
    .compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(PresetsPanelComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
