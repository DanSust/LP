import { ComponentFixture, TestBed } from '@angular/core/testing';

import { UserPhoto } from './user-photo';

describe('UserPhoto', () => {
  let component: UserPhoto;
  let fixture: ComponentFixture<UserPhoto>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [UserPhoto]
    })
    .compileComponents();

    fixture = TestBed.createComponent(UserPhoto);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
