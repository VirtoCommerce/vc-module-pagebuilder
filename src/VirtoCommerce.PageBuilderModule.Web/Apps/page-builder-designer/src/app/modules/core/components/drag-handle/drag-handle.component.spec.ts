import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DragHandleComponent } from './drag-handle.component';

xdescribe('DragHandleComponent', () => {
  let component: DragHandleComponent;
  let fixture: ComponentFixture<DragHandleComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ DragHandleComponent ]
    })
    .compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(DragHandleComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
