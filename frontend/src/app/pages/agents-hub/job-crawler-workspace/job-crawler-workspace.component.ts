import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-job-crawler-workspace',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './job-crawler-workspace.component.html',
  styleUrl: './job-crawler-workspace.component.scss'
})
export class JobCrawlerWorkspaceComponent { }
