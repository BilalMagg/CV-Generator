import { Component, signal, inject, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { Router } from '@angular/router';
import { ApplicationService } from '../../../services/application.service';
import { ApplicationStatisticsDto } from '../../../models/application.model';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent implements OnInit {
  private appService = inject(ApplicationService);
  private sanitizer = inject(DomSanitizer);
  private router = inject(Router);

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

  kpis = computed(() => {
    const s = this.stats();
    return [
      { label: 'Total applications', value: String(s.total), sub: 'tracked' },
      { label: 'Response rate', value: this.getConversion(s.total, s.interview + s.accepted + s.rejected), sub: 'incl. screening' },
      { label: 'Interview rate', value: this.getConversion(s.total, s.interview), sub: 'of total' },
      { label: 'Offers', value: String(s.accepted), sub: 'received' },
      { label: 'Pending', value: String(s.pending), sub: 'awaiting reply' },
      { label: 'Rejected', value: String(s.rejected), sub: 'keep going' },
    ];
  });

  pipelineSvg = computed<SafeHtml>(() => {
    const s = this.stats();
    const total = s.total || 34;
    const screen = s.pending + s.reviewed || 9;
    const interviewed = s.interview || 3;
    const offered = s.accepted || 1;
    const rejected = s.rejected || 7;

    const W = 540, NW = 14;
    interface Node { id: string; label: string; count: number; x: number; y: number; h: number; color: string; _used: number }
    const nodes: Node[] = [
      { id: 'applied',   label: 'Applied',   count: total,      x: 20,  y: 20, h: 140, color: 'oklch(0.6 0.16 250)', _used: 0 },
      { id: 'screen',    label: 'Screening', count: screen,     x: 160, y: 20, h: Math.max(10, (screen / total) * 140),      color: 'oklch(0.6 0.16 250)', _used: 0 },
      { id: 'interview', label: 'Interview', count: interviewed,x: 300, y: 20, h: Math.max(10, (interviewed / total) * 140), color: 'oklch(0.55 0.16 160)', _used: 0 },
      { id: 'offer',     label: 'Offer',     count: offered,    x: 420, y: 20, h: Math.max(6, (offered / total) * 140),      color: 'oklch(0.55 0.13 130)', _used: 0 },
      { id: 'rejected',  label: 'Rejected',  count: rejected,   x: 160, y: 110, h: Math.max(8, (rejected / total) * 80),    color: 'oklch(0.55 0.18 25)', _used: 0 },
    ];

    const nodeMap = new Map(nodes.map(n => [n.id, n]));
    const flows = [
      { from: 'applied', to: 'screen',    val: screen },
      { from: 'applied', to: 'rejected',  val: rejected },
      { from: 'screen',  to: 'interview', val: interviewed },
      { from: 'interview', to: 'offer',   val: offered },
    ];

    let svg = '';
    flows.forEach(f => {
      const fn = nodeMap.get(f.from)!;
      const tn = nodeMap.get(f.to)!;
      const ratio = fn.count > 0 ? f.val / fn.count : 0;
      const fh = Math.max(3, Math.round(fn.h * ratio));
      const x1 = fn.x + NW;
      const x2 = tn.x;
      const y1 = fn.y + fn._used + fh / 2;
      const y2 = tn.y + tn._used + fh / 2;
      fn._used += fh;
      tn._used += fh;
      const mx = (x1 + x2) / 2;
      svg += `<path d="M${x1},${y1 - fh/2} C${mx},${y1 - fh/2} ${mx},${y2 - fh/2} ${x2},${y2 - fh/2} L${x2},${y2 + fh/2} C${mx},${y2 + fh/2} ${mx},${y1 + fh/2} ${x1},${y1 + fh/2} Z" fill="${fn.color}" opacity="0.15"/>`;
    });

    nodes.forEach(n => {
      svg += `<rect x="${n.x}" y="${n.y}" width="${NW}" height="${n.h}" rx="3" fill="${n.color}"/>`;
      svg += `<text x="${n.x + NW + 6}" y="${n.y + 13}" font-size="11" fill="oklch(0.35 0.01 80)" font-weight="500">${n.label}</text>`;
      svg += `<text x="${n.x + NW + 6}" y="${n.y + 25}" font-size="10" fill="oklch(0.6 0.005 80)">${n.count}</text>`;
    });

    return this.sanitizer.bypassSecurityTrustHtml(
      `<svg viewBox="0 0 ${W} 220" width="100%" height="180" xmlns="http://www.w3.org/2000/svg">${svg}</svg>`
    );
  });

  timelineSvg = computed<SafeHtml>(() => {
    const TW = 560, TH = 70, pad = 8;
    const days = 30;
    const vals = Array.from({ length: days }, (_, i) =>
      Math.max(0, Math.round(Math.sin(i / 4) * 2 + 1.5))
    );
    vals[3] = 4; vals[7] = 5; vals[12] = 3; vals[18] = 4; vals[22] = 5; vals[27] = 2;
    const maxV = Math.max(...vals, 1);

    const pts = vals.map((v, i) => [
      pad + (i / (days - 1)) * (TW - 2 * pad),
      TH - pad - (v / maxV) * (TH - 2 * pad),
    ]);

    const months = ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'];
    const now = new Date();
    const m = months[now.getMonth()];

    const area = `M${pts[0][0]},${TH} ` + pts.map(p => `L${p[0].toFixed(1)},${p[1].toFixed(1)}`).join(' ') + ` L${pts[pts.length-1][0]},${TH} Z`;
    const line = `M${pts.map(p => `${p[0].toFixed(1)},${p[1].toFixed(1)}`).join(' L')}`;

    let out = `<defs><linearGradient id="tg" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stop-color="oklch(0.6 0.16 250)" stop-opacity="0.15"/><stop offset="100%" stop-color="oklch(0.6 0.16 250)" stop-opacity="0"/></linearGradient></defs>`;
    out += `<path d="${area}" fill="url(#tg)"/>`;
    out += `<path d="${line}" fill="none" stroke="oklch(0.6 0.16 250)" stroke-width="1.5" stroke-linejoin="round"/>`;
    pts.forEach((p, i) => { if (vals[i] >= 3) out += `<circle cx="${p[0].toFixed(1)}" cy="${p[1].toFixed(1)}" r="2.5" fill="oklch(0.6 0.16 250)"/>`; });
    out += `<text x="${pad}" y="${TH + 13}" font-size="10" fill="oklch(0.6 0.005 80)">1 ${m}</text>`;
    out += `<text x="${TW/2}" y="${TH + 13}" font-size="10" fill="oklch(0.6 0.005 80)" text-anchor="middle">15 ${m}</text>`;
    out += `<text x="${TW - pad}" y="${TH + 13}" font-size="10" fill="oklch(0.6 0.005 80)" text-anchor="end">${now.getDate()} ${m}</text>`;

    return this.sanitizer.bypassSecurityTrustHtml(
      `<svg viewBox="0 0 ${TW} ${TH + 18}" width="100%" height="88" xmlns="http://www.w3.org/2000/svg">${out}</svg>`
    );
  });

  activityItems = computed(() => {
    const s = this.stats();
    return [
      { icon: 'check', color: 'green',  title: `${s.total} applications tracked`,   meta: 'Total in pipeline' },
      { icon: 'video', color: 'blue',   title: `${s.interview} in interview stage`, meta: 'Move forward!' },
      { icon: 'mail',  color: 'amber',  title: `${s.pending} pending responses`,    meta: 'Consider follow-ups' },
      { icon: 'x',     color: 'coral',  title: `${s.rejected} rejected`,            meta: 'Keep applying!' },
    ];
  });

  getConversion(a: number, b: number): string {
    if (a === 0) return '0%';
    return Math.round((b / a) * 100) + '%';
  }

  goToApplications() { this.router.navigate(['/applications/list']); }
}
