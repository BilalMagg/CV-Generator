import { Routes } from '@angular/router';
import { authGuard } from './guards/auth.guard';
import { HomeComponent } from './pages/home/home.component';
import { LoginComponent } from './pages/login/login.component';
// Register merged into LoginComponent — route redirects to /login?mode=sign-up

import { ApplicationsLayoutComponent } from './pages/applications/applications-layout.component';
import { DashboardComponent } from './pages/applications/dashboard/dashboard.component';
import { GenerateCvComponent } from './pages/applications/generate-cv/generate-cv.component';
import { EditCvComponent } from './pages/applications/generate-cv/edit-cv.component';
import { ApplicationsListComponent } from './pages/applications/list/applications-list.component';
import { KanbanComponent } from './pages/applications/kanban/kanban.component';
import { AnalyticsComponent } from './pages/applications/analytics/analytics.component';
import { CalendarComponent } from './pages/applications/calendar/calendar.component';
import { ResumesComponent } from './pages/applications/resumes/resumes.component';
import { ApplicationDetailComponent } from './pages/applications/detail/application-detail.component';
import { ApplicationCreateComponent } from './pages/applications/create/application-create.component';
import { SettingsLayoutComponent } from './pages/settings/settings-layout.component';
import { NotificationsComponent } from './pages/settings/notifications.component';
import { AboutComponent } from './pages/about/about.component';
import { ContactPageComponent } from './pages/contact/contact-page.component';
// Reminders removed as they are now in Calendar

import { MailboxComponent } from './pages/mailbox/mailbox.component';
import { MyCvComponent } from './pages/my-cv/my-cv.component';
import { AgentsHubComponent } from './pages/agents-hub/agents-hub.component';
import { AgentGuideComponent } from './pages/agents-hub/agent-guide/agent-guide.component';
import { JobExtractorWorkspaceComponent } from './pages/agents-hub/job-extractor-workspace/job-extractor-workspace.component';
import { SearchAgentWorkspaceComponent } from './pages/agents-hub/search-agent-workspace/search-agent-workspace.component';
import { SearchAgentResultComponent } from './pages/agents-hub/search-agent-result/search-agent-result.component';
import { ExtractionResultComponent } from './pages/agents-hub/extraction-result/extraction-result.component';
import { AgentConfigComponent } from './pages/agents-hub/agent-config/agent-config.component';
import { JobCrawlerWorkspaceComponent } from './pages/agents-hub/job-crawler-workspace/job-crawler-workspace.component';
import { JobCrawlerResultComponent } from './pages/agents-hub/job-crawler-result/job-crawler-result.component';
import { JobOffersComponent } from './pages/job-offers/job-offers.component';
import { PersonalInfoComponent } from './pages/personal-info/personal-info.component';
import { EntityDetailsComponent } from './pages/entity-details/entity-details.component';
import { EntityListComponent } from './pages/entity-list/entity-list.component';
import { EntityFormComponent } from './shared/entity-form/entity-form.component';

export const routes: Routes = [
  { path: '', component: HomeComponent },
  { path: 'login', component: LoginComponent },
  { path: 'register', redirectTo: '/login?mode=sign-up', pathMatch: 'full' },
  { path: 'about', component: AboutComponent },
  { path: 'contact', component: ContactPageComponent },
  { path: 'profile', component: PersonalInfoComponent, canActivate: [authGuard] },
  {
    path: 'applications',
    component: ApplicationsLayoutComponent,
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      { path: 'dashboard', component: DashboardComponent },
      { path: 'generate', component: GenerateCvComponent },
      { path: 'generate/edit/:runId', component: EditCvComponent },
      { path: 'list', component: ApplicationsListComponent },
      { path: 'kanban', component: KanbanComponent },
      { path: 'analytics', component: AnalyticsComponent },
      { path: 'calendar', component: CalendarComponent },
      { path: 'resumes', component: ResumesComponent },
    ],
  },
  { path: 'applications/new', component: ApplicationCreateComponent, canActivate: [authGuard] },
  { path: 'applications/:id', component: ApplicationDetailComponent, canActivate: [authGuard] },
  {
    path: 'settings',
    component: SettingsLayoutComponent,
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'notifications', pathMatch: 'full' },
      { path: 'notifications', component: NotificationsComponent },
    ],
  },  { path: 'agents-hub', component: AgentsHubComponent, canActivate: [authGuard] },
  { path: 'agents-hub/guide/:id', component: AgentGuideComponent, canActivate: [authGuard] },
  { path: 'agents-hub/job-extractor', component: JobExtractorWorkspaceComponent, canActivate: [authGuard] },
  { path: 'agents-hub/search-agent', component: SearchAgentWorkspaceComponent, canActivate: [authGuard] },
  { path: 'agents-hub/search-agent/result', component: SearchAgentResultComponent, canActivate: [authGuard] },
  { path: 'agents-hub/job-extractor/result/:id', component: ExtractionResultComponent, canActivate: [authGuard] },
  { path: 'agents-hub/job-extractor/config', component: AgentConfigComponent, canActivate: [authGuard] },
  { path: 'agents-hub/job-crawler', component: JobCrawlerWorkspaceComponent, canActivate: [authGuard] },
  { path: 'agents-hub/job-crawler/result/:id', component: JobCrawlerResultComponent, canActivate: [authGuard] },
  { path: 'job-offers', component: JobOffersComponent, canActivate: [authGuard] },
  {
    path: 'my-career',
    component: MyCvComponent,
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'cvprofiles', pathMatch: 'full' },
      { path: ':entity', component: EntityListComponent },
      { path: ':entity/add', component: EntityFormComponent },
      { path: ':entity/:id', component: EntityDetailsComponent },
      { path: ':entity/:id/edit', component: EntityFormComponent },
    ],
  },
  { path: 'mailbox', component: MailboxComponent, canActivate: [authGuard] },
  { path: '**', redirectTo: '' },
];
