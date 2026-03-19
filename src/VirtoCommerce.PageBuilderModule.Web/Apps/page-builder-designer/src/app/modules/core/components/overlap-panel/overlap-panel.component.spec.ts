import { ComponentFixture, TestBed } from '@angular/core/testing';

import { OverlapPanelComponent } from './overlap-panel.component';

xdescribe('OverlapPanelComponent', () => {
  let component: OverlapPanelComponent;
  let fixture: ComponentFixture<OverlapPanelComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ OverlapPanelComponent ]
    })
    .compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(OverlapPanelComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
