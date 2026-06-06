---
marp: true
theme: uncover
class: invert
paginate: true
style: |
  section { font-family: 'Segoe UI', system-ui, sans-serif; }
  h1 { font-size: 2.2em; }
  h2 { font-size: 1.6em; margin-bottom: 0.3em; }
  p, li { font-size: 0.7em; line-height: 1.6; }
  table { font-size: 0.6em; margin: 0 auto; }
  code { font-size: 0.7em; background: #222; padding: 0.1em 0.3em; border-radius: 4px; }
  .cols { display: flex; gap: 2em; justify-content: center; }
  .cols ul { flex: 1; }
---

# Section 3
## Architecture

Microservices · Event-Driven · gRPC · REST

---

## Global System Architecture

![width:900px](../diagrams/system-architecture.eraserdiagram)

*Frontend → Nginx → API Gateway → 7 Backend Services + 6 AI Agents + Infrastructure*

> **Note:** Export from eraser.io as PNG to display here

---

## Microservices Breakdown

<div class="cols">
<div>

**User Service** `:8082`
- User CRUD, Keycloak sync
- gRPC server on :50001

**User Content** `:8083`
- Portfolio: projects, skills, experience, education, certifications
- 10 entity types

**Application** `:8085`
- Job tracking, Kanban, analytics
- gRPC client → User Service

**CV Service** `:8088`
- CV creation, versioning
- PDF/DOCX export

</div>
<div>

**Workflow** `:8084`
- CV generation orchestration
- Calls AI agents sequentially

**Job Offer** `:8086`
- Live job crawling
- SignalR real-time hub

**Notification** `:8087`
- Email via MailKit
- Hangfire scheduler
- Gmail integration

</div>
</div>

---

## Communication Patterns

| Pattern | Protocol | Usage |
|---------|----------|-------|
| **REST** | HTTP/1.1 | API Gateway → Backend services |
| **gRPC** | HTTP/2 | User Service ↔ Application Service |
| **Events** | Kafka | async: registration, crawl triggers |
| **Real-time** | SignalR | Job Offer → Browser notifications |

```
Browser               API Gateway              Backend
   │                       │                       │
   │─── REST /api/* ───────┤                       │
   │                       │─── REST ──────────────┤
   │                       │─── gRPC ──────────────┤
   │                       │─── Kafka publish ─────┤
   │◄── SignalR push ──────┤                       │
```

---

## API Gateway — YARP Reverse Proxy

- Single entry point: **`http://localhost:8080`**
- Routes `/api/*` to backend services
- Handles authentication (Keycloak OIDC)
- Publishes `user.registered` Kafka events

| Route | Target | Service |
|-------|--------|---------|
| `/api/users/*` | `user-service:8082` | User Service |
| `/api/user-content/*` | `user-content-service:8083` | User Content |
| `/api/workflows/*` | `workflow-service:8084` | Workflow |
| `/api/applications/*` | `application-service:8085` | Application |
| `/api/job-offers/*` | `job-offer-service:8086` | Job Offer |
| `/api/notifications/*` | `notification-service:8087` | Notification |
| `/api/cv/*` | `cv-service:8088` | CV Service |

---

## Database per Service

8 PostgreSQL 16 databases, each with pgvector enabled:

![width:800px](../diagrams/database-erd.eraserdiagram)

*One database per microservice — no shared databases*

> **Note:** Export from eraser.io as PNG to display here

---

## gRPC Inter-Service Communication

```
┌──────────────────┐         gRPC          ┌─────────────────────┐
│   User Service   │ ◄─────────────────── │ Application Service │
│   :50001         │   GetUserById()      │   (gRPC client)     │
└──────────────────┘                      └─────────────────────┘
```

- **Proto definitions:** `backend/src/common-protos/`
- **Services:** `UserServiceGrpc` — GetUserById, GetUserByEmail, CreateUser, etc.
- **ContentServiceGrpc** — GetProjectById/ByUserId (streaming)
- Multiplexed HTTP/2 on same port as REST (user-content, workflow, cv-service)

---

## Event-Driven Architecture (Kafka)

![width:800px](../diagrams/event-flow.eraserdiagram)

*Kafka topics with publishers and consumers*

> **Note:** Export from eraser.io as PNG to display here

---

## Event Flow Details

```
┌──────────────┐     user.registered     ┌─────────────────────┐
│ API Gateway  │ ──────────────────────► │ Notification Service│
│ (publisher)  │  { userId, email,       │ (consumer)          │
│              │    username }           │ → send welcome email│
└──────────────┘                         └─────────────────────┘

┌──────────────┐     trigger-live-crawl  ┌─────────────────────┐
│Job Offer Svc │ ──────────────────────► │   Job Crawler       │
│ (publisher)  │  { keywords, location } │ (consumer)          │
│              │                         │ → crawl job boards  │
└──────────────┘                         └─────────────────────┘
```

| Topic | Publisher | Consumer | Action |
|-------|-----------|----------|--------|
| `user.registered` | API Gateway | Notification Svc | Welcome email |
| `user.created` | User Service | — | Future use |
| `trigger-live-crawl` | Job Offer Svc | Job Crawler | Crawl boards |

---

## Authentication Flow (Keycloak OIDC)

![width:700px](../diagrams/auth-flow.eraserdiagram)

*OIDC sequence: Browser → Gateway → Keycloak → User Service*

> **Note:** Export from eraser.io as PNG to display here

---

## Auth Flow in 7 Steps

```
1. Browser → GET /api/auth/login
2. Gateway → 302 redirect to Keycloak
3. Browser → Authenticate (username/password or Google/GitHub)
4. Keycloak → Auth code redirect back
5. Gateway → POST /api/auth/callback?code=
6. Gateway → Keycloak: exchange code for tokens
7. Gateway → User Service: sync user → set cv_session cookie
```

- **Cookie name:** `cv_session`
- **Downstream trust:** `X-User-Id` header forwarded by Gateway
- **Registration:** Creates user in Keycloak + emits `user.registered`

---

## Section Summary

- ✅ **7 microservices** communicating via REST, gRPC, Kafka
- ✅ **API Gateway** (YARP) as single entry point with auth
- ✅ **Database per service** — 8 PostgreSQL instances
- ✅ **Event-driven** with Kafka for async workflows
- ✅ **Keycloak SSO** with OIDC and cookie sessions
- ✅ **Real-time** via SignalR for job offer updates
