import { Component, OnInit, OnDestroy, inject, signal, ElementRef, ViewChild, AfterViewInit } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { DomSanitizer, SafeResourceUrl, SafeHtml } from '@angular/platform-browser';
import loader from '@monaco-editor/loader';
import { RenderedCV } from '@app/services/template-agent.service';

@Component({
  selector: 'app-template-agent-result',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './template-agent-result.component.html',
  styleUrl: './template-agent-result.component.scss',
})
export class TemplateAgentResultComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('editorContainer') editorContainerRef!: ElementRef<HTMLDivElement>;

  private readonly router = inject(Router);
  private readonly sanitizer = inject(DomSanitizer);

  result: RenderedCV | null = null;
  targetRole = '';

  safePdfUrl = signal<SafeResourceUrl | null>(null);
  safeHtmlSrc = signal<SafeHtml | null>(null);
  copied = signal(false);
  editorReady = signal(false);

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  private monacoEditor: any = null;

  ngOnInit(): void {
    const state = history.state as { result?: RenderedCV; targetRole?: string };

    if (!state?.result) {
      this.router.navigate(['/agents-hub/template-agent']);
      return;
    }

    this.result = state.result;
    this.targetRole = state.targetRole ?? '';

    if (this.result.pdf_url) {
      this.safePdfUrl.set(this.sanitizer.bypassSecurityTrustResourceUrl(this.result.pdf_url));
    }
  }

  async ngAfterViewInit(): Promise<void> {
    if (!this.result || !this.editorContainerRef) return;

    const language = this.result.template_id?.includes('html') ? 'html' : 'latex';

    try {
      const monaco = await loader.init();
      this.monacoEditor = monaco.editor.create(this.editorContainerRef.nativeElement, {
        value: this.result.cv_code ?? '',
        language,
        theme: 'vs-dark',
        fontSize: 13,
        lineHeight: 20,
        minimap: { enabled: false },
        wordWrap: 'on',
        scrollBeyondLastLine: false,
        automaticLayout: true,
        padding: { top: 16, bottom: 16 },
        fontFamily: 'Geist Mono, Consolas, monospace',
        renderLineHighlight: 'all',
      });
      this.editorReady.set(true);
    } catch {
      this.editorReady.set(true);
    }
  }

  ngOnDestroy(): void {
    this.monacoEditor?.dispose();
  }

  get editorContent(): string {
    return this.monacoEditor?.getValue() ?? this.result?.cv_code ?? '';
  }

  async copyCode(): Promise<void> {
    await navigator.clipboard.writeText(this.editorContent);
    this.copied.set(true);
    setTimeout(() => this.copied.set(false), 2000);
  }

  get previewType(): 'pdf' | 'html' | 'none' {
    if (this.result?.pdf_url) return 'pdf';
    if (this.result?.cv_code) return 'html';
    return 'none';
  }

  get htmlSrcdoc(): string {
    return this.result?.cv_code ?? '';
  }
}
