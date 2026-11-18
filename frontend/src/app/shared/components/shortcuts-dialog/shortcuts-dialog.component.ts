import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ShortcutService } from '@app/services/shortcut.service';

@Component({
  selector: 'app-shortcuts-dialog',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './shortcuts-dialog.component.html',
  styleUrl: './shortcuts-dialog.component.scss'
})
export class ShortcutsDialogComponent {
  readonly shortcuts = inject(ShortcutService);

  readonly globalShortcuts = [
    { keys: ['Alt', 'A'], description: 'Add new application' },
    { keys: ['Alt', 'C'], description: 'Quick add company' },
    { keys: ['Alt', 'M'], description: 'Compose email in mailbox' },
    { keys: ['Alt', 'B'], description: 'Toggle BIME AI assistant' },
    { keys: ['Ctrl', 'K'], altKeys: ['/'], description: 'Focus search on current page' },
    { keys: ['?'], description: 'Show keyboard shortcuts' },
  ];

  readonly navigationShortcuts = [
    { seq: ['G', 'A'], description: 'Go to Applications' },
    { seq: ['G', 'K'], description: 'Go to Kanban view' },
    { seq: ['G', 'L'], description: 'Go to Applications List' },
    { seq: ['G', 'C'], description: 'Go to Companies' },
    { seq: ['G', 'D'], description: 'Go to Documents' },
    { seq: ['G', 'M'], description: 'Go to Mailbox' },
    { seq: ['G', 'R'], description: 'Go to My Career' },
    { seq: ['G', 'T'], description: 'Go to Tools Hub' },
    { seq: ['G', 'S'], description: 'Go to Settings' },
  ];

  readonly formShortcuts = [
    { keys: ['Ctrl', 'Enter'], description: 'Save / Submit active form or dialog' },
    { keys: ['Esc'], description: 'Close modal, dialog or cancel focus' },
  ];

  close(): void {
    this.shortcuts.closeHelp();
  }
}
