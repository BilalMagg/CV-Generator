import { TestBed } from '@angular/core/testing';
import { PersonalInfoComponent } from './personal-info.component';

describe('PersonalInfoComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PersonalInfoComponent],
    }).compileComponents();
  });

  it('should create', () => {
    const fixture = TestBed.createComponent(PersonalInfoComponent);
    const component = fixture.componentInstance;
    expect(component).toBeTruthy();
  });
});
