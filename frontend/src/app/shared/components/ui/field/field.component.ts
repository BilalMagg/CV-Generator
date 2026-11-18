import { Component, Input, Directive, HostBinding, booleanAttribute } from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * <ui-field-set>
 * Outer container grouping related form sections, matching modern shadcn FieldSet design.
 */
@Component({
  selector: 'ui-field-set',
  standalone: true,
  imports: [CommonModule],
  template: `
    <fieldset class="ui-fieldset-inner">
      <ng-content></ng-content>
    </fieldset>
  `,
  styleUrl: './field.component.scss'
})
export class UiFieldSetComponent {
  @Input() className = '';
  @HostBinding('class') get hostClass() {
    return `ui-fieldset ${this.className}`.trim();
  }
}

/**
 * <ui-field-legend>
 * Title for a form section or fieldset.
 */
@Component({
  selector: 'ui-field-legend',
  standalone: true,
  imports: [CommonModule],
  template: `<legend class="ui-field-legend-text"><ng-content></ng-content></legend>`,
  styleUrl: './field.component.scss'
})
export class UiFieldLegendComponent {
  @Input() className = '';
  @HostBinding('class') get hostClass() {
    return `ui-field-legend ${this.className}`.trim();
  }
}

/**
 * <ui-field-description>
 * Context or helper text positioned under a legend or field.
 */
@Component({
  selector: 'ui-field-description',
  standalone: true,
  imports: [CommonModule],
  template: `<p class="ui-field-description-text"><ng-content></ng-content></p>`,
  styleUrl: './field.component.scss'
})
export class UiFieldDescriptionComponent {
  @Input() className = '';
  @HostBinding('class') get hostClass() {
    return `ui-field-description ${this.className}`.trim();
  }
}

/**
 * <ui-field-group>
 * Stack of fields with consistent vertical rhythm.
 */
@Component({
  selector: 'ui-field-group',
  standalone: true,
  imports: [CommonModule],
  template: `<div class="ui-field-group-inner"><ng-content></ng-content></div>`,
  styleUrl: './field.component.scss'
})
export class UiFieldGroupComponent {
  @Input() className = '';
  @HostBinding('class') get hostClass() {
    return `ui-field-group ${this.className}`.trim();
  }
}

/**
 * <ui-field [orientation]="'vertical' | 'horizontal'">
 * Container wrapping an input, its label, and validation error.
 */
@Component({
  selector: 'ui-field',
  standalone: true,
  imports: [CommonModule],
  template: `<ng-content></ng-content>`,
  styleUrl: './field.component.scss'
})
export class UiFieldComponent {
  @Input() orientation: 'vertical' | 'horizontal' = 'vertical';
  @Input() className = '';

  @HostBinding('class') get hostClass() {
    return `ui-field orientation-${this.orientation} ${this.className}`.trim();
  }
}

/**
 * <ui-field-label [htmlFor]="id" [required]="boolean">
 * Refined label typography with optional required marker.
 */
@Component({
  selector: 'ui-field-label',
  standalone: true,
  imports: [CommonModule],
  template: `
    <label [attr.for]="htmlFor" class="ui-field-label-text">
      <ng-content></ng-content>
      @if (required) {
        <span class="ui-required-mark" aria-hidden="true">*</span>
      }
    </label>
  `,
  styleUrl: './field.component.scss'
})
export class UiFieldLabelComponent {
  @Input() htmlFor = '';
  @Input({ transform: booleanAttribute }) required = false;
  @Input() className = '';

  @HostBinding('class') get hostClass() {
    return `ui-field-label ${this.className}`.trim();
  }
}

/**
 * <ui-field-error>
 * Validation error message below a field.
 */
@Component({
  selector: 'ui-field-error',
  standalone: true,
  imports: [CommonModule],
  template: `
    <span class="ui-field-error-text" role="alert">
      <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round">
        <circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/>
      </svg>
      <ng-content></ng-content>
    </span>
  `,
  styleUrl: './field.component.scss'
})
export class UiFieldErrorComponent {
  @HostBinding('class') hostClass = 'ui-field-error';
}

/**
 * <ui-field-hint>
 * Subtle character count or helper note below an input.
 */
@Component({
  selector: 'ui-field-hint',
  standalone: true,
  imports: [CommonModule],
  template: `<span class="ui-field-hint-text"><ng-content></ng-content></span>`,
  styleUrl: './field.component.scss'
})
export class UiFieldHintComponent {
  @HostBinding('class') hostClass = 'ui-field-hint';
}

import { UI_SELECT_COMPONENTS } from '../select/select.component';

/**
 * Export all components as a convenient array for imports: [...]
 */
export const UI_FIELD_COMPONENTS = [
  UiFieldSetComponent,
  UiFieldLegendComponent,
  UiFieldDescriptionComponent,
  UiFieldGroupComponent,
  UiFieldComponent,
  UiFieldLabelComponent,
  UiFieldErrorComponent,
  UiFieldHintComponent,
  ...UI_SELECT_COMPONENTS
] as const;

export * from '../select/select.component';
