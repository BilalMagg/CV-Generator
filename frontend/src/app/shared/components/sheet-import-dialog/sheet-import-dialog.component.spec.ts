import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { SheetImportDialogComponent } from './sheet-import-dialog.component';
import { ToastService } from '@app/services/toast.service';

describe('SheetImportDialogComponent (skills)', () => {
  let httpMock: HttpTestingController;

  const TSV = [
    'name\tcategory\tlevel',
    'HTML\tFrontend\tAdvanced',
    'CSS\tFrontend\tAdvanced',
    'SQL\tData\tExpert',
    'Bash\tCloud\tBeginner',
  ].join('\n');

  const EXISTING = [{ name: 'Frontend' }, { name: 'Backend' }, { name: 'Data' }, { name: 'DevOps' }, { name: 'AI / ML' }];

  function makeFixture() {
    const fixture = TestBed.createComponent(SheetImportDialogComponent);
    fixture.componentRef.setInput('type', 'skills');
    fixture.componentRef.setInput('open', true);
    fixture.detectChanges();
    return { fixture, comp: fixture.componentInstance };
  }

  /** Paste TSV and fulfil the existing-categories fetch (missing = ['Cloud']). */
  async function seed(comp: SheetImportDialogComponent) {
    comp.refreshOnEdit(TSV);
    httpMock.expectOne('/api/categories/tree?scope=skills').flush({ success: true, data: EXISTING });
    await new Promise(r => setTimeout(r));
  }

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SheetImportDialogComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ToastService, useValue: { success: () => {}, error: () => {} } },
      ],
    }).compileComponents();
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('parses a skills sheet and flags categories that will be created', async () => {
    const { fixture, comp } = makeFixture();
    comp.refreshOnEdit(TSV);

    const req = httpMock.expectOne('/api/categories/tree?scope=skills');
    req.flush({ success: true, data: EXISTING });
    await new Promise(r => setTimeout(r));
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    expect((el.textContent ?? '').replace(/\s+/g, ' ')).toContain('Import 4 rows');
    const warnings = [...el.querySelectorAll('.preview.bad')].map(w => w.textContent ?? '');
    const missingLine = warnings.find(w => w.includes('(*)'));
    expect(missingLine).toBeTruthy();
    expect(missingLine).toContain('Cloud');
    expect(missingLine).not.toContain('Frontend');
  });

  it('shows no missing-category warning when every category exists', async () => {
    const { fixture, comp } = makeFixture();
    comp.refreshOnEdit(TSV);

    const req = httpMock.expectOne('/api/categories/tree?scope=skills');
    req.flush({ success: true, data: [{ name: 'Frontend' }, { name: 'Data' }, { name: 'Cloud' }] });
    await new Promise(r => setTimeout(r));
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent ?? '').not.toContain('not in your categories yet');
  });

  it('opens the mapping popup instead of importing when there are new categories', async () => {
    const { fixture, comp } = makeFixture();
    await seed(comp);
    fixture.detectChanges();

    comp.doImport();
    fixture.detectChanges();

    expect(comp.showCategoryMap()).toBe(true);
    const planList = (comp as unknown as { categoryPlanList: () => { name: string; mode: string }[] }).categoryPlanList();
    expect(planList.length).toBe(1);
    expect(planList[0].name).toBe('Cloud');
    const el: HTMLElement = fixture.nativeElement;
    expect(el.textContent ?? '').toContain('New categories');
    expect(el.textContent ?? '').toContain('Create new category');
  });

  it('applies the default "create" plan and imports with original categories', async () => {
    const { fixture, comp } = makeFixture();
    await seed(comp);

    comp.doImport();
    comp.applyPlan();

    const req = httpMock.expectOne('/api/imports/skills');
    expect(req.request.method).toBe('POST');
    const body = req.request.body as Record<string, unknown>[];
    expect(body.length).toBe(4);
    expect(body.find(r => r['name'] === 'Bash')?.['category']).toBe('Cloud');
    req.flush({ success: true, data: { imported: 4, skipped: 0, errors: [], categoriesCreated: 1, createdCategories: ['Cloud'] } });
    await new Promise(r => setTimeout(r));
    fixture.detectChanges();

    expect(comp.showCategoryMap()).toBe(false);
    const el: HTMLElement = fixture.nativeElement;
    expect(el.textContent ?? '').toContain('category created');
    expect(el.textContent ?? '').toContain('Created: Cloud');
  });

  it('rewrites rows into an existing category when mapped', async () => {
    const { fixture, comp } = makeFixture();
    await seed(comp);

    comp.doImport();
    comp.setPlanMode('Cloud', 'Frontend');
    comp.applyPlan();

    const req = httpMock.expectOne('/api/imports/skills');
    const body = req.request.body as Record<string, unknown>[];
    expect(body.find(r => r['name'] === 'Bash')?.['category']).toBe('Frontend');
    req.flush({ success: true, data: { imported: 4, skipped: 0, errors: [] } });
    await new Promise(r => setTimeout(r));
    fixture.detectChanges();
  });

  it('clears the category when told to keep uncategorized', async () => {
    const { fixture, comp } = makeFixture();
    await seed(comp);

    comp.doImport();
    comp.setPlanMode('Cloud', comp.uncatKey);
    comp.applyPlan();

    const req = httpMock.expectOne('/api/imports/skills');
    const body = req.request.body as Record<string, unknown>[];
    expect(body.find(r => r['name'] === 'Bash')?.['category']).toBe('');
    req.flush({ success: true, data: { imported: 4, skipped: 0, errors: [] } });
    await new Promise(r => setTimeout(r));
    fixture.detectChanges();
  });

  it('imports directly (no popup) when every category exists', async () => {
    const { fixture, comp } = makeFixture();
    comp.refreshOnEdit(TSV);
    httpMock.expectOne('/api/categories/tree?scope=skills').flush({ success: true, data: [...EXISTING, { name: 'Cloud' }] });
    await new Promise(r => setTimeout(r));
    fixture.detectChanges();

    comp.doImport();

    expect(comp.showCategoryMap()).toBe(false);
    const req = httpMock.expectOne('/api/imports/skills');
    req.flush({ success: true, data: { imported: 4, skipped: 0, errors: [] } });
    await new Promise(r => setTimeout(r));
    fixture.detectChanges();
  });
});