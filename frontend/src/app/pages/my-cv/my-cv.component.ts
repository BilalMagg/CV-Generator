import { Component, inject, computed } from '@angular/core'
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router'
import { AuthService } from '../../services/auth.service'

@Component({
  selector: 'app-my-cv',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './my-cv.component.html',
  styleUrl: './my-cv.component.css',
})
export class MyCvComponent {
  private authService = inject(AuthService)

  initials = computed(() => {
    const u = this.authService.currentUser()
    if (!u) return '?'
    return (u.firstName[0] + u.lastName[0]).toUpperCase()
  })

  fullName = computed(() => {
    const u = this.authService.currentUser()
    return u ? `${u.firstName} ${u.lastName}` : 'User'
  })
}
