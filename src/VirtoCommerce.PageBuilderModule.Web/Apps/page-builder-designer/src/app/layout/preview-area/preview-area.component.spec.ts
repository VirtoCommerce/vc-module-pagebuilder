import { ComponentFixture, TestBed } from '@angular/core/testing';

import { PreviewAreaComponent } from './preview-area.component';

xdescribe('PreviewAreaComponent', () => {
  let component: PreviewAreaComponent;
  let fixture: ComponentFixture<PreviewAreaComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ PreviewAreaComponent ]
    })
    .compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(PreviewAreaComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
