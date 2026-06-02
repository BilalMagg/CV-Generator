import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NavbarComponent } from '@app/shared/components/navbar/navbar.component';
import { FooterComponent } from '@app/shared/components/footer/footer.component';
import { HeroSectionComponent } from './sections/hero/hero.component';
import { LogosSectionComponent } from './sections/logos/logos.component';
import { FeaturesSectionComponent } from './sections/features/features.component';
import { PipelineSectionComponent } from './sections/pipeline/pipeline.component';
import { HowItWorksSectionComponent } from './sections/how-it-works/how-it-works.component';
import { StatsSectionComponent } from './sections/stats/stats.component';
import { TestimonialsSectionComponent } from './sections/testimonials/testimonials.component';
import { ArcCtaSectionComponent } from './sections/arc-cta/arc-cta.component';
import { CtaBannerSectionComponent } from './sections/cta-banner/cta-banner.component';
import { ContactSectionComponent } from './sections/contact/contact.component';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [
    CommonModule,
    NavbarComponent,
    FooterComponent,
    HeroSectionComponent,
    LogosSectionComponent,
    FeaturesSectionComponent,
    PipelineSectionComponent,
    HowItWorksSectionComponent,
    StatsSectionComponent,
    TestimonialsSectionComponent,
    ArcCtaSectionComponent,
    CtaBannerSectionComponent,
    ContactSectionComponent,
  ],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss',
})
export class HomeComponent {}
