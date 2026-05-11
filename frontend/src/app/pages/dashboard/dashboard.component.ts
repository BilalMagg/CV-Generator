import { Component, OnInit, signal } from '@angular/core';
import { AuthService, User } from '../../services/auth.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent implements OnInit {
  loading = signal(true);
  user = signal<User | null>(null);

  constructor(private authService: AuthService) {}

  ngOnInit(): void {
    this.loadUser();
  }

  async loadUser(): Promise<void> {
    this.loading.set(true);
    await this.authService.refreshUser();
    this.user.set(this.authService.currentUser());
    this.loading.set(false);
  }

  logout(): void {
    this.authService.logout();
  }
}
