import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ThemeEditorComponent } from './theme-editor.component';

xdescribe('ThemeEditorComponent', () => {
  let component: ThemeEditorComponent;
  let fixture: ComponentFixture<ThemeEditorComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ ThemeEditorComponent ]
    })
    .compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(ThemeEditorComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
