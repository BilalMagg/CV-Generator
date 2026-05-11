import { Component, signal, computed, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
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
  private router = inject(Router);

  stats = signal<ApplicationStatisticsDto>({ total: 0, pending: 0, reviewed: 0, interview: 0, accepted: 0, rejected: 0, cancelled: 0 });
  loading = signal(true);

  sankeyNodes = signal<any[]>([]);
  sankeyFlows = signal<any[]>([]);
  timelineData = signal<number[]>([]);

  ngOnInit() {
    this.loadStats();
  }

  async loadStats() {
    try {
      const res = await this.appService.getStatistics();
      if (res.success && res.data) {
        this.stats.set(res.data);
        this.buildSankey(res.data);
      }
    } catch { } finally {
      this.loading.set(false);
    }
  }

  private buildSankey(s: ApplicationStatisticsDto) {
    this.sankeyNodes.set([
      { id: 'applied', label: 'Applied', count: s.total, color: '#7F77DD', light: '#EEEDFE' },
      { id: 'screen', label: 'Screening', count: s.pending + s.reviewed, color: '#378ADD', light: '#E6F1FB' },
      { id: 'interview', label: 'Interview', count: s.interview, color: '#1D9E75', light: '#E1F5EE' },
      { id: 'offer', label: 'Offer', count: s.accepted, color: '#EF9F27', light: '#FAEEDA' },
      { id: 'accepted', label: 'Accepted', count: s.accepted, color: '#639922', light: '#EAF3DE' },
    ]);
    this.sankeyFlows.set([
      { from: 'applied', to: 'screen', val: s.pending + s.reviewed },
      { from: 'applied', to: 'rejected', val: s.rejected },
      { from: 'screen', to: 'interview', val: s.interview },
      { from: 'interview', to: 'offer', val: s.accepted },
    ]);
  }

  getConversion(a: number, b: number): string {
    if (a === 0) return '0%';
    return Math.round((b / a) * 100) + '%';
  }

  formatDate(d: Date): string {
    return d.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
  }

  goToApplications() {
    this.router.navigate(['/applications/list']);
  }

  get sankeySvgPath(): string {
    const nodes = this.sankeyNodes();
    const flows = this.sankeyFlows();
    if (!nodes.length || !flows.length) return '';
    const W = 540, H = 220, NW = 16;
    let svg = '';
    const nodeMap: Record<string, any> = {};
    nodes.forEach(n => { nodeMap[n.id] = { ...n, _usedH: 0, y: 20 }; });

    const totalH = 180;
    const maxCount = Math.max(...nodes.map(n => n.count));
    nodes.forEach(n => {
      n.h = Math.max(5, (n.count / maxCount) * totalH);
    });

    const totalScreenH = nodeMap['screen']?.h || 80;
    const totalAppliedH = nodeMap['applied']?.h || 160;

    const colorMap: Record<string, string> = {
      'applied': '#7F77DD', 'screen': '#378ADD', 'interview': '#1D9E75',
      'offer': '#EF9F27', 'accepted': '#639922', 'rejected': '#E24B4A',
      'noresponse': '#888780', 'declined': '#D85A30',
    };

    flows.forEach(f => {
      const fn = nodeMap[f.from], tn = nodeMap[f.to];
      if (!fn || !tn) return;
      const ratio = f.val / fn.count;
      const flowH = Math.max(3, fn.h * ratio);
      const x1 = 30 + NW;
      const x2 = f.to === 'rejected' ? 150 : f.from === 'interview' ? 270 : 150;
      const y1 = fn.y + (fn._usedH || 0) + flowH / 2;
      const y2 = f.to === 'rejected' ? 118 + flowH / 2 : f.to === 'offer' ? 20 + flowH / 2 : tn.y + (tn._usedH || 0) + flowH / 2;
      fn._usedH = (fn._usedH || 0) + flowH;
      if (f.to !== 'rejected') tn._usedH = (tn._usedH || 0) + flowH;
      const mx = (x1 + x2) / 2;
      const pct = Math.round((f.val / fn.count) * 100);
      const color = colorMap[f.from] || '#7F77DD';
      svg += `<path d="M${x1},${y1 - flowH / 2} C${mx},${y1 - flowH / 2} ${mx},${y2 - flowH / 2} ${x2},${y2 - flowH / 2} L${x2},${y2 + flowH / 2} C${mx},${y2 + flowH / 2} ${mx},${y1 + flowH / 2} ${x1},${y1 + flowH / 2} Z" fill="${color}" opacity="0.18" data-tip="${f.val} applicants · ${pct}%" style="cursor:pointer"/>`;
    });

    const xPositions: Record<string, number> = {
      applied: 30, screen: 150, interview: 270, offer: 390, accepted: 490, rejected: 150,
    };
    nodes.forEach(n => {
      const x = xPositions[n.id] || 30;
      svg += `<rect x="${x}" y="${n.y}" width="${NW}" height="${n.h}" rx="3" fill="${colorMap[n.id] || '#888'}"/>`;
      svg += `<text x="${x + NW + 6}" y="${n.y + Math.min(16, n.h / 2 + 6)}" font-size="11" fill="var(--color-text-primary)" font-weight="500">${n.label}</text>`;
      svg += `<text x="${x + NW + 6}" y="${n.y + Math.min(28, n.h / 2 + 18)}" font-size="10" fill="var(--color-text-tertiary)">${n.count}</text>`;
    });

    return svg;
  }

  get timelineSvgPath(): string {
    const TW = 620, TH = 80, pad = 10;
    const vals = [1, 0, 2, 3, 1, 4, 2, 5, 3, 2, 4, 1, 3, 5, 4, 3, 2, 4, 5, 3, 4, 2, 3, 1, 4, 5, 2, 3, 4, 5];
    const maxV = Math.max(...vals, 1);
    const pts = vals.map((v, i) => {
      const x = pad + (i / (vals.length - 1)) * (TW - 2 * pad);
      const y = TH - pad - (v / maxV) * (TH - 2 * pad);
      return [x, y];
    });

    let out = `<defs><linearGradient id="tlg" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stop-color="#7F77DD" stop-opacity="0.18"/><stop offset="100%" stop-color="#7F77DD" stop-opacity="0"/></linearGradient></defs>`;
    const area = `M${pts[0][0]},${TH} ` + pts.map(p => `L${p[0].toFixed(1)},${p[1].toFixed(1)}`).join(' ') + ` L${pts[pts.length - 1][0]},${TH} Z`;
    out += `<path d="${area}" fill="url(#tlg)"/>`;
    out += `<path d="M${pts.map(p => p[0].toFixed(1) + ',' + p[1].toFixed(1)).join(' L')}" fill="none" stroke="#7F77DD" stroke-width="1.5" stroke-linejoin="round"/>`;
    const today = new Date();
    const month = today.toLocaleDateString('en-US', { month: 'short' });
    out += `<text x="${pad}" y="${TH + 14}" font-size="10" fill="var(--color-text-tertiary)">1 ${month}</text>`;
    out += `<text x="${TW / 2}" y="${TH + 14}" font-size="10" fill="var(--color-text-tertiary)" text-anchor="middle">15 ${month}</text>`;
    out += `<text x="${TW - pad}" y="${TH + 14}" font-size="10" fill="var(--color-text-tertiary)" text-anchor="end">${today.getDate()} ${month}</text>`;
    return out;
  }
}
