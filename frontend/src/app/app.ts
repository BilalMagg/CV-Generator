import { Component, inject } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { Router, RouterOutlet } from '@angular/router';
import { HeaderComponent } from './layout/header/header.component';
import { SidebarComponent } from './layout/sidebar/sidebar.component';
import { RevealOverlayComponent } from './pages/reveal-overlay/reveal-overlay.component';
import { ToastComponent } from './shared/components/toast/toast.component';
import { ConfirmDialogComponent } from './shared/components/confirm-dialog/confirm-dialog.component';
import { BimeChatComponent } from './shared/components/bime-chat/bime-chat';
import { LatexBannerComponent } from './shared/components/latex-banner/latex-banner.component';
import { ShortcutsDialogComponent } from './shared/components/shortcuts-dialog/shortcuts-dialog.component';
import { ShortcutService } from './services/shortcut.service';
import { APP_NAME } from './app-name';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, HeaderComponent, SidebarComponent, RevealOverlayComponent, ToastComponent, ConfirmDialogComponent, BimeChatComponent, LatexBannerComponent, ShortcutsDialogComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  private router = inject(Router);
  private title = inject(Title);
  private shortcutService = inject(ShortcutService);

  isAuthPage = false;
  showHeader = false;

  ngOnInit(): void {
    this.shortcutService.init();
    this.title.setTitle(APP_NAME);
    this.router.events.subscribe(() => {
      const url = this.router.url.split('#')[0].split('?')[0];
      this.isAuthPage = url === '/' || url === '/login' || url === '/register' || url.startsWith('/about') || url.startsWith('/contact');
      this.showHeader = !this.isAuthPage;
    });
    const initialUrl = this.router.url.split('#')[0].split('?')[0];
    this.isAuthPage = initialUrl === '/' || initialUrl === '/login' || initialUrl === '/register' || initialUrl.startsWith('/about') || initialUrl.startsWith('/contact');
    this.showHeader = !this.isAuthPage;
  }
}
