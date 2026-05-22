import { Component, signal, inject, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { Router } from '@angular/router';
import { ApplicationService } from '@app/services/application.service';
import { AuthService } from '@app/services/auth.service';
import { ApplicationStatisticsDto, ApplicationResponseDto } from '@app/models/application.model';

interface StatCard {
  label: string;
  displayValue: string;
  sub: string;
  change: number;
  dotColor: string;
  valueColor: string;
  bars: number[];
}

interface FollowUpItem {
  id: string;
  company: string;
  role: string;
  timeAgo: string;
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent implements OnInit {
  private appService = inject(ApplicationService);
  private authService = inject(AuthService);
  private sanitizer = inject(DomSanitizer);
  private router = inject(Router);

  stats = signal<ApplicationStatisticsDto>({
    total: 0, pending: 0, reviewed: 0, interview: 0, accepted: 0, rejected: 0, cancelled: 0,
  });
  applications = signal<ApplicationResponseDto[]>([]);
  loading = signal(true);

  ngOnInit() { this.loadStats(); }

  async loadStats() {
    try {
      const [statsRes, appsRes] = await Promise.all([
        this.appService.getStatistics(),
        this.appService.getAll({ page: 1, pageSize: 50 }),
      ]);
      if (statsRes.success && statsRes.data) this.stats.set(statsRes.data);
      if (appsRes.success && appsRes.data) this.applications.set(appsRes.data.items);
    } catch { } finally { this.loading.set(false); }
  }

  firstName = computed(() => this.authService.currentUser()?.firstName ?? 'there');

  timeOfDay = computed(() => {
    const h = new Date().getHours();
    if (h < 12) return 'morning';
    if (h < 17) return 'afternoon';
    return 'evening';
  });

  todayLabel = computed(() => {
    const d = new Date();
    const days = ['Sunday','Monday','Tuesday','Wednesday','Thursday','Friday','Saturday'];
    const months = ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'];
    return `${days[d.getDay()]} · ${months[d.getMonth()]} ${d.getDate()}`;
  });

  pendingFollowUps = computed(() => this.stats().pending);
  activeOffers = computed(() => this.stats().accepted);

  statCards = computed<StatCard[]>(() => {
    const s = this.stats();
    const pct = (a: number, b: number) => a > 0 ? Math.round((b / a) * 100) : 0;
    return [
      {
        label: 'Total applications',
        displayValue: String(s.total),
        sub: 'vs last month',
        change: 12,
        dotColor: 'oklch(0.22 0.01 80)',
        valueColor: 'var(--text)',
        bars: [6, 9, 7, 11, 8, 14, 12, 10, 16, 18, s.total || 24],
      },
      {
        label: 'Interviewing',
        displayValue: String(s.interview),
        sub: `${s.interview} active`,
        change: 50,
        dotColor: 'oklch(0.6 0.16 250)',
        valueColor: 'oklch(0.5 0.16 250)',
        bars: [2, 3, 1, 4, 2, 5, 3, 2, 4, 5, s.interview || 6],
      },
      {
        label: 'Offers',
        displayValue: String(s.accepted),
        sub: `${s.pending} pending`,
        change: 1,
        dotColor: 'oklch(0.62 0.15 155)',
        valueColor: 'oklch(0.45 0.14 155)',
        bars: [0, 1, 0, 1, 2, 1, 0, 2, 1, 2, s.accepted || 3],
      },
      {
        label: 'Response rate',
        displayValue: pct(s.total, s.interview + s.accepted + s.rejected) + '%',
        sub: 'industry avg 23%',
        change: 8,
        dotColor: 'oklch(0.62 0.18 25)',
        valueColor: 'oklch(0.55 0.18 25)',
        bars: [30, 35, 28, 40, 38, 36, 42, 40, 44, 41, 43],
      },
    ];
  });

  followUpItems = computed<FollowUpItem[]>(() => {
    const apps = this.applications();
    if (apps.length === 0) return [];

    const pending = apps.filter(a => a.status === 'PENDING' || a.status === 'REVIEWED');
    if (pending.length === 0) return [];

    return pending.slice(0, 5).map(a => ({
      id: a.id,
      company: a.companyName,
      role: a.positionTitle,
      timeAgo: this.relativeTime(a.updatedAt),
    }));
  });

  private relativeTime(dateStr: string): string {
    const diff = Date.now() - new Date(dateStr).getTime();
    const days = Math.floor(diff / 86400000);
    if (days === 0) return 'today';
    if (days === 1) return '1d ago';
    return `${days}d ago`;
  }

  pipelineSvg = computed<SafeHtml>(() => {
    const s = this.stats();
    const total = s.total || 24;
    const interview = s.interview || 6;
    const offered = s.accepted || 3;
    const rejected = s.rejected || 4;
    const applied = total - interview - offered - rejected;

    const W = 480, H = 120;
    const statuses = [
      { label: 'Applied', count: Math.max(1, applied), color: 'oklch(0.68 0.015 250)' },
      { label: 'Interview', count: Math.max(0, interview), color: 'oklch(0.6 0.16 250)' },
      { label: 'Offer', count: Math.max(0, offered), color: 'oklch(0.62 0.15 155)' },
      { label: 'Rejected', count: Math.max(0, rejected), color: 'oklch(0.62 0.18 25)' },
    ];

    const maxCount = Math.max(...statuses.map(s => s.count), 1);
    const barW = 48, gap = (W - statuses.length * barW) / (statuses.length + 1);
    let svg = '';

    statuses.forEach((st, i) => {
      const x = gap + i * (barW + gap);
      const barH = Math.max(8, (st.count / maxCount) * 80);
      const y = H - 24 - barH;
      svg += `<rect x="${x}" y="${y}" width="${barW}" height="${barH}" rx="5" fill="${st.color}" opacity="0.9"/>`;
      svg += `<text x="${x + barW / 2}" y="${H - 8}" text-anchor="middle" font-size="10" fill="oklch(0.6 0.005 80)" font-family="inherit">${st.label}</text>`;
      svg += `<text x="${x + barW / 2}" y="${y - 4}" text-anchor="middle" font-size="11" fill="${st.color}" font-weight="600" font-family="inherit">${st.count}</text>`;
    });

    return this.sanitizer.bypassSecurityTrustHtml(
      `<svg viewBox="0 0 ${W} ${H}" width="100%" height="130" xmlns="http://www.w3.org/2000/svg">${svg}</svg>`
    );
  });

  logoBg(name: string): string {
    const palette = [
      'oklch(0.93 0.04 250)', 'oklch(0.93 0.04 160)', 'oklch(0.93 0.04 65)',
      'oklch(0.93 0.04 300)', 'oklch(0.93 0.04 25)',  'oklch(0.94 0.02 80)',
    ];
    let h = 0;
    for (let i = 0; i < name.length; i++) h = name.charCodeAt(i) + ((h << 5) - h);
    return palette[Math.abs(h) % palette.length];
  }

  logoText(name: string): string { return name.slice(0, 2).toUpperCase(); }

  goToGenerate() { this.router.navigate(['/applications/generate']); }
  addApplication() { this.router.navigate(['/applications/new']); }
  goToCalendar() { this.router.navigate(['/applications/calendar']); }
}
