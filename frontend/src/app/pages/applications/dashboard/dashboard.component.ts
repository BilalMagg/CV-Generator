import { Component, signal, inject, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { Router } from '@angular/router';
import { ApplicationService } from '../../../services/application.service';
import { ApplicationStatisticsDto } from '../../../models/application.model';

interface SankeyNode {
  id: string; label: string; count: number; x: number; y: number; h: number;
  color: string;
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

  sankeySvg = computed<SafeHtml>(() => {
    const s = this.stats();
    const total = s.total || 34;
    const screening = s.pending + s.reviewed || 9;
    const interviewed = s.interview || 3;
    const offered = s.accepted || 1;

    const nodes: SankeyNode[] = [
      { id: 'applied',   label: 'Applied',   count: total,      x: 30, y: 20, h: 160, color: '#7F77DD' },
      { id: 'screen',    label: 'Screening', count: screening,  x: 150, y: 20, h: Math.max(10, (screening/total)*160), color: '#378ADD' },
      { id: 'interview', label: 'Interview', count: interviewed,x: 270, y: 20, h: Math.max(10, (interviewed/total)*160), color: '#1D9E75' },
      { id: 'offer',     label: 'Offer',     count: offered,    x: 390, y: 20, h: Math.max(10, (offered/total)*160), color: '#EF9F27' },
      { id: 'accepted',  label: 'Accepted',  count: offered,    x: 490, y: 14, h: Math.max(5, (offered/total)*160), color: '#639922' },
      { id: 'rejected',  label: 'Rejected',  count: s.rejected, x: 150, y: 118, h: Math.max(10, (s.rejected/total)*160), color: '#E24B4A' },
    ];

    const NW = 16;
    const flows = [
      { from: 'applied', to: 'screen', val: screening },
      { from: 'applied', to: 'rejected', val: nodes.find(n => n.id === 'rejected')!.count },
      { from: 'screen', to: 'interview', val: interviewed },
      { from: 'interview', to: 'offer', val: offered },
    ];

    const nodeMap = new Map<string, SankeyNode & { _usedH?: number }>();
    nodes.forEach(n => nodeMap.set(n.id, { ...n, _usedH: 0 }));

    let svgStr = '';

    flows.forEach(f => {
      const fn = nodeMap.get(f.from)!;
      const tn = nodeMap.get(f.to)!;
      const ratio = f.val / fn.count;
      const flowH = Math.max(3, Math.round(fn.h * ratio));
      const x1 = fn.x + NW;
      const x2 = tn.x;
      const y1 = fn.y + (fn._usedH || 0) + flowH / 2;
      const y2 = tn.y + (tn._usedH || 0) + flowH / 2;
      fn._usedH = (fn._usedH || 0) + flowH;
      tn._usedH = (tn._usedH || 0) + flowH;
      const mx = (x1 + x2) / 2;
      const pct = Math.round((f.val / fn.count) * 100);
      svgStr += `<path d="M${x1},${y1 - flowH / 2} C${mx},${y1 - flowH / 2} ${mx},${y2 - flowH / 2} ${x2},${y2 - flowH / 2} L${x2},${y2 + flowH / 2} C${mx},${y2 + flowH / 2} ${mx},${y1 + flowH / 2} ${x1},${y1 + flowH / 2} Z" fill="${fn.color}" opacity="0.18"/>`;
    });

    nodes.forEach(n => {
      const x = n.x;
      svgStr += `<rect x="${x}" y="${n.y}" width="${NW}" height="${n.h}" rx="3" fill="${n.color}"/>`;
      const lx = x > 400 ? x + NW + 4 : x + NW + 6;
      svgStr += `<text x="${lx}" y="${n.y + Math.min(16, n.h / 2 + 6)}" font-size="11" fill="var(--color-text-primary)" font-weight="500">${n.label}</text>`;
      svgStr += `<text x="${lx}" y="${n.y + Math.min(28, n.h / 2 + 18)}" font-size="10" fill="var(--color-text-tertiary)">${n.count}</text>`;
    });

    return this.sanitizer.bypassSecurityTrustHtml(
      `<svg viewBox="0 0 540 220" width="100%" height="220" xmlns="http://www.w3.org/2000/svg">${svgStr}</svg>`
    );
  });

  timelineSvg = computed<SafeHtml>(() => {
    const TW = 620, TH = 80, pad = 10;
    const days = 30;
    const vals = Array.from({ length: days }, (_, i) =>
      Math.max(0, Math.round(Math.sin(i / 4) * 2 + Math.random() * 3 + 0.5))
    );
    vals[3] = 4; vals[7] = 5; vals[12] = 3; vals[18] = 4; vals[22] = 5; vals[27] = 2;
    const maxV = Math.max(...vals, 1);

    const pts = vals.map((v, i) => {
      const x = pad + (i / (days - 1)) * (TW - 2 * pad);
      const y = TH - pad - (v / maxV) * (TH - 2 * pad);
      return [x, y];
    });

    const monthNames = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
    const now = new Date();
    const month = monthNames[now.getMonth()];

    let out = `<defs><linearGradient id="tlg" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stop-color="#7F77DD" stop-opacity="0.18"/><stop offset="100%" stop-color="#7F77DD" stop-opacity="0"/></linearGradient></defs>`;
    const areaPath = `M${pts[0][0]},${TH} ` + pts.map(p => `L${p[0].toFixed(1)},${p[1].toFixed(1)}`).join(' ') + ` L${pts[pts.length - 1][0]},${TH} Z`;
    out += `<path d="${areaPath}" fill="url(#tlg)"/>`;
    const linePath = `M${pts.map(p => p[0].toFixed(1) + ',' + p[1].toFixed(1)).join(' L')}`;
    out += `<path d="${linePath}" fill="none" stroke="#7F77DD" stroke-width="1.5" stroke-linejoin="round"/>`;
    pts.forEach((p, i) => { if (vals[i] >= 3) out += `<circle cx="${p[0].toFixed(1)}" cy="${p[1].toFixed(1)}" r="3" fill="#7F77DD"/>`; });
    out += `<text x="${pad}" y="${TH + 14}" font-size="10" fill="var(--color-text-tertiary)">1 ${month}</text>`;
    out += `<text x="${TW / 2}" y="${TH + 14}" font-size="10" fill="var(--color-text-tertiary)" text-anchor="middle">15 ${month}</text>`;
    out += `<text x="${TW - pad}" y="${TH + 14}" font-size="10" fill="var(--color-text-tertiary)" text-anchor="end">${now.getDate()} ${month}</text>`;

    return this.sanitizer.bypassSecurityTrustHtml(
      `<svg viewBox="0 0 ${TW} ${TH + 18}" width="100%" height="98" xmlns="http://www.w3.org/2000/svg">${out}</svg>`
    );
  });

  getConversion(a: number, b: number): string {
    if (a === 0) return '0%';
    return Math.round((b / a) * 100) + '%';
  }

  goToApplications() { this.router.navigate(['/applications/list']); }
}
