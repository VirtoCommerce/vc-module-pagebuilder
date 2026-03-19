import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ToolbarHostComponent } from './toolbar-host.component';

xdescribe('ToolbarHostComponent', () => {
  let component: ToolbarHostComponent;
  let fixture: ComponentFixture<ToolbarHostComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ ToolbarHostComponent ]
    })
    .compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(ToolbarHostComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
