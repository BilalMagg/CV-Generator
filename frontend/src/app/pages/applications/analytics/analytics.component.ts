import { Component, signal, inject, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { ApplicationService } from '@app/services/application.service';
import { ApplicationStatisticsDto } from '@app/models/application.model';

interface StatCard {
  label: string;
  value: string;
  change: string;
  positive: boolean;
  color: string;
}

@Component({
  selector: 'app-analytics',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './analytics.component.html',
  styleUrl: './analytics.component.scss',
})
export class AnalyticsComponent implements OnInit {
  private appService = inject(ApplicationService);
  private sanitizer = inject(DomSanitizer);

  stats = signal<ApplicationStatisticsDto>({
    total: 0, pending: 0, reviewed: 0, interview: 0, accepted: 0, rejected: 0, cancelled: 0,
  });
  loading = signal(true);

  ngOnInit() { this.loadStats(); }

  async loadStats() {
    try {
      const res = await this.appService.getStatistics();
      if (res.success && res.data) this.stats.set(res.data);
    } catch { } finally { this.loading.set(false); }
  }

  pct(a: number, b: number) { return a > 0 ? Math.round((b / a) * 100) : 0; }

  statCards = computed<StatCard[]>(() => {
    const s = this.stats();
    return [
      { label: 'Response rate',      value: this.pct(s.total, s.interview + s.accepted + s.rejected) + '%', change: '+8%', positive: true,  color: 'oklch(0.5 0.16 250)' },
      { label: 'Interview rate',     value: this.pct(s.total, s.interview) + '%',                           change: '+5%', positive: true,  color: 'oklch(0.55 0.18 25)' },
      { label: 'Offer rate',         value: this.pct(s.total, s.accepted) + '%',                            change: '+2%', positive: true,  color: 'oklch(0.55 0.14 155)' },
      { label: 'Avg. response time', value: '6.4d',                                                          change: '-1.2d', positive: false, color: 'oklch(0.55 0.14 280)' },
    ];
  });

  donutSvg = computed<SafeHtml>(() => {
    const s = this.stats();
    const total = s.total || 21;
    const slices = [
      { label: 'Applied',   pct: 38, count: Math.round(total * 0.38), color: 'oklch(0.72 0.01 80)' },
      { label: 'Interview', pct: 29, count: Math.round(total * 0.29), color: 'oklch(0.6 0.16 250)' },
      { label: 'Offer',     pct: 14, count: Math.round(total * 0.14), color: 'oklch(0.62 0.15 155)' },
      { label: 'Rejected',  pct: 19, count: Math.round(total * 0.19), color: 'oklch(0.62 0.18 25)' },
    ];

    if (s.total > 0) {
      slices[0].count = s.pending + s.reviewed; slices[0].pct = this.pct(total, slices[0].count);
      slices[1].count = s.interview;             slices[1].pct = this.pct(total, s.interview);
      slices[2].count = s.accepted;              slices[2].pct = this.pct(total, s.accepted);
      slices[3].count = s.rejected;              slices[3].pct = this.pct(total, s.rejected);
    }

    const cx = 80, cy = 80, r = 60, hole = 36;
    let svg = '';
    let angle = -Math.PI / 2;

    slices.forEach(sl => {
      const span = (sl.pct / 100) * 2 * Math.PI;
      const x1 = cx + r * Math.cos(angle);
      const y1 = cy + r * Math.sin(angle);
      const x2 = cx + r * Math.cos(angle + span);
      const y2 = cy + r * Math.sin(angle + span);
      const hx1 = cx + hole * Math.cos(angle);
      const hy1 = cy + hole * Math.sin(angle);
      const hx2 = cx + hole * Math.cos(angle + span);
      const hy2 = cy + hole * Math.sin(angle + span);
      const large = span > Math.PI ? 1 : 0;
      svg += `<path d="M${hx1.toFixed(2)},${hy1.toFixed(2)} L${x1.toFixed(2)},${y1.toFixed(2)} A${r},${r} 0 ${large},1 ${x2.toFixed(2)},${y2.toFixed(2)} L${hx2.toFixed(2)},${hy2.toFixed(2)} A${hole},${hole} 0 ${large},0 ${hx1.toFixed(2)},${hy1.toFixed(2)} Z" fill="${sl.color}"/>`;
      angle += span;
    });

    svg += `<text x="${cx}" y="${cy - 6}" text-anchor="middle" font-size="20" font-weight="700" fill="oklch(0.22 0.01 80)" font-family="inherit">${total}</text>`;
    svg += `<text x="${cx}" y="${cy + 10}" text-anchor="middle" font-size="10" fill="oklch(0.6 0.005 80)" font-family="inherit">Total apps</text>`;

    return this.sanitizer.bypassSecurityTrustHtml(
      `<svg viewBox="0 0 160 160" width="160" height="160" xmlns="http://www.w3.org/2000/svg">${svg}</svg>`
    );
  });

  donutSlices = computed(() => {
    const s = this.stats();
    const total = s.total || 21;
    return [
      { label: 'Applied',   pct: this.pct(total, s.total > 0 ? s.pending + s.reviewed : Math.round(total * 0.38)), count: s.total > 0 ? s.pending + s.reviewed : 8, color: 'oklch(0.72 0.01 80)' },
      { label: 'Interview', pct: this.pct(total, s.total > 0 ? s.interview : Math.round(total * 0.29)),             count: s.total > 0 ? s.interview : 6,             color: 'oklch(0.6 0.16 250)' },
      { label: 'Offer',     pct: this.pct(total, s.total > 0 ? s.accepted : Math.round(total * 0.14)),              count: s.total > 0 ? s.accepted : 3,              color: 'oklch(0.62 0.15 155)' },
      { label: 'Rejected',  pct: this.pct(total, s.total > 0 ? s.rejected : Math.round(total * 0.19)),              count: s.total > 0 ? s.rejected : 4,              color: 'oklch(0.62 0.18 25)' },
    ];
  });

  barChartSvg = computed<SafeHtml>(() => {
    const months = ['Nov', 'Dec', 'Jan', 'Feb', 'Mar', 'Apr', 'May'];
    const data = [
      { applied: 3, interview: 1, offer: 0, rejected: 1 },
      { applied: 5, interview: 2, offer: 1, rejected: 1 },
      { applied: 7, interview: 3, offer: 1, rejected: 2 },
      { applied: 6, interview: 4, offer: 1, rejected: 1 },
      { applied: 8, interview: 3, offer: 1, rejected: 2 },
      { applied: 12, interview: 5, offer: 2, rejected: 3 },
      { applied: 10, interview: 4, offer: 2, rejected: 2 },
    ];

    const W = 320, H = 160, pad = 16, barW = 28, gap = (W - 2 * pad - data.length * barW) / (data.length - 1);
    const maxTotal = Math.max(...data.map(d => d.applied + d.interview + d.offer + d.rejected), 1);
    const chartH = H - pad - 20;

    const colors = {
      applied:   'oklch(0.72 0.01 80)',
      interview: 'oklch(0.6 0.16 250)',
      offer:     'oklch(0.62 0.15 155)',
      rejected:  'oklch(0.62 0.18 25)',
    };

    let svg = '';

    // Gridlines
    [0, 5, 10, 15, 20].forEach(v => {
      const y = pad + chartH - (v / maxTotal) * chartH;
      svg += `<line x1="${pad}" y1="${y.toFixed(1)}" x2="${W - pad}" y2="${y.toFixed(1)}" stroke="oklch(0.93 0.004 80)" stroke-width="1"/>`;
      svg += `<text x="${pad - 4}" y="${(y + 4).toFixed(1)}" text-anchor="end" font-size="9" fill="oklch(0.65 0.005 80)" font-family="inherit">${v}</text>`;
    });

    data.forEach((d, i) => {
      const x = pad + i * (barW + gap);
      let y = pad + chartH;
      const keys: (keyof typeof d)[] = ['applied', 'interview', 'offer', 'rejected'];
      keys.forEach(k => {
        if (d[k] === 0) return;
        const h = (d[k] / maxTotal) * chartH;
        y -= h;
        svg += `<rect x="${x.toFixed(1)}" y="${y.toFixed(1)}" width="${barW}" height="${h.toFixed(1)}" fill="${colors[k as keyof typeof colors]}" rx="2"/>`;
      });
      svg += `<text x="${(x + barW / 2).toFixed(1)}" y="${(pad + chartH + 14).toFixed(1)}" text-anchor="middle" font-size="9" fill="oklch(0.65 0.005 80)" font-family="inherit">${months[i]}</text>`;
    });

    return this.sanitizer.bypassSecurityTrustHtml(
      `<svg viewBox="0 0 ${W} ${H}" width="100%" height="${H}" xmlns="http://www.w3.org/2000/svg">${svg}</svg>`
    );
  });
}
