import { Component, AfterViewInit } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-stats-section',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './stats.component.html',
  styleUrl: './stats.component.scss',
})
export class StatsSectionComponent implements AfterViewInit {
  private statsObserver: IntersectionObserver | null = null;

  ngAfterViewInit(): void {
    this.initScrollReveal();
    this.initCounterAnimation();
  }

  private initScrollReveal(): void {
    const observer = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting) {
            entry.target.classList.add('visible');
            observer.unobserve(entry.target);
          }
        });
      },
      { threshold: 0.12 }
    );
    document.querySelectorAll('app-stats-section .reveal').forEach((el) => observer.observe(el));
  }

  private initCounterAnimation(): void {
    const statsSection = document.querySelector('.stats-section');
    if (!statsSection) return;

    this.statsObserver = new IntersectionObserver(
      (entries) => {
        if (entries[0].isIntersecting) {
          this.animCounter('stat-1', 12847, '',  1600, 0);
          this.animCounter('stat-2', 92,    '%', 1200, 150);
          this.animCounter('stat-3', 8,     's', 900,  300);
          this.animCounter('stat-4', 2341,  '',  1600, 200);
          this.statsObserver?.disconnect();
        }
      },
      { threshold: 0.3 }
    );

    this.statsObserver.observe(statsSection);
  }

  private animCounter(
    id: string,
    target: number,
    suffix: string,
    duration: number,
    delay: number
  ): void {
    setTimeout(() => {
      const el = document.getElementById(id);
      if (!el) return;

      let start: number | null = null;

      const step = (timestamp: number) => {
        if (!start) start = timestamp;
        const progress = Math.min((timestamp - start) / duration, 1);
        const eased = 1 - Math.pow(1 - progress, 3);
        el.textContent =
          Math.round(target * eased).toLocaleString() + suffix;
        if (progress < 1) requestAnimationFrame(step);
      };

      requestAnimationFrame(step);
    }, delay);
  }
}
