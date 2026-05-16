import { Component, OnInit, OnDestroy, AfterViewInit } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-pipeline-section',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './pipeline.component.html',
  styleUrl: './pipeline.component.scss',
})
export class PipelineSectionComponent implements OnInit, AfterViewInit, OnDestroy {
  activePipelineStep = 0;
  private pipelineInterval: ReturnType<typeof setInterval> | null = null;

  ngOnInit(): void {
    this.pipelineInterval = setInterval(() => {
      this.activePipelineStep = (this.activePipelineStep + 1) % 5;
    }, 1300);
  }

  ngAfterViewInit(): void {
    const observer = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting) {
            entry.target.classList.add('visible');
            observer.unobserve(entry.target);
          }
        });
      },
      { threshold: 0.12 }
    );
    document.querySelectorAll('app-pipeline-section .reveal').forEach((el) => observer.observe(el));
  }

  ngOnDestroy(): void {
    if (this.pipelineInterval) clearInterval(this.pipelineInterval);
  }

  isPipelineStepActive(index: number): boolean {
    return this.activePipelineStep === index;
  }
}
