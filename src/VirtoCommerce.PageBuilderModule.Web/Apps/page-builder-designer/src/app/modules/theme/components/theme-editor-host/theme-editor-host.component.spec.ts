import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ThemeEditorHostComponent } from './theme-editor-host.component';

xdescribe('ThemeEditorHostComponent', () => {
  let component: ThemeEditorHostComponent;
  let fixture: ComponentFixture<ThemeEditorHostComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ ThemeEditorHostComponent ]
    })
    .compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(ThemeEditorHostComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
