import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [RouterLink, CommonModule],
  template: `
    <div class="container">
      <header class="hero">
        <h1>CV Generator</h1>
        <p class="tagline">AI-powered CV creation and optimization</p>

        <div class="cta-group">
          <a routerLink="/login" class="btn btn-primary">Login</a>
          <a routerLink="/register" class="btn btn-secondary">Register</a>
        </div>
      </header>

      <section class="features">
        <div class="feature">
          <h3>AI-Powered</h3>
          <p>Let AI help you create and optimize your CV for maximum impact.</p>
        </div>
        <div class="feature">
          <h3>Vector Search</h3>
          <p>Smart matching to find the perfect skills and experiences.</p>
        </div>
        <div class="feature">
          <h3>Easy Integration</h3>
          <p>Connect with Keycloak for secure authentication.</p>
        </div>
      </section>
    </div>
  `,
  styles: [`
    .container {
      min-height: 100vh;
      display: flex;
      flex-direction: column;
    }
    .hero {
      flex: 1;
      display: flex;
      flex-direction: column;
      justify-content: center;
      align-items: center;
      text-align: center;
      padding: 4rem 2rem;
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      color: white;
    }
    .hero h1 {
      font-size: 3rem;
      margin: 0 0 1rem;
    }
    .tagline {
      font-size: 1.25rem;
      opacity: 0.9;
      margin-bottom: 2rem;
    }
    .cta-group {
      display: flex;
      gap: 1rem;
    }
    .btn {
      padding: 0.75rem 1.5rem;
      border-radius: 4px;
      text-decoration: none;
      font-weight: 500;
      transition: transform 0.2s, box-shadow 0.2s;
    }
    .btn:hover {
      transform: translateY(-2px);
      box-shadow: 0 4px 12px rgba(0,0,0,0.2);
    }
    .btn-primary {
      background: white;
      color: #667eea;
    }
    .btn-secondary {
      background: transparent;
      color: white;
      border: 2px solid white;
    }
    .features {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
      gap: 2rem;
      padding: 4rem 2rem;
      max-width: 1200px;
      margin: 0 auto;
    }
    .feature {
      text-align: center;
      padding: 1.5rem;
    }
    .feature h3 {
      margin: 0 0 0.5rem;
      color: #333;
    }
    .feature p {
      color: #666;
      margin: 0;
    }
  `]
})
export class HomeComponent {}
