import { TestBed } from '@angular/core/testing';
import { ExperienceComponent } from './experience.component';

describe('ExperienceComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ExperienceComponent],
    }).compileComponents();
  });

  it('should create', () => {
    const fixture = TestBed.createComponent(ExperienceComponent);
    const component = fixture.componentInstance;
    expect(component).toBeTruthy();
  });
});
