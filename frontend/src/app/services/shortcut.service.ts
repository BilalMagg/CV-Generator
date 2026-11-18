import { Injectable, inject, signal, computed, DestroyRef } from '@angular/core';
import { Router } from '@angular/router';
import { BimeService } from './bime.service';

export interface ContextAction {
  key: string;       // e.g. 'a', 'c', 'm'
  label: string;     // e.g. 'Add Company'
  handler: () => void;
}

export interface ShortcutGroup {
  name: string;
  shortcuts: {
    keys: string[];
    description: string;
  }[];
}

@Injectable({ providedIn: 'root' })
export class ShortcutService {
  private readonly router = inject(Router);
  private readonly bime = inject(BimeService);

  readonly helpOpen = signal(false);
  private readonly contextActions = signal<Map<string, ContextAction>>(new Map());

  private pendingSequence: string | null = null;
  private sequenceTimer: ReturnType<typeof setTimeout> | null = null;
  private initialized = false;

  readonly activeContextActions = computed<ContextAction[]>(() => {
    return Array.from(this.contextActions().values());
  });

  init(): void {
    if (this.initialized) return;
    this.initialized = true;

    window.addEventListener('keydown', (e: KeyboardEvent) => this.handleKeyDown(e), { capture: true });
  }

  toggleHelp(): void {
    this.helpOpen.update(v => !v);
  }

  closeHelp(): void {
    this.helpOpen.set(false);
  }

  /**
   * Register a contextual shortcut (e.g. 'a' for Add in a list page).
   * Automatically unregisters when the component's DestroyRef fires if provided.
   */
  register(action: ContextAction, destroyRef?: DestroyRef): () => void {
    const key = action.key.toLowerCase();
    this.contextActions.update(map => {
      const copy = new Map(map);
      copy.set(key, action);
      return copy;
    });

    const cleanup = () => {
      this.contextActions.update(map => {
        const copy = new Map(map);
        if (copy.get(key) === action) {
          copy.delete(key);
        }
        return copy;
      });
    };

    if (destroyRef) {
      destroyRef.onDestroy(cleanup);
    }

    return cleanup;
  }

  private handleKeyDown(e: KeyboardEvent): void {
    const isInput = this.isInputActive(e);

    // 1. Escape: Close help modal if open, or blur active input
    if (e.key === 'Escape') {
      if (this.helpOpen()) {
        e.preventDefault();
        e.stopPropagation();
        this.closeHelp();
        return;
      }
      if (isInput) {
        (e.target as HTMLElement)?.blur();
      }
      return;
    }

    // 2. Ctrl + Enter (or Cmd + Enter): Submit active form or modal
    if ((e.ctrlKey || e.metaKey) && e.key === 'Enter') {
      const submitBtn = document.querySelector<HTMLButtonElement>(
        '.dialog-footer button.btn-primary, .modal-footer button.btn-primary, .modal-actions button.btn-primary, form button[type="submit"]'
      );
      if (submitBtn && !submitBtn.disabled) {
        e.preventDefault();
        submitBtn.click();
        return;
      }
    }

    // 3. Ctrl + K / Cmd + K: Focus search from anywhere
    if ((e.ctrlKey || e.metaKey) && (e.key === 'k' || e.key === 'K')) {
      e.preventDefault();
      this.focusSearch();
      return;
    }

    // 4. Global Alt + <Key> shortcuts (work everywhere, even if in input)
    if (e.altKey && !e.ctrlKey && !e.metaKey) {
      const key = e.key.toLowerCase();
      if (key === 'a') {
        e.preventDefault();
        void this.router.navigate(['/applications/new']);
        return;
      }
      if (key === 'c') {
        e.preventDefault();
        // If already on companies page and contextual 'c' or 'a' is registered, trigger it; else navigate
        const currentUrl = this.router.url.split('?')[0];
        if (currentUrl === '/companies') {
          const action = this.contextActions().get('c') || this.contextActions().get('a');
          if (action) {
            action.handler();
            return;
          }
        }
        void this.router.navigate(['/companies'], { queryParams: { create: '1' } });
        return;
      }
      if (key === 'm') {
        e.preventDefault();
        void this.router.navigate(['/mailbox'], { queryParams: { tab: 'compose' } });
        return;
      }
      if (key === 'b') {
        e.preventDefault();
        this.bime.toggle();
        return;
      }
    }

    // Stop here if the user is currently typing in an input/textarea/select
    if (isInput) {
      return;
    }

    // 5. Help shortcut: '?' (Shift + /)
    if (e.key === '?') {
      e.preventDefault();
      this.toggleHelp();
      return;
    }

    // 6. Forward slash '/' to focus search when not in input
    if (e.key === '/' && !e.ctrlKey && !e.metaKey && !e.altKey) {
      e.preventDefault();
      this.focusSearch();
      return;
    }

    // 7. Multi-key sequence: 'G' then navigation key
    if (this.pendingSequence === 'g') {
      this.clearSequence();
      const target = e.key.toLowerCase();
      const navMap: Record<string, string> = {
        'a': '/applications/dashboard',
        'k': '/applications/kanban',
        'l': '/applications/list',
        'c': '/companies',
        'd': '/documents/cvs',
        'm': '/mailbox',
        'r': '/my-career/projects',
        't': '/tools',
        's': '/settings/llm'
      };

      if (navMap[target]) {
        e.preventDefault();
        void this.router.navigateByUrl(navMap[target]);
        return;
      }
    }

    if (e.key.toLowerCase() === 'g' && !e.ctrlKey && !e.metaKey && !e.altKey) {
      this.pendingSequence = 'g';
      if (this.sequenceTimer) clearTimeout(this.sequenceTimer);
      this.sequenceTimer = setTimeout(() => {
        this.pendingSequence = null;
      }, 1200);
      return;
    }

    // 8. Contextual registered single-key actions (e.g. 'a', 'c', 'm')
    const actionKey = e.key.toLowerCase();
    const registered = this.contextActions().get(actionKey);
    if (registered && !e.ctrlKey && !e.metaKey && !e.altKey) {
      e.preventDefault();
      registered.handler();
      return;
    }

    // 9. Fallback contextual action for 'a' or 'c' based on active route
    if ((actionKey === 'a' || actionKey === 'c') && !e.ctrlKey && !e.metaKey && !e.altKey) {
      const url = this.router.url.split('?')[0];
      if (url.startsWith('/applications')) {
        e.preventDefault();
        void this.router.navigate(['/applications/new']);
        return;
      }
      if (url === '/companies') {
        e.preventDefault();
        void this.router.navigate(['/companies'], { queryParams: { create: '1' } });
        return;
      }
      if (url.startsWith('/my-career/')) {
        const segments = url.split('/').filter(Boolean);
        if (segments.length >= 2 && segments[1] !== 'skills') {
          e.preventDefault();
          void this.router.navigate(['/my-career', segments[1], 'add']);
          return;
        }
      }
    }
  }

  private isInputActive(e: KeyboardEvent): boolean {
    const target = e.target as HTMLElement | null;
    if (!target || !target.tagName) return false;
    const tag = target.tagName.toLowerCase();
    if (tag === 'input' || tag === 'textarea' || tag === 'select') return true;
    if (target.isContentEditable) return true;
    return false;
  }

  private clearSequence(): void {
    this.pendingSequence = null;
    if (this.sequenceTimer) {
      clearTimeout(this.sequenceTimer);
      this.sequenceTimer = null;
    }
  }

  focusSearch(): void {
    const searchInput = document.querySelector<HTMLInputElement>(
      'input[type="search"], input.search-input, .filter-search input, .header-search input, input[placeholder*="Search" i], input[placeholder*="Filter" i]'
    );
    if (searchInput) {
      searchInput.focus();
      searchInput.select();
    }
  }
}
