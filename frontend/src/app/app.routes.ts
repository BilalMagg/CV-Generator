import { Routes } from '@angular/router';
import { authGuard } from './guards/auth.guard';
import { HomeComponent } from './pages/home/home.component';
import { LoginComponent } from './pages/login/login.component';
import { RegisterComponent } from './pages/register/register.component';
import { ExperienceComponent } from './pages/experience/experience.component';
import { PersonalInfoComponent } from './pages/personal-info/personal-info.component';
import { EducationComponent } from './pages/education/education.component';
import { SkillsComponent } from './pages/skills/skills.component';
import { ApplicationsLayoutComponent } from './pages/applications/applications-layout.component';
import { DashboardComponent } from './pages/applications/dashboard/dashboard.component';
import { ApplicationsListComponent } from './pages/applications/list/applications-list.component';
import { KanbanComponent } from './pages/applications/kanban/kanban.component';
import { AnalyticsComponent } from './pages/applications/analytics/analytics.component';
import { CalendarComponent } from './pages/applications/calendar/calendar.component';
import { ResumesComponent } from './pages/applications/resumes/resumes.component';
import { ApplicationDetailComponent } from './pages/applications/detail/application-detail.component';
import { ApplicationCreateComponent } from './pages/applications/create/application-create.component';
import { SettingsLayoutComponent } from './pages/settings/settings-layout.component';
import { NotificationsComponent } from './pages/settings/notifications.component';
// Reminders removed as they are now in Calendar

export const routes: Routes = [
  { path: '', component: HomeComponent },
  { path: 'login', component: LoginComponent },
  { path: 'register', component: RegisterComponent },
  { path: 'experience', component: ExperienceComponent, canActivate: [authGuard] },
  { path: 'personal-info', component: PersonalInfoComponent, canActivate: [authGuard] },
  { path: 'education', component: EducationComponent, canActivate: [authGuard] },
  { path: 'skills', component: SkillsComponent, canActivate: [authGuard] },
  {
    path: 'applications',
    component: ApplicationsLayoutComponent,
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      { path: 'dashboard', component: DashboardComponent },
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
  },
  { path: '**', redirectTo: '' },
];
