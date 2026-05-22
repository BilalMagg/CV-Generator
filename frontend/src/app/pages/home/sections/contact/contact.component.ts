import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { APP_NAME } from '@app/app-name';

@Component({
  selector: 'app-contact-section',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './contact.component.html',
  styleUrl: './contact.component.scss',
})
export class ContactSectionComponent {
  appName = APP_NAME;
}
