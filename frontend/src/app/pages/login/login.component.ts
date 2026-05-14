import { Component, computed } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent {
  isTempAuth = computed(() => environment.useTempAuth);
  loginUrl = computed(() => `${environment.gatewayUrl}/api/auth/login?returnUrl=${encodeURIComponent(window.location.origin + '/applications')}`);

  constructor(private router: Router, private authService: AuthService) {}

  loginWithTemp(): void {
    this.authService.login();
    this.router.navigate(['/applications']);
  }
}
