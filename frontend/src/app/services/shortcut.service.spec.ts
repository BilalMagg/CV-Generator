import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { createEnvironmentInjector, EnvironmentInjector } from '@angular/core';
import { Router } from '@angular/router';
import { ShortcutService } from './shortcut.service';
import { BimeService } from './bime.service';

describe('ShortcutService', () => {
  let service: ShortcutService;
  let mockRouter: { navigate: any; navigateByUrl: any; url: string };
  let mockBime: { toggle: any; isOpen: any };

  beforeEach(() => {
    mockRouter = {
      navigate: vi.fn(),
      navigateByUrl: vi.fn(),
      url: '/companies'
    };

    mockBime = {
      toggle: vi.fn(),
      isOpen: vi.fn(() => false)
    };

    const parent = { get: () => null } as unknown as EnvironmentInjector;
    const injector = createEnvironmentInjector([
      ShortcutService,
      { provide: Router, useValue: mockRouter },
      { provide: BimeService, useValue: mockBime }
    ], parent);

    service = injector.get(ShortcutService);
    service.init();
  });

  it('toggles help modal state', () => {
    expect(service.helpOpen()).toBe(false);
    service.toggleHelp();
    expect(service.helpOpen()).toBe(true);
    service.closeHelp();
    expect(service.helpOpen()).toBe(false);
  });

  it('registers and triggers contextual single-key actions', () => {
    const handler = vi.fn();
    const cleanup = service.register({ key: 'a', label: 'Add Item', handler });

    expect(service.activeContextActions().length).toBe(1);
    expect(service.activeContextActions()[0].label).toBe('Add Item');

    // Simulate keydown 'a'
    const event = new KeyboardEvent('keydown', { key: 'a', bubbles: true, cancelable: true });
    window.dispatchEvent(event);

    expect(handler).toHaveBeenCalledTimes(1);

    // After unregistering
    cleanup();
    expect(service.activeContextActions().length).toBe(0);

    const event2 = new KeyboardEvent('keydown', { key: 'a', bubbles: true, cancelable: true });
    window.dispatchEvent(event2);
    expect(handler).toHaveBeenCalledTimes(1);
  });

  it('triggers Alt+A to navigate to new application', () => {
    const event = new KeyboardEvent('keydown', { key: 'a', altKey: true, bubbles: true, cancelable: true });
    window.dispatchEvent(event);

    expect(mockRouter.navigate).toHaveBeenCalledWith(['/applications/new']);
  });

  it('triggers Alt+B to toggle BIME', () => {
    const event = new KeyboardEvent('keydown', { key: 'b', altKey: true, bubbles: true, cancelable: true });
    window.dispatchEvent(event);

    expect(mockBime.toggle).toHaveBeenCalledTimes(1);
  });

  it('triggers ? to toggle help modal', () => {
    expect(service.helpOpen()).toBe(false);
    const event = new KeyboardEvent('keydown', { key: '?', bubbles: true, cancelable: true });
    window.dispatchEvent(event);

    expect(service.helpOpen()).toBe(true);
  });

  it('executes G sequence navigation', () => {
    // Press 'g'
    const gEvent = new KeyboardEvent('keydown', { key: 'g', bubbles: true, cancelable: true });
    window.dispatchEvent(gEvent);

    // Then press 'c' (Companies)
    const cEvent = new KeyboardEvent('keydown', { key: 'c', bubbles: true, cancelable: true });
    window.dispatchEvent(cEvent);

    expect(mockRouter.navigateByUrl).toHaveBeenCalledWith('/companies');
  });

  it('ignores single-key shortcuts when typing in an input element', () => {
    const handler = vi.fn();
    service.register({ key: 'a', label: 'Add Item', handler });

    const input = document.createElement('input');
    document.body.appendChild(input);

    const event = new KeyboardEvent('keydown', { key: 'a', bubbles: true, cancelable: true });
    Object.defineProperty(event, 'target', { value: input });
    window.dispatchEvent(event);

    expect(handler).not.toHaveBeenCalled();

    document.body.removeChild(input);
  });
});
