import { Routes } from '@angular/router';
import { authGuard } from './guards/auth.guard';
import { HomeComponent } from './pages/home/home.component';
import { LoginComponent } from './pages/login/login.component';
import { RegisterComponent } from './pages/register/register.component';
import { PersonalInfoComponent } from './pages/personal-info/personal-info.component';
import { SkillsComponent } from './pages/skills/skills.component';
import { ApplicationsComponent } from './pages/applications/applications.component';
import { ApplicationDetailComponent } from './pages/application-detail/application-detail.component';
import { ApplicationCreateComponent } from './pages/application-create/application-create.component';
import {MyCvComponent} from './pages/my-cv/my-cv.component'
import {EntityDetailsComponent } from './pages/entity-details/entity-details.component'
import {EntityListComponent}   from './pages/entity-list/entity-list.component'
import {EntityFormComponent} from './shared/entity-form/entity-form.component'

export const routes: Routes = [
  { path: '', component: HomeComponent },
  { path: 'login', component: LoginComponent },
  { path: 'register', component: RegisterComponent },
  { path: 'personal-info', component: PersonalInfoComponent, canActivate: [authGuard] },
  { path: 'skills', component: SkillsComponent, canActivate: [authGuard] },
  { path: 'applications', component: ApplicationsComponent, canActivate: [authGuard] },
  { path: 'applications/new', component: ApplicationCreateComponent, canActivate: [authGuard] },
  { path: 'applications/:id', component: ApplicationDetailComponent, canActivate: [authGuard] },
  { path: 'my-cv', component:MyCvComponent,  canActivate: [authGuard],
    children: [
            {
            path: 'entity',
            component: EntityListComponent
            },
            {
            path: ':entity/:id',
            component: EntityDetailsComponent
            },
            {
            path: 'my-cv/:entity/add',
            component: EntityFormComponent
            },
             {
            path: 'my-cv/:entity/:id/edit',
            component: EntityFormComponent
            }

   ]},
  { path: '**', redirectTo: '' }
];
 