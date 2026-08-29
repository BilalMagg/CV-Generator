import { Component, signal, inject, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { ApplicationService } from '@app/services/application.service';
import { ApplicationStatisticsDto, StatisticsTrendsDto, MonthlyTrendDto } from '@app/models/application.model';
import { RefreshButtonComponent } from '@app/shared/components/refresh-button/refresh-button.component';

interface StatCard {
  label: string;
  value: string;
  change: string;
  positive: boolean;
  color: string;
}

const MONTH_LABELS = ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'];

@Component({
  selector: 'app-analytics',
  standalone: true,
  imports: [CommonModule, RefreshButtonComponent],
  templateUrl: './analytics.component.html',
  styleUrl: './analytics.component.scss',
})
export class AnalyticsComponent implements OnInit {
  private appService = inject(ApplicationService);
  private sanitizer = inject(DomSanitizer);

  trends = signal<StatisticsTrendsDto | null>(null);
  loading = signal(true);
  refreshing = signal(false);
  periodMonths = signal(6);

  ngOnInit() { this.loadTrends(); }

  async loadTrends() {
    try {
      const res = await this.appService.getTrends();
      if (res.success && res.data) this.trends.set(res.data);
    } catch { } finally { this.loading.set(false); this.refreshing.set(false); }
  }

  onRefresh() { this.refreshing.set(true); this.loadTrends(); }

  setPeriod(months: number): void {
    this.periodMonths.set(months);
  }

  s = computed<ApplicationStatisticsDto>(() => this.trends()?.current ?? { total: 0, saved: 0, applied: 0, screening: 0, interview: 0, offer: 0, accepted: 0, rejected: 0, withdrawn: 0 });

  pct(a: number, b: number) { return a > 0 ? Math.round((b / a) * 100) : 0; }

  statCards = computed<StatCard[]>(() => {
    const s = this.s();
    const avgTime = this.trends()?.averageResponseTimeDays;
    return [
      { label: 'Response rate',      value: this.pct(s.total, s.screening + s.interview + s.offer + s.accepted + s.rejected) + '%', change: '+8%', positive: true,  color: 'oklch(0.5 0.16 250)' },
      { label: 'Interview rate',     value: this.pct(s.total, s.interview) + '%',                           change: '+5%', positive: true,  color: 'oklch(0.55 0.18 25)' },
      { label: 'Offer rate',         value: this.pct(s.total, s.offer + s.accepted) + '%',                  change: '+2%', positive: true,  color: 'oklch(0.55 0.14 155)' },
      { label: 'Avg. response time', value: avgTime != null ? avgTime.toFixed(1) + 'd' : '—',                change: '',    positive: true,  color: 'oklch(0.55 0.14 280)' },
    ];
  });

  filteredTrends = computed<MonthlyTrendDto[]>(() => {
    const trends = this.trends()?.monthlyTrends ?? [];
    const cutoff = this.periodMonths();
    return cutoff > 0 ? trends.slice(-cutoff) : trends;
  });

  donutSvg = computed<SafeHtml>(() => {
    const s = this.s();
    const total = s.total || 1;
    const slices = [
      { label: 'Applied',   count: s.applied + s.screening, pct: this.pct(total, s.applied + s.screening), color: 'oklch(0.72 0.01 80)' },
      { label: 'Interview', count: s.interview,             pct: this.pct(total, s.interview),             color: 'oklch(0.6 0.16 250)' },
      { label: 'Offer',     count: s.offer + s.accepted,    pct: this.pct(total, s.offer + s.accepted),    color: 'oklch(0.62 0.15 155)' },
      { label: 'Rejected',  count: s.rejected,              pct: this.pct(total, s.rejected),              color: 'oklch(0.62 0.18 25)' },
    ];

    const cx = 80, cy = 80, r = 60, hole = 36;
    let svg = '';
    let angle = -Math.PI / 2;
    const active = slices.filter(sl => sl.count > 0);
    if (active.length === 0) {
      svg += `<circle cx="${cx}" cy="${cy}" r="${r}" fill="oklch(0.93 0.004 80)"/>`;
    } else {
      active.forEach(sl => {
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
    }

    svg += `<text x="${cx}" y="${cy - 6}" text-anchor="middle" font-size="20" font-weight="700" fill="oklch(0.22 0.01 80)" font-family="inherit">${total}</text>`;
    svg += `<text x="${cx}" y="${cy + 10}" text-anchor="middle" font-size="10" fill="oklch(0.6 0.005 80)" font-family="inherit">Total apps</text>`;

    return this.sanitizer.bypassSecurityTrustHtml(
      `<svg viewBox="0 0 160 160" width="160" height="160" xmlns="http://www.w3.org/2000/svg">${svg}</svg>`
    );
  });

  donutSlices = computed(() => {
    const s = this.s();
    const total = s.total || 1;
    return [
      { label: 'Applied',   pct: this.pct(total, s.applied + s.screening), count: s.applied + s.screening, color: 'oklch(0.72 0.01 80)' },
      { label: 'Interview', pct: this.pct(total, s.interview),             count: s.interview,             color: 'oklch(0.6 0.16 250)' },
      { label: 'Offer',     pct: this.pct(total, s.offer + s.accepted),    count: s.offer + s.accepted,    color: 'oklch(0.62 0.15 155)' },
      { label: 'Rejected',  pct: this.pct(total, s.rejected),              count: s.rejected,              color: 'oklch(0.62 0.18 25)' },
    ];
  });

  private _shortNum(n: number): string {
    if (n >= 1000) return (n / 1000).toFixed(n % 1000 === 0 ? 0 : 1) + 'k';
    return String(n);
  }

  barChartSvg = computed<SafeHtml>(() => {
    const data = this.filteredTrends();
    if (data.length === 0) return this.sanitizer.bypassSecurityTrustHtml('');

    const pad = 32;
    const W = 340, H = 160, barW = 28;
    const gap = (W - 2 * pad - data.length * barW) / (data.length - 1);
    const maxTotal = Math.max(...data.map(d => d.saved + d.applied + d.screening + d.interview + d.offer + d.accepted + d.rejected + d.withdrawn), 1);
    const chartH = H - pad - 20;

    const colors = {
      saved:     'oklch(0.72 0.01 80)',
      applied:   'oklch(0.65 0.02 80)',
      screening: 'oklch(0.6 0.13 200)',
      interview: 'oklch(0.55 0.16 160)',
      offer:     'oklch(0.55 0.13 130)',
      accepted:  'oklch(0.62 0.15 155)',
      rejected:  'oklch(0.62 0.18 25)',
    };

    const barKeys: (keyof typeof colors)[] = ['saved', 'applied', 'screening', 'interview', 'offer', 'accepted', 'rejected'];

    let svg = '';

    const gridMax = Math.ceil(maxTotal / 5) * 5;
    const steps = Math.max(1, Math.floor(gridMax / 5));
    const gridLines = Array.from({ length: steps + 1 }, (_, i) => i * (gridMax / steps));
    gridLines.forEach(v => {
      const y = pad + chartH - (v / gridMax) * chartH;
      svg += `<line x1="${pad}" y1="${y.toFixed(1)}" x2="${W - pad}" y2="${y.toFixed(1)}" stroke="oklch(0.93 0.004 80)" stroke-width="1"/>`;
      svg += `<text x="${pad - 6}" y="${(y + 4).toFixed(1)}" text-anchor="end" font-size="9" fill="oklch(0.65 0.005 80)" font-family="inherit">${this._shortNum(Math.round(v))}</text>`;
    });

    data.forEach((d, i) => {
      const x = pad + i * (barW + gap);
      let y = pad + chartH;
      barKeys.forEach(k => {
        const val = d[k as keyof MonthlyTrendDto] as number;
        if (val === 0) return;
        const h = (val / gridMax) * chartH;
        y -= h;
        svg += `<rect x="${x.toFixed(1)}" y="${y.toFixed(1)}" width="${barW}" height="${h.toFixed(1)}" fill="${colors[k]}" rx="2"/>`;
      });
      const label = MONTH_LABELS[d.month - 1] + (d.year !== new Date().getFullYear() ? ` ${d.year}` : '');
      svg += `<text x="${(x + barW / 2).toFixed(1)}" y="${(pad + chartH + 14).toFixed(1)}" text-anchor="middle" font-size="9" fill="oklch(0.65 0.005 80)" font-family="inherit">${label}</text>`;
    });

    return this.sanitizer.bypassSecurityTrustHtml(
      `<svg viewBox="0 0 ${W} ${H}" width="100%" height="${H}" xmlns="http://www.w3.org/2000/svg">${svg}</svg>`
    );
  });
}
