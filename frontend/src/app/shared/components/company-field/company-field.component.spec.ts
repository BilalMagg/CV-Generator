import { TestBed } from '@angular/core/testing';
import { describe, expect, it, beforeEach } from 'vitest';
import { companyStatus } from './company-field.component';
import { CompanyFieldComponent } from './company-field.component';
import { CompanyService } from '@app/services/company.service';

const companies = [
  { name: 'Stripe' },
  { name: 'Capgemini' },
  { name: 'capgemini ' },
];

describe('companyStatus', () => {
  it('returns neutral for an empty value', () => {
    expect(companyStatus('', companies, null)).toEqual({ exact: null, isNew: false });
    expect(companyStatus('   ', companies, null)).toEqual({ exact: null, isNew: false });
  });

  it('marks an exact name match as existing', () => {
    const st = companyStatus('Stripe', companies, null);
    expect(st.exact?.name).toBe('Stripe');
    expect(st.isNew).toBe(false);
  });

  it('matches case- and whitespace-insensitively', () => {
    const st = companyStatus('capgemini', companies, null);
    expect(st.exact?.name).toBe('Capgemini');
    expect(st.isNew).toBe(false);
  });

  it('marks a non-matching value as new', () => {
    const st = companyStatus('Acme Corp', companies, null);
    expect(st.exact).toBeNull();
    expect(st.isNew).toBe(true);
  });

  it('is not new when the typed value is the last picked company', () => {
    const st = companyStatus('Stripe', [], 'Stripe');
    expect(st.exact).toBeNull();
    expect(st.isNew).toBe(false);
  });

  it('is new when the picked company no longer matches the value', () => {
    const st = companyStatus('Stripe X', [], 'Stripe');
    expect(st.isNew).toBe(true);
  });
});

describe('CompanyFieldComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CompanyFieldComponent],
      providers: [
        {
          provide: CompanyService,
          useValue: {
            getCompanies: () => Promise.resolve({ success: true, data: { items: [], total: 0 } }),
          },
        },
      ],
    }).compileComponents();
  });

  it('should create', () => {
    const fixture = TestBed.createComponent(CompanyFieldComponent);
    expect(fixture.componentInstance).toBeTruthy();
  });
});