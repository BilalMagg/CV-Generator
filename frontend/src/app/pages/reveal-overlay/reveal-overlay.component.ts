import {
  Component,
  OnInit,
  AfterViewInit,
  ViewChild,
  ElementRef,
} from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-reveal-overlay',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './reveal-overlay.component.html',
  styleUrl: './reveal-overlay.component.scss',
})
export class RevealOverlayComponent implements OnInit, AfterViewInit {
  @ViewChild('curtainTop') curtainTop!: ElementRef<HTMLElement>;
  @ViewChild('curtainBottom') curtainBottom!: ElementRef<HTMLElement>;

  splitting = false;
  fading = false;
  visible = true;
  tagline = '';

  private readonly taglines = [
    'AI-Powered CVs.',
    'Land interviews faster.',
    'Your dream job awaits.',
  ];

  ngOnInit(): void {
    document.body.classList.add('is-revealing');

    // Start typing the tagline after a short delay
    setTimeout(() => this.typeTagline(this.taglines[0], 0), 400);

    // Start the split sequence after 2.6s
    setTimeout(() => this.startSplit(), 2600);
  }

  ngAfterViewInit(): void {
    this.addParticles(this.curtainTop.nativeElement, 18);
    this.addParticles(this.curtainBottom.nativeElement, 18);
  }

  private typeTagline(text: string, charIndex: number): void {
    if (charIndex <= text.length) {
      this.tagline = text.slice(0, charIndex);
      setTimeout(() => this.typeTagline(text, charIndex + 1), 60);
    }
  }

  private startSplit(): void {
    // 1. Fade out the center logo
    this.fading = true;

    setTimeout(() => {
      // 2. Split the curtains apart
      this.splitting = true;

      setTimeout(() => {
        // 3. Reveal the page content
        document.body.classList.remove('is-revealing');
        document.body.classList.add('page-revealed');

        // 4. Remove the overlay from the DOM
        setTimeout(() => {
          this.visible = false;
        }, 400);
      }, 700);
    }, 300);
  }

  private addParticles(parent: HTMLElement, count: number): void {
    for (let i = 0; i < count; i++) {
      const p = document.createElement('div');
      p.className = 'curtain-particle';
      const size = Math.random() * 6 + 3;
      p.style.cssText = `
        width: ${size}px;
        height: ${size}px;
        left: ${Math.random() * 100}%;
        top: ${Math.random() * 100}%;
        animation-duration: ${3 + Math.random() * 4}s;
        animation-delay: ${Math.random() * 2}s;
      `;
      parent.appendChild(p);
    }
  }
}