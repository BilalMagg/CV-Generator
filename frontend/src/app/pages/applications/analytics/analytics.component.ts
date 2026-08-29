import { Component, signal, inject, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NgxChartsModule } from '@swimlane/ngx-charts';
import { ApplicationService } from '@app/services/application.service';
import {
  AnalyticsSummaryDto, MonthlyTrendDto, ApplicationStatus,
  STATUS_ORDER, STATUS_LABELS, STATUS_COLORS,
  ATTEMPT_CHANNEL_LABELS,
} from '@app/models/application.model';
import { RefreshButtonComponent } from '@app/shared/components/refresh-button/refresh-button.component';

const MONTH_LABELS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

interface KpiCard { label: string; value: string; sub: string; color: string; }
interface FunnelRow { label: string; count: number; pct: number; color: string; }
interface NameValue { name: string; value: number; }
interface StatusSlice extends NameValue { color: string; }

@Component({
  selector: 'app-analytics',
  standalone: true,
  imports: [CommonModule, NgxChartsModule, RefreshButtonComponent],
  templateUrl: './analytics.component.html',
  styleUrl: './analytics.component.scss',
})
export class AnalyticsComponent implements OnInit {
  private appService = inject(ApplicationService);

  summary = signal<AnalyticsSummaryDto | null>(null);
  loading = signal(true);
  refreshing = signal(false);
  periodMonths = signal(6);

  ngOnInit() { this.load(); }

  async load() {
    try {
      const res = await this.appService.getAnalyticsSummary();
      if (res.success && res.data) this.summary.set(res.data);
    } catch { } finally { this.loading.set(false); this.refreshing.set(false); }
  }

  onRefresh() { this.refreshing.set(true); this.load(); }
  setPeriod(months: number) { this.periodMonths.set(months); }

  stats = computed(() => this.summary()?.statistics ?? {
    total: 0, saved: 0, applied: 0, screening: 0, interview: 0, offer: 0, accepted: 0, rejected: 0, withdrawn: 0,
  });
  avgTime = computed(() => this.summary()?.averageResponseTimeDays ?? null);

  private pct(a: number, b: number): number { return a > 0 ? Math.round((b / a) * 100) : 0; }

  kpis = computed<KpiCard[]>(() => {
    const s = this.stats();
    const responded = s.screening + s.interview + s.offer + s.accepted + s.rejected;
    const inPipeline = s.total - s.rejected - s.withdrawn - s.accepted;
    const companies = this.summary()?.distinctCompanies ?? 0;
    const avg = this.avgTime();
    return [
      { label: 'Total applications', value: String(s.total), sub: `${s.saved} saved`, color: 'oklch(0.6 0.16 250)' },
      { label: 'In pipeline', value: String(inPipeline), sub: 'active, not closed', color: 'oklch(0.55 0.16 160)' },
      { label: 'Response rate', value: this.pct(s.total, responded) + '%', sub: `${responded} responded`, color: 'oklch(0.55 0.16 200)' },
      { label: 'Companies reached', value: String(companies), sub: 'distinct', color: 'oklch(0.62 0.15 130)' },
      { label: 'Avg. response', value: avg != null ? avg.toFixed(1) + 'd' : '—', sub: 'to first change', color: 'oklch(0.55 0.14 280)' },
    ];
  });

  private statKeyFor(st: ApplicationStatus): number {
    return (this.stats() as unknown as Record<string, number>)[st.toLowerCase()] ?? 0;
  }

  statusPie = computed<StatusSlice[]>(() =>
    STATUS_ORDER
      .map(st => ({ name: STATUS_LABELS[st], value: this.statKeyFor(st), color: STATUS_COLORS[st] }))
      .filter(d => d.value > 0)
  );
  statusColors = computed(() => this.statusPie().map(d => ({ name: d.name, value: d.color })));

  funnel = computed<FunnelRow[]>(() => {
    const f = this.summary()?.funnel ?? [];
    // Baseline = SAVED count when present, otherwise the largest stage (e.g. when nothing is still SAVED).
    const saved = f.find(r => r.stage === 'SAVED')?.count ?? 0;
    const max = f.reduce((m, r) => Math.max(m, r.count), 0);
    const base = saved > 0 ? saved : max;
    return f.map(st => ({
      label: STATUS_LABELS[st.stage as ApplicationStatus] ?? st.stage,
      count: st.count,
      pct: base > 0 ? Math.round((st.count / base) * 100) : 0,
      color: STATUS_COLORS[st.stage as ApplicationStatus] ?? 'oklch(0.6 0.01 80)',
    }));
  });

  topCompanies = computed<NameValue[]>(() =>
    (this.summary()?.topCompanies ?? []).map(c => ({ name: c.name, value: c.count }))
  );
  topCompanyList = computed(() => this.summary()?.topCompanies ?? []);
  topCompanyColors = computed(() => this.topCompanies().map(c => ({ name: c.name, value: 'oklch(0.6 0.16 250)' })));
  topCompanyView = computed<[number, number]>(() => [320, Math.max(120, this.topCompanies().length * 34 + 30)]);

  emailPie = computed<NameValue[]>(() => {
    const e = this.summary()?.email;
    if (!e) return [];
    const arr: NameValue[] = [];
    if (e.emailsSent > 0) arr.push({ name: 'Sent', value: e.emailsSent });
    if (e.emailsFailed > 0) arr.push({ name: 'Failed', value: e.emailsFailed });
    return arr;
  });
  emailColors = computed(() => [
    { name: 'Sent', value: 'oklch(0.62 0.15 155)' },
    { name: 'Failed', value: 'oklch(0.62 0.18 25)' },
  ]);

  channelBreakdown = computed<NameValue[]>(() => {
    const c = this.summary()?.channelCounts ?? {};
    return Object.keys(c)
      .map(key => ({ name: ATTEMPT_CHANNEL_LABELS[key as keyof typeof ATTEMPT_CHANNEL_LABELS] ?? key, value: c[key] }))
      .filter(d => d.value > 0)
      .sort((a, b) => b.value - a.value);
  });

  filteredTrends = computed<MonthlyTrendDto[]>(() => {
    const trends = this.summary()?.monthlyTrends ?? [];
    const cutoff = this.periodMonths();
    return cutoff > 0 ? trends.slice(-cutoff) : trends;
  });

  monthlyStacked = computed(() =>
    this.filteredTrends().map(d => ({
      name: MONTH_LABELS[d.month - 1] + (d.year !== new Date().getFullYear() ? ` ${d.year}` : ''),
      series: STATUS_ORDER.map(st => ({ name: STATUS_LABELS[st], value: (d as unknown as Record<string, number>)[st.toLowerCase()] ?? 0 })),
    }))
  );

  hasData = computed(() => this.stats().total > 0);
}
