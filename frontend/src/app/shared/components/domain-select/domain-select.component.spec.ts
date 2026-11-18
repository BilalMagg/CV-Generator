import { ComponentFixture, TestBed } from '@angular/core/testing';
import { DomainSelectComponent } from './domain-select.component';
import { ApplicationService } from '@app/services/application.service';
import { ApplicationDomainDto, ApiResponse } from '@app/models/application.model';

describe('DomainSelectComponent', () => {
  let component: DomainSelectComponent;
  let fixture: ComponentFixture<DomainSelectComponent>;
  let mockAppService: any;

  const initialDomains: ApplicationDomainDto[] = [
    { id: '1', name: 'Web dev', applicationCount: 5 },
    { id: '2', name: 'Mobile dev', applicationCount: 3 },
    { id: '3', name: 'Data Science', applicationCount: 8 },
    { id: '4', name: 'AI & LLMs', applicationCount: 4 },
    { id: '5', name: 'Systems', applicationCount: 2 }
  ];

  beforeEach(async () => {
    mockAppService = {
      getDomains: vi.fn().mockResolvedValue({
        success: true,
        data: [...initialDomains]
      } as ApiResponse<ApplicationDomainDto[]>),
      createDomain: vi.fn().mockImplementation((name: string) =>
        Promise.resolve({
          success: true,
          data: { id: '6', name, applicationCount: 0 }
        } as ApiResponse<ApplicationDomainDto>)
      ),
      updateDomain: vi.fn().mockImplementation((id: string, name: string) =>
        Promise.resolve({
          success: true,
          data: { id, name, applicationCount: 2 }
        } as ApiResponse<ApplicationDomainDto>)
      ),
      deleteDomain: vi.fn().mockResolvedValue({
        success: true,
        data: undefined
      } as ApiResponse<void>)
    };

    await TestBed.configureTestingModule({
      imports: [DomainSelectComponent],
      providers: [
        { provide: ApplicationService, useValue: mockAppService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(DomainSelectComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
  });

  it('should create and load domains on init', () => {
    expect(component).toBeTruthy();
    expect(mockAppService.getDomains).toHaveBeenCalled();
    expect(component.domains().length).toBe(5);
  });

  it('should generate options list with action options, divider, undefined, and domains', () => {
    const opts = component.options();
    expect(opts[0].label).toBe('Add category…');
    expect(opts[1].label).toBe('Edit categories…');
    expect(opts[2].isDivider).toBe(true);
    expect(opts[3].label).toBe('Undefined (None)');
    expect(opts[3].value).toBe('');
    expect(opts[4].label).toBe('Web dev');
  });

  it('should detect exact duplicate names case-insensitively', () => {
    component.newCategoryName.set('web dev');
    expect(component.exactMatch()).not.toBeNull();
    expect(component.exactMatch()?.name).toBe('Web dev');

    component.newCategoryName.set('Data & AI');
    expect(component.exactMatch()).toBeNull();
  });

  it('should detect similar category names using token overlap and fuzzy matching', () => {
    // "Data & AI" shares "data" with "Data Science", and "ai" with "AI & LLMs"
    component.newCategoryName.set('Data & AI');
    const similar = component.similarCategories();
    expect(similar).toContain('Data Science');
    expect(similar).toContain('AI & LLMs');

    // "System" is a close typo / singular of "Systems"
    component.newCategoryName.set('System');
    expect(component.similarCategories()).toContain('Systems');
  });

  it('should create new category and auto-select it', async () => {
    component.openAddModal();
    expect(component.showAddModal()).toBe(true);

    component.newCategoryName.set('Cybersecurity');
    const spy = vi.spyOn(component.valueChange, 'emit');

    await component.submitNewCategory();

    expect(mockAppService.createDomain).toHaveBeenCalledWith('Cybersecurity');
    expect(component.currentValue()).toBe('Cybersecurity');
    expect(spy).toHaveBeenCalledWith('Cybersecurity');
    expect(component.showAddModal()).toBe(false);
  });

  it('should open manage modal and update category name', async () => {
    component.openManageModal();
    expect(component.showManageModal()).toBe(true);

    const target = component.domains()[0]; // Web dev
    component.startEdit(target);
    expect(component.editingId()).toBe(target.id);

    component.editingName.set('Fullstack Web');
    await component.saveEdit(target);

    expect(mockAppService.updateDomain).toHaveBeenCalledWith(target.id, 'Fullstack Web');
    expect(component.editingId()).toBeNull();
  });

  it('should delete a category and reset selection if it was selected', async () => {
    component.currentValue.set('Web dev');
    const target = component.domains()[0];

    const spy = vi.spyOn(component.valueChange, 'emit');
    await component.confirmDelete(target);

    expect(mockAppService.deleteDomain).toHaveBeenCalledWith(target.id);
    expect(component.currentValue()).toBe('');
    expect(spy).toHaveBeenCalledWith(undefined);
  });
});
