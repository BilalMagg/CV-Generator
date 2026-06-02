import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { APP_NAME } from '@app/app-name';

interface ArcAvatar {
  initials: string;
  hue: number;
  xPct: number;    // 0–100 % horizontal position
  yPx: number;     // px drop from top (0 = highest / center)
  size: number;    // diameter in px
  fontSize: number; // px
  zIndex: number;
  opacity: number;
}

@Component({
  selector: 'app-arc-cta-section',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './arc-cta.component.html',
  styleUrl: './arc-cta.component.scss',
})
export class ArcCtaSectionComponent {
  appName = APP_NAME;
  private readonly SOURCE = [
    { initials: 'SL', hue: 250 },
    { initials: 'MR', hue: 145 },
    { initials: 'AK', hue: 30  },
    { initials: 'JP', hue: 265 },
    { initials: 'EM', hue: 155 },
    { initials: 'YZ', hue: 250 }, // center
    { initials: 'BM', hue: 75  },
    { initials: 'CL', hue: 25  },
    { initials: 'TR', hue: 265 },
    { initials: 'NF', hue: 145 },
    { initials: 'KR', hue: 30  },
  ];

  readonly avatars: ArcAvatar[] = this.SOURCE.map((a, i) => {
    const n      = this.SOURCE.length;
    const center = (n - 1) / 2;       // 5
    const dist   = Math.abs(i - center);
    const ratio  = dist / center;      // 0 → 1

    return {
      ...a,
      xPct:    (i / (n - 1)) * 100,          // 0 % … 100 %
      yPx:     Math.round(ratio * ratio * 150), // parabola: 0 → 150 px
      size:     Math.round(90 - ratio * 46),     // 90 px center → 44 px edges
      fontSize: Math.round(20 - ratio * 8),      // 20 px center → 12 px edges
      zIndex:   Math.round(n - dist),
      opacity:  +(1 - ratio * 0.28).toFixed(2),  // 1.0 → 0.72
    };
  });

  /** Container height = tallest point (edge y + edge size) */
  readonly rowHeight = Math.max(
    ...this.avatars.map(a => a.yPx + a.size)
  ) + 16; // 16 px bottom breathing room

  gradientFor(hue: number): string {
    return `linear-gradient(145deg, oklch(0.62 0.16 ${hue}), oklch(0.5 0.16 ${hue}))`;
  }
}
