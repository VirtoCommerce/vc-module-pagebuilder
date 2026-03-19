import { ComponentFixture, TestBed } from '@angular/core/testing';

import { IconWithPreviewComponent } from './icon-with-preview.component';

xdescribe('IconWithPreviewComponent', () => {
  let component: IconWithPreviewComponent;
  let fixture: ComponentFixture<IconWithPreviewComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ IconWithPreviewComponent ]
    })
    .compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(IconWithPreviewComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
