import { Component, computed } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../services/auth.service';
import { APP_NAME } from '../../app-name';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent {
  appName = APP_NAME;
  isTempAuth = computed(() => environment.useTempAuth);
  loginUrl = computed(() => `${environment.gatewayUrl}/api/auth/login?returnUrl=${encodeURIComponent(window.location.origin + '/applications')}`);

  constructor(private router: Router, private authService: AuthService) {}

  loginWithTemp(): void {
    this.authService.login();
    this.router.navigate(['/applications']);
  }
}
