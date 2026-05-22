import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { APP_NAME } from '../../../../../app-name';

@Component({
  selector: 'app-cta-banner-section',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './cta-banner.component.html',
  styleUrl: './cta-banner.component.scss',
})
export class CtaBannerSectionComponent {
  appName = APP_NAME;
}
