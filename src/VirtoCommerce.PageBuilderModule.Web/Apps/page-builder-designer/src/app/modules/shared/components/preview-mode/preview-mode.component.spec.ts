import { ComponentFixture, TestBed } from '@angular/core/testing';

import { PreviewModeComponent } from './preview-mode.component';

xdescribe('PreviewModeComponent', () => {
  let component: PreviewModeComponent;
  let fixture: ComponentFixture<PreviewModeComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ PreviewModeComponent ]
    })
    .compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(PreviewModeComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
