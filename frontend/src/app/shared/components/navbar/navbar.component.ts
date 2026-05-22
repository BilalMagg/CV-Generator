import { Component, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { APP_NAME } from '@app/app-name';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './navbar.component.html',
  styleUrl: './navbar.component.scss',
})
export class NavbarComponent {
  appName = APP_NAME;
  navScrolled = false;
  mobileOpen = false;

  @HostListener('window:scroll')
  onScroll(): void {
    this.navScrolled = window.scrollY > 20;
  }

  toggleMobile(): void {
    this.mobileOpen = !this.mobileOpen;
  }

  scrollTo(event: Event, sectionId: string): void {
    event.preventDefault();
    document.getElementById(sectionId)?.scrollIntoView({ behavior: 'smooth' });
  }
}
