import { Component } from '@angular/core'
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router'

@Component({
  selector: 'app-my-cv',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './my-cv.component.html',
  styleUrl: './my-cv.component.css',
})
export class MyCvComponent {}
