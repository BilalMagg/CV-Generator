import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';

interface ArcAvatar {
  initials: string;
  hue: number;
  arcY: number;
  scale: number;
  zIndex: number;
}

@Component({
  selector: 'app-arc-cta-section',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './arc-cta.component.html',
  styleUrl: './arc-cta.component.scss',
})
export class ArcCtaSectionComponent {
  private readonly SOURCE = [
    { initials: 'SL', hue: 250 },
    { initials: 'MR', hue: 145 },
    { initials: 'AK', hue: 30  },
    { initials: 'JP', hue: 265 },
    { initials: 'YZ', hue: 250 }, // center
    { initials: 'BM', hue: 155 },
    { initials: 'CL', hue: 25  },
    { initials: 'TR', hue: 75  },
    { initials: 'NF', hue: 145 },
  ];

  readonly avatars: ArcAvatar[] = this.SOURCE.map((a, i) => {
    const center = Math.floor(this.SOURCE.length / 2);
    const dist = Math.abs(i - center);
    return {
      ...a,
      arcY:   dist * dist * 4,          // 0 → 4 → 16 → 36 → 64 px
      scale:  1 - dist * 0.055,          // 1.0 → 0.78
      zIndex: this.SOURCE.length - dist, // center on top
    };
  });

  gradientFor(hue: number): string {
    return `linear-gradient(145deg, oklch(0.62 0.16 ${hue}), oklch(0.5 0.16 ${hue}))`;
  }
}
