import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DefaultToolbarComponent } from './default-toolbar.component';

xdescribe('DefaultToolbarComponent', () => {
  let component: DefaultToolbarComponent;
  let fixture: ComponentFixture<DefaultToolbarComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ DefaultToolbarComponent ]
    })
    .compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(DefaultToolbarComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
