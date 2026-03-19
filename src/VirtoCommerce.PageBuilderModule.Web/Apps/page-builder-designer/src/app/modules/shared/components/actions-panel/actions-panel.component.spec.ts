import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ActionsPanelComponent } from './actions-panel.component';

xdescribe('ActionsPanelComponent', () => {
  let component: ActionsPanelComponent;
  let fixture: ComponentFixture<ActionsPanelComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ ActionsPanelComponent ]
    })
    .compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(ActionsPanelComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
