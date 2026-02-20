import { ComponentFixture, TestBed } from '@angular/core/testing';

import { FotoScrollComponent } from './foto-scroll-component';

describe('FotoScrollComponent', () => {
  let component: FotoScrollComponent;
  let fixture: ComponentFixture<FotoScrollComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FotoScrollComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(FotoScrollComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
