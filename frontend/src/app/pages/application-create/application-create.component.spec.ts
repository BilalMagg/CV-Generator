import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { ApplicationCreateComponent } from '../applications/create/application-create.component';

describe('ApplicationCreateComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ApplicationCreateComponent],
      providers: [provideRouter([]), provideHttpClient()],
    }).compileComponents();
  });

  it('should create', () => {
    const fixture = TestBed.createComponent(ApplicationCreateComponent);
    const component = fixture.componentInstance;
    expect(component).toBeTruthy();
  });
});
