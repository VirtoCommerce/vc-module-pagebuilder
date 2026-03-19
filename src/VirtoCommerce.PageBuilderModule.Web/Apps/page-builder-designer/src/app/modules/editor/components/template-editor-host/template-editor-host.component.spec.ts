import { ComponentFixture, TestBed } from '@angular/core/testing';

import { TemplateEditorHostComponent } from './template-editor-host.component';

xdescribe('TemplateEditorHostComponent', () => {
  let component: TemplateEditorHostComponent;
  let fixture: ComponentFixture<TemplateEditorHostComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ TemplateEditorHostComponent ]
    })
    .compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(TemplateEditorHostComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
