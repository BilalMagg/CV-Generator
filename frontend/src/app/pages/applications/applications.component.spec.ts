import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { ApplicationsComponent } from './applications.component';

describe('ApplicationsComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ApplicationsComponent],
      providers: [provideRouter([]), provideHttpClient()],
    }).compileComponents();
  });

  it('should create', () => {
    const fixture = TestBed.createComponent(ApplicationsComponent);
    const component = fixture.componentInstance;
    expect(component).toBeTruthy();
  });
});
