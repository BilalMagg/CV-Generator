import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Component, createEnvironmentInjector, EnvironmentInjector, ElementRef, ChangeDetectorRef } from '@angular/core';
import { UiSelectComponent, SelectOption } from './select.component';

describe('UiSelectComponent', () => {
  let component: UiSelectComponent;

  beforeEach(() => {
    const mockElementRef = {
      nativeElement: document.createElement('div')
    } as ElementRef;

    const mockCdr = {
      markForCheck: vi.fn(),
      detectChanges: vi.fn()
    } as unknown as ChangeDetectorRef;

    const parent = { get: () => null } as unknown as EnvironmentInjector;
    const injector = createEnvironmentInjector([
      UiSelectComponent,
      { provide: ElementRef, useValue: mockElementRef },
      { provide: ChangeDetectorRef, useValue: mockCdr }
    ], parent);

    component = injector.get(UiSelectComponent);
  });

  it('initializes closed with placeholder label', () => {
    component.placeholder = 'Select font…';
    expect(component.isOpen()).toBe(false);
    expect(component.displayLabel()).toBe('Select font…');
    expect(component.hasSelection()).toBe(false);
  });

  it('opens and closes dropdown state', () => {
    component.open();
    expect(component.isOpen()).toBe(true);

    component.close();
    expect(component.isOpen()).toBe(false);
  });

  it('selects an option, updates value, and closes', () => {
    const options: SelectOption[] = [
      { value: 'sans', label: 'Inter' },
      { value: 'mono', label: 'Mono' },
      { value: 'serif', label: 'Serif' }
    ];
    component.options = options;
    component.ngAfterContentInit();

    const changeSpy = vi.fn();
    component.registerOnChange(changeSpy);

    component.open();
    component.selectOption(options[1]);

    expect(component.currentValue()).toBe('mono');
    expect(component.displayLabel()).toBe('Mono');
    expect(component.hasSelection()).toBe(true);
    expect(component.isOpen()).toBe(false);
    expect(changeSpy).toHaveBeenCalledWith('mono');
  });

  it('updates display label when written via writeValue', () => {
    component.options = [
      { value: 'Ongoing', label: 'Ongoing' },
      { value: 'Completed', label: 'Completed' }
    ];
    component.ngAfterContentInit();

    component.writeValue('Completed');
    expect(component.displayLabel()).toBe('Completed');
    expect(component.hasSelection()).toBe(true);
  });

  it('filters options on search input', () => {
    component.options = [
      { value: 'ma', label: 'Morocco' },
      { value: 'fr', label: 'France' },
      { value: 'us', label: 'United States' }
    ];
    component.ngAfterContentInit();

    expect(component.filteredOptions().length).toBe(3);

    component.onSearchChange('mor');
    expect(component.filteredOptions().length).toBe(1);
    expect(component.filteredOptions()[0].label).toBe('Morocco');

    component.onSearchChange('');
    expect(component.filteredOptions().length).toBe(3);
  });

  it('does not open when disabled', () => {
    component.disabled = true;
    component.open();
    expect(component.isOpen()).toBe(false);
  });
});
