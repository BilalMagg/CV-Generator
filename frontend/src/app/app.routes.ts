import { Routes } from '@angular/router';
import { authGuard } from './guards/auth.guard';
import { HomeComponent } from './pages/home/home.component';
import { LoginComponent } from './pages/login/login.component';
import { RegisterComponent } from './pages/register/register.component';

import { ApplicationsLayoutComponent } from './pages/applications/applications-layout.component';
import { DashboardComponent } from './pages/applications/dashboard/dashboard.component';
import { ApplicationsListComponent } from './pages/applications/list/applications-list.component';
import { KanbanComponent } from './pages/applications/kanban/kanban.component';
import { AnalyticsComponent } from './pages/applications/analytics/analytics.component';
import { CalendarComponent } from './pages/applications/calendar/calendar.component';
import { ResumesComponent } from './pages/applications/resumes/resumes.component';
import { ApplicationDetailComponent } from './pages/applications/detail/application-detail.component';
import { ApplicationCreateComponent } from './pages/applications/create/application-create.component';

import { MyCvComponent } from './pages/my-cv/my-cv.component';
import { EntityDetailsComponent } from './pages/entity-details/entity-details.component';
import { EntityListComponent } from './pages/entity-list/entity-list.component';
import { EntityFormComponent } from './shared/entity-form/entity-form.component';

export const routes: Routes = [
  { path: '', component: HomeComponent },
  { path: 'login', component: LoginComponent },
  { path: 'register', component: RegisterComponent },
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
    path: 'my-cv',
    component: MyCvComponent,
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'cvprofile', pathMatch: 'full' },
      { path: ':entity', component: EntityListComponent },
      { path: ':entity/add', component: EntityFormComponent },
      { path: ':entity/:id', component: EntityDetailsComponent },
      { path: ':entity/:id/edit', component: EntityFormComponent },
    ],
  },
  { path: '**', redirectTo: '' }
];