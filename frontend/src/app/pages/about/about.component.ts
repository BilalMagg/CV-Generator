import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NavbarComponent } from '@app/shared/components/navbar/navbar.component';
import { FooterComponent } from '@app/shared/components/footer/footer.component';
import { APP_NAME } from '@app/app-name';

@Component({
  selector: 'app-about',
  standalone: true,
  imports: [CommonModule, NavbarComponent, FooterComponent],
  templateUrl: './about.component.html',
  styleUrl: './about.component.scss'
})
export class AboutComponent {
  appName = APP_NAME;
}
