import {
  Component,
  ElementRef,
  ViewChild,
  AfterViewInit,
  OnDestroy,
  HostListener,
  NgZone,
} from '@angular/core';

interface Dot {
  x: number;
  y: number;
  baseX: number;
  baseY: number;
}

@Component({
  selector: 'app-dot-field',
  standalone: true,
  template: `<canvas #canvas></canvas>`,
  styleUrl: './dot-field.component.scss',
})
export class DotFieldComponent implements AfterViewInit, OnDestroy {
  @ViewChild('canvas') canvasRef!: ElementRef<HTMLCanvasElement>;

  private dots: Dot[] = [];
  private mouseX = -9999;
  private mouseY = -9999;
  private rafId = 0;
  private ctx!: CanvasRenderingContext2D;

  // Config matching screenshot
  private readonly DOT_RADIUS = 1;
  private readonly DOT_SPACING = 18;
  private readonly CURSOR_RADIUS = 150;
  private readonly BULGE_STRENGTH = 31;
  private readonly GLOW_RADIUS = 50;

  constructor(private ngZone: NgZone) {}

  ngAfterViewInit(): void {
    const canvas = this.canvasRef.nativeElement;
    this.ctx = canvas.getContext('2d')!;
    this.resize();
    this.ngZone.runOutsideAngular(() => this.loop());
  }

  ngOnDestroy(): void {
    cancelAnimationFrame(this.rafId);
  }

  @HostListener('window:resize')
  resize(): void {
    const canvas = this.canvasRef.nativeElement;
    const rect = canvas.parentElement!.getBoundingClientRect();
    canvas.width = rect.width;
    canvas.height = rect.height;
    this.buildGrid(rect.width, rect.height);
  }

  onMouseMove(e: MouseEvent): void {
    const rect = this.canvasRef.nativeElement.getBoundingClientRect();
    this.mouseX = e.clientX - rect.left;
    this.mouseY = e.clientY - rect.top;
  }

  onMouseLeave(): void {
    this.mouseX = -9999;
    this.mouseY = -9999;
  }

  private buildGrid(w: number, h: number): void {
    this.dots = [];
    const s = this.DOT_SPACING;
    // Center-align the grid
    const cols = Math.ceil(w / s);
    const rows = Math.ceil(h / s);
    const offsetX = (w - (cols - 1) * s) / 2;
    const offsetY = (h - (rows - 1) * s) / 2;
    for (let r = 0; r < rows; r++) {
      for (let c = 0; c < cols; c++) {
        const bx = offsetX + c * s;
        const by = offsetY + r * s;
        this.dots.push({ x: bx, y: by, baseX: bx, baseY: by });
      }
    }
  }

  private loop(): void {
    this.draw();
    this.rafId = requestAnimationFrame(() => this.loop());
  }

  private draw(): void {
    const canvas = this.canvasRef.nativeElement;
    const ctx = this.ctx;
    ctx.clearRect(0, 0, canvas.width, canvas.height);

    const mx = this.mouseX;
    const my = this.mouseY;
    const cr = this.CURSOR_RADIUS;
    const bs = this.BULGE_STRENGTH;
    const gr = this.GLOW_RADIUS;
    const dr = this.DOT_RADIUS;

    // Glow effect under cursor
    if (mx > -1000) {
      const grd = ctx.createRadialGradient(mx, my, 0, mx, my, gr);
      grd.addColorStop(0, 'oklch(0.6 0.16 250 / 0.12)');
      grd.addColorStop(1, 'oklch(0.6 0.16 250 / 0)');
      ctx.fillStyle = grd;
      ctx.beginPath();
      ctx.arc(mx, my, gr, 0, Math.PI * 2);
      ctx.fill();
    }

    for (const dot of this.dots) {
      const dx = dot.baseX - mx;
      const dy = dot.baseY - my;
      const dist = Math.sqrt(dx * dx + dy * dy);

      if (dist < cr && dist > 0) {
        const force = (1 - dist / cr) * bs;
        dot.x = dot.baseX + (dx / dist) * force;
        dot.y = dot.baseY + (dy / dist) * force;
      } else {
        dot.x += (dot.baseX - dot.x) * 0.15;
        dot.y += (dot.baseY - dot.y) * 0.15;
      }

      ctx.beginPath();
      ctx.arc(dot.x, dot.y, dr, 0, Math.PI * 2);
      ctx.fillStyle = 'oklch(0.82 0.005 80)';
      ctx.fill();
    }
  }
}
