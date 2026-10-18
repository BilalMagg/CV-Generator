import { TestBed } from '@angular/core/testing';
import { describe, expect, it, beforeEach, vi } from 'vitest';
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
  let getCompanies: ReturnType<typeof vi.fn>;

  beforeEach(async () => {
    getCompanies = vi.fn(() =>
      Promise.resolve({ success: true, data: { items: [{ id: '1', name: 'Stripe' }], total: 1 } }),
    );
    await TestBed.configureTestingModule({
      imports: [CompanyFieldComponent],
      providers: [
        {
          provide: CompanyService,
          useValue: { getCompanies },
        },
      ],
    }).compileComponents();
  });

  it('should create', () => {
    const fixture = TestBed.createComponent(CompanyFieldComponent);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('browses the saved companies when the empty field gains focus', async () => {
    const fixture = TestBed.createComponent(CompanyFieldComponent);
    const cmp = fixture.componentInstance;
    cmp.onFocus();
    await fixture.whenStable();
    expect(getCompanies).toHaveBeenCalledWith({ pageSize: 8, sortBy: 'name' });
    expect(cmp.matches().map(m => m.name)).toEqual(['Stripe']);
    expect(cmp.open()).toBe(true);
  });

  it('does not refetch the browse list on a second focus', async () => {
    const fixture = TestBed.createComponent(CompanyFieldComponent);
    const cmp = fixture.componentInstance;
    cmp.onFocus();
    await fixture.whenStable();
    cmp.onBlur();
    cmp.onFocus();
    await fixture.whenStable();
    expect(getCompanies).toHaveBeenCalledTimes(1);
  });

  it('sends the typed term as a search filter', async () => {
    vi.useFakeTimers();
    try {
      const fixture = TestBed.createComponent(CompanyFieldComponent);
      const cmp = fixture.componentInstance;
      cmp.onInput('cap');
      await vi.advanceTimersByTimeAsync(250);
      expect(getCompanies).toHaveBeenCalledWith({ search: 'cap', pageSize: 8, sortBy: 'name' });
      expect(cmp.model()).toBe('cap');
    } finally {
      vi.useRealTimers();
    }
  });

  it('uses the context hint for unknown names and keeps the control name bindable', () => {
    const fixture = TestBed.createComponent(CompanyFieldComponent);
    const cmp = fixture.componentInstance;
    expect(cmp.newCompanyHint()).toBe('New company — will be added on save');
    expect(cmp.nameInput()).toBe('company');
    fixture.componentRef.setInput('newCompanyHint', 'Not in your company list');
    expect(cmp.newCompanyHint()).toBe('Not in your company list');
  });

  it('picking a suggestion writes the canonical company name', async () => {
    const fixture = TestBed.createComponent(CompanyFieldComponent);
    const cmp = fixture.componentInstance;
    const picked: string[] = [];
    cmp.picked.subscribe(c => picked.push(c.name));
    cmp.pick({ id: '1', name: 'Capgemini Morocco' } as never);
    expect(cmp.model()).toBe('Capgemini Morocco');
    expect(cmp.matches()).toEqual([]);
    expect(picked).toEqual(['Capgemini Morocco']);
  });
});