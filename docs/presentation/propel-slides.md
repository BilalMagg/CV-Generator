---
marp: true
theme: uncover
class:
  - lead
  - invert
paginate: true
style: |
  section {
    font-family: 'Segoe UI', system-ui, sans-serif;
  }
  h1 { font-size: 2.5em; }
  h2 { font-size: 1.8em; }
  h3 { font-size: 1.2em; }
  table { font-size: 0.65em; margin: 0 auto; }
  li { font-size: 0.75em; line-height: 1.6; }
  .cols { display: flex; gap: 2em; justify-content: center; }
  .cols ul { flex: 1; }
  .small { font-size: 0.55em; }
  code { font-size: 0.7em; background: #222; padding: 0.1em 0.4em; border-radius: 4px; }
---

# Propel

**AI-Powered CV Generator**

Microservices · Event-Driven · Intelligent Agents

---

## What is Propel?

<div class="cols">
<ul>
<li>AI-driven CV generation from job descriptions</li>
<li>Job application tracking with Kanban & analytics</li>
<li>Portfolio management (projects, skills, experience)</li>
<li>Live job offer aggregation via crawling</li>
<li>Email notifications & scheduled reminders</li>
<li>Keycloak SSO with Google/GitHub providers</li>
</ul>
</div>

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| **Frontend** | Angular 21, TypeScript, RxJS, Signals, SCSS |
| **Backend** | .NET 10, ASP.NET Core, EF Core, gRPC |
| **AI Agents** | Python 3.11, FastAPI, LangChain, RAG |
| **Database** | PostgreSQL 16 + pgvector (8 databases) |
| **Auth** | Keycloak 26.2, OpenID Connect, JWT |
| **Messaging** | Confluent Kafka (KRaft mode) |
| **Storage** | MinIO (S3-compatible) |
| **Container** | Docker, Docker Compose, Nginx |

---

## System Architecture

```
Frontend → Nginx → API Gateway (YARP)
                        │
        ┌───────────────┼───────────────┐
        │               │               │
  7 Backend Svc    6 AI Agents     Infrastructure
  (.NET 10)        (Python 3.11)   (Kafka, Keycloak,
                                    MinIO, PostgreSQL)
```

> Full diagram: `docs/diagrams/system-architecture.eraserdiagram`

---

## Backend Services (.NET 10)

<div class="cols">
<ul>
<li><strong>User Service</strong> — CRUD, Keycloak sync, gRPC</li>
<li><strong>User Content</strong> — Portfolio (projects, skills, experience, education, etc.)</li>
<li><strong>Workflow</strong> — CV generation orchestration</li>
<li><strong>Application</strong> — Job tracking, Kanban, analytics</li>
<li><strong>CV Service</strong> — CV creation, versioning, PDF/DOCX</li>
<li><strong>Job Offer</strong> — Live crawling, SignalR real-time</li>
<li><strong>Notification</strong> — Email, reminders, Hangfire</li>
</ul>
</div>

---

## AI Agent Pipeline

**Flow:** Submit JD → Extract → Match → Render → Optimize → Deliver

<div class="cols">
<ol>
<li><strong>Job Extractor</strong> — Parses JD → structured requirements</li>
<li><strong>Search Agent</strong> — RAG matching via pgvector</li>
<li><strong>Template Agent</strong> — Renders CV with Jinja2/LaTeX</li>
<li><strong>CV Optimizer</strong> — ATS compliance, keywords</li>
<li><strong>Contact Agent</strong> — Email delivery + cover letter</li>
<li><strong>Job Crawler</strong> — Automated job board crawling</li>
</ol>
</div>

> Full diagram: `docs/diagrams/cv-pipeline.eraserdiagram`

---

## Deployment — 8 VMs / 5 Team Members

| Owner | VM | Services |
|-------|----|----------|
| **Bilal** | VM1 | Frontend + API Gateway + Monitoring |
| **Bilal** | VM2 | User + Content + Application + Notification |
| **Bilal** | VM3 | Extractor + Contact + Template agents |

| Owner | VM | Services |
|-------|----|----------|
| **Somia** | VM1 | Workflow Service |
| **Somia** | VM2 | Optimizer + Search + Crawler agents |
| **Nourelhouda** | VM1 | Keycloak + Kafka + MinIO + DBs |
| **Mouhssine** | VM1 | CV + Job Offer services |
| **Salman** | VM1 | Prometheus + Grafana |

> Full diagram: `docs/diagrams/deployment.eraserdiagram`

---

## Use Cases

| Diagram | File |
|---------|------|
| Authentication | `docs/diagrams/use-cases/authentication.puml` |
| Profile Management | `docs/diagrams/use-cases/profile-management.puml` |
| CV Generation | `docs/diagrams/use-cases/cv-generation.puml` |
| Application Tracking | `docs/diagrams/use-cases/application-tracking.puml` |
| Jobs & Notifications | `docs/diagrams/use-cases/jobs-notifications.puml` |

All diagrams generated with **PlantUML**.

---

## Kafka Event Flow

```
API Gateway ──> user.registered ──> Notification Service
                (userId, email, username)

Job Offer Service ──> trigger-live-crawl ──> Job Crawler
                      (keywords, location)
```

> Full diagram: `docs/diagrams/event-flow.eraserdiagram`

---

## Key Features

- ✨ **AI CV Generation** — Tailored CV in 11 seconds
- 📊 **Kanban Dashboard** — Drag & drop application tracking
- 📈 **Analytics** — Response rates, trends, statistics
- 🔔 **Smart Reminders** — Follow-up scheduling
- 🔒 **SSO** — Keycloak with Google/GitHub
- 🔄 **Real-time** — Kafka events + SignalR updates
- 🐳 **Containerized** — Full Docker Compose deployment

---

## Thank You

**Propel** — AI-Powered CV Generator

[Diagrams](./diagrams/) · [Slides](./presentation/)

Questions?
