import { Component, inject, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './header.component.html',
  styleUrl: './header.component.scss'
})
export class HeaderComponent {
  private authService = inject(AuthService);

  initials = computed(() => {
    const user = this.authService.currentUser();
    if (!user) return '?';
    return (user.firstName[0] + user.lastName[0]).toUpperCase();
  });

  userFullName = computed(() => {
    const user = this.authService.currentUser();
    return user ? `${user.firstName} ${user.lastName}` : 'Guest';
  });

  logout(): void {
    this.authService.logout();
  }
}
