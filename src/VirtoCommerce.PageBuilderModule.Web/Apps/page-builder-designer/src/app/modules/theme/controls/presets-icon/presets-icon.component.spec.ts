import { ComponentFixture, TestBed } from '@angular/core/testing';

import { PresetsIconComponent } from './presets-icon.component';

xdescribe('PresetsIconComponent', () => {
  let component: PresetsIconComponent;
  let fixture: ComponentFixture<PresetsIconComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ PresetsIconComponent ]
    })
    .compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(PresetsIconComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
