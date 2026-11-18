# Propel Infrastructure Audit & Technical Discovery Report

**Date:** 2026-11-18  
**Scope:** Propel Monolith Backend, YARP Gateway, Frontend (Angular 21), AI Agent Sidecar, Docker Deployment  
**Auditor:** Senior Software Architect & Full-Stack Systems Engineer  

---

## 1. Executive Summary

Propel is an AI-powered CV generation and job application management platform. The platform is architected as:
- An **Angular 21** frontend (standalone components, signals).
- A **.NET 10 YARP API Gateway** acting as reverse proxy and authentication orchestrator.
- A **.NET 10 Monolithic Backend** holding core business logic, application tracking, CV document models, and integrations.
- A **Python 3.12 FastAPI Sidecar** hosting 7 AI agents (BIME, CV optimizer, extract, crawler, search, etc.).
- External infrastructure: **PostgreSQL (pgvector)**, **MinIO** (object storage), **Keycloak** (identity management & OpenID Connect provider), and **Gmail API** (email delivery).

Currently, all four application components (frontend, gateway, backend, agents) are containerized into a single Docker image (`deploy/Dockerfile`). However, running Propel currently mandates running sidecar infrastructure containers: Keycloak (and its dedicated PostgreSQL database) and MinIO.

This audit details the current implementation across **Object Storage**, **Authentication & Authorization**, **Gmail Integration**, and **Container Deployment**, identifying exact dependencies, code paths, security concerns, and migration requirements.

---

## 2. Existing Architecture Overview

```
                                    ┌────────────────────────────────────────────────────────┐
                                    │                     Docker Image                       │
                                    │                                                        │
┌──────────────┐     HTTP :4200     │  ┌──────────────┐    HTTP :8080    ┌────────────────┐  │
│  User / SPA  │ ─────────────────> │  │ Nginx (SPA)  │ ───────────────> │  YARP Gateway  │  │
└──────────────┘                    │  └──────────────┘                  └───────┬────────┘  │
                                    │                                            │           │
                                    │               ┌────────────────────────────┴─────┐     │
                                    │               │ Bearer JWT + X-User-Id header    │     │
                                    │               v                                  v     │
                                    │     ┌──────────────────┐               ┌────────────┐  │
                                    │     │  .NET 10 Backend │ <-----------> │  AI Agents │  │
                                    │     │    (:5000)       │  HTTP :8000   │  (:8000)   │  │
                                    │     └─────────┬────────┘               └────────────┘  │
                                    └───────────────┼────────────────────────────────────────┘
                                                    │
                   ┌────────────────────────────────┼───────────────────────────────┐
                   v                                v                               v
          ┌────────────────┐               ┌────────────────┐              ┌────────────────┐
          │   PostgreSQL   │               │     MinIO      │              │    Keycloak    │
          │  cv_monolith   │               │    (:9000)     │              │    (:9090)     │
          │    (:5432)     │               └────────────────┘              └───────┬────────┘
          └────────────────┘                                                       │
                                                                           ┌───────┴────────┐
                                                                           │  Keycloak DB   │
                                                                           │  postgres:17   │
                                                                           └────────────────┘
```

### Component Breakdown
1. **Frontend (`frontend/`)**:
   - Angular 21, standalone components, signals.
   - HTTP interceptor / base service: `HttpService` (`frontend/src/app/services/http.service.ts`) communicates with `/api/*` on same origin.
   - Auth Service: `frontend/src/app/services/auth.service.ts` coordinates user state via `/api/auth/me`, `/api/auth/login`, `/api/auth/register`, `/api/auth/reset-password`, `/api/auth/logout`.
2. **API Gateway (`monolith/gateway/`)**:
   - .NET 10 ASP.NET Core with `Yarp.ReverseProxy`.
   - Listens on `:8080`.
   - Manages cookie authentication (`cv_session` cookie with in-memory distributed cache `MemoryCacheTicketStore`).
   - Owns `/api/auth/*` routes and proxies all other routes (`/api/users/*`, `/api/cv/*`, `/api/applications/*`, `/api/mailbox/*`, etc.) to backend `:5000`.
   - Forwarding transform (`monolith/gateway/Program.cs#L561-L585`): Attaches `Authorization: Bearer <access_token>` and `X-User-Id: <internalUserId>`.
3. **Backend Monolith (`monolith/backend/`)**:
   - .NET 10 Web API on `:5000`.
   - EF Core 10 with Npgsql accessing `cv_monolith`.
   - Dual authentication: `JwtBearer` (pointing to `JWT_AUTHORITY`) and `InternalServiceAuthHandler` (trusting `X-User-Id` from gateway).
   - `CurrentUserService` resolves internal `Guid UserId` from `X-User-Id`, `user_id` claim, or `sub` claim matched with `users.KeycloakId` or `users.Email`.
4. **AI Agents Sidecar (`monolith/agents/`)**:
   - Python 3.12 FastAPI on `:8000`.
   - Communicates with backend over HTTP; does NOT access MinIO or database directly.
5. **Supervisor Process Manager (`deploy/supervisord.conf`)**:
   - Runs in the unified image to manage Nginx, Gateway, Backend, and Agents in one container.

---

## 3. Storage Audit (MinIO vs. Cloudflare R2)

### 3.1 Current Initialization and Implementation
- **Registration**: `monolith/backend/Program.cs#L160`:
  `builder.Services.AddSingleton<IMinioStorageService, MinioStorageService>();`
- **Implementation**: `monolith/backend/Services/MinioStorageService.cs`:
  Directly instantiates `MinioClient` from the `Minio` NuGet package (v6.0.3).
  ```csharp
  _client = new MinioClient()
      .WithEndpoint(host, port)
      .WithCredentials(accessKey, secretKey)
      .WithSSL(secure)
      .Build();
  ```
- **Configuration Keys**:
  - `MINIO_ENDPOINT` or `Minio:Endpoint` (default: `localhost:9000`)
  - `MINIO_ROOT_USER` or `Minio:AccessKey` (default: `minioadmin`)
  - `MINIO_ROOT_PASSWORD` or `Minio:SecretKey` (default: `minioadmin`)
  - `MINIO_SECURE` or `Minio:Secure` (default: `false`)

### 3.2 Storage Interface Analysis
The existing interface `IMinioStorageService` defines only 4 operations:
```csharp
public interface IMinioStorageService
{
    Task EnsureBucketAsync(string bucket, CancellationToken ct = default);
    Task<string> UploadAsync(string bucket, string objectKey, Stream stream, string contentType, long size, CancellationToken ct = default);
    Task<byte[]> GetObjectAsync(string bucket, string objectKey, CancellationToken ct = default);
    Task DeleteAsync(string bucket, string objectKey, CancellationToken ct = default);
}
```

### 3.3 Active Buckets and Consumers
| Bucket Name | Purpose | Consumers | Object Key Format |
|---|---|---|---|
| `cv-artifacts` | Uploaded and generated CV PDFs, DOCX, TeX sources, and PNG thumbnails | `CvsController`, `CvVersionsController`, `TemplateRenderService` | `{userId}/{cvId}/{guid}.{ext}` |
| `cover-letter-artifacts` | Cover letter PDFs and thumbnails | `CoverLettersController`, `CoverLetterVersionsController` | `{userId}/{coverLetterId}/{guid}.{ext}` |
| `mail-attachments` | Email attachments for schedules, drafts, and applications | `MailboxController`, `EmailDraftsController`, `ApplicationsController`, `ApplyService`, `EmailDraftService`, `EmailScheduleService` | `{userId}/{guid}.{ext}` |
| `profile-images` | User profile and CV photos | `ImagesController` | `{userId}/{imageId}/{guid}.{ext}` |

### 3.4 Storage Flow and File Reference Persistence
- **Upload**: Consumer passes stream to `UploadAsync`. MinIO implementation uploads the object, calls `SetPolicyAsync` applying a public read policy (`PublicReadPolicy`), and returns `{publicBaseUrl}/{bucket}/{objectKey}`.
- **Database Persistence**:
  - `CvVersion`: Stores `FileUrl`, `PdfUrl`, `ThumbnailUrl` (full URL string).
  - `CoverLetterVersion`: Stores `FileUrl`, `PdfUrl`, `ThumbnailUrl` (full URL string).
  - `UserImage`: Stores `ObjectKey` (`{userId}/{imageId}/{guid}.ext`) AND `Url`.
  - `User`: Stores `ProfilePhotoKey` (object key).
  - `ApplicationAttempt`, `EmailDraft`, `EmailSchedule`: Stores JSON array of `ScheduleAttachmentRef` containing `ObjectKey`, `FileName`, `ContentType`, `SizeBytes`.
- **Retrieval**:
  - `CvVersionsController.GetFile` and `CoverLetterVersionsController.GetFile` implement `TryResolveObject(url, out bucket, out key)` which splits `uri.AbsolutePath.TrimStart('/')` into bucket and object key.
  - The controller streams bytes via same-origin endpoint `GET /api/cv/versions/{id}/file` using `_storage.GetObjectAsync(bucket, key)` because browser PDF viewers cannot render cross-origin MinIO URLs without CORS issues.
- **Python Agent Storage Access**:
  - Verified: **Python agent does NOT access MinIO or object storage directly**.
  - All file and PDF transfers occur via HTTP payloads between the backend and sidecar.

---

## 4. Authentication Audit (Keycloak vs. Native)

### 4.1 Current Keycloak Configuration
- **Gateway Configuration**:
  - `monolith/gateway/Program.cs`:
    - Reads `KEYCLOAK_HOST` (default: `localhost`), `KEYCLOAK_PORT` (`9090`), `KEYCLOAK_EXTERNAL_HOST`, `KEYCLOAK_EXTERNAL_PORT`.
    - Internal authority: `http://{keycloakHost}:{keycloakPort}/realms/{keycloakRealm}` (`cv-realm`).
    - External login URL: `http://{keycloakExtHost}:{keycloakExtPort}/realms/{keycloakRealm}/protocol/openid-connect/auth`.
    - Client: `cv-gateway` (secret `change-me-in-production`).
    - Realm definition file: `deploy/keycloak-realm/cv-realm-realm.json`.
- **Automatic Keycloak Bootstrap**:
  - In `monolith/gateway/Program.cs#L788-L974`, `ConfigureKeycloakAsync` polls the Keycloak Admin API with `admin-cli` credentials (`admin`/`admin`), ensures client redirect URIs for `cv-gateway` and `cv-frontend`, and configures identity providers (Google, GitHub) if environment variables are provided.

### 4.2 Current Authentication Flows
1. **Credentials Login (`POST /api/auth/login`)**:
   - Gateway sends Direct Access Grant (`grant_type=password`) to Keycloak token endpoint.
   - Extracts `access_token` and `id_token`.
   - Calls backend `POST /api/users/sync` with `Authorization: Bearer <access_token>`.
   - Creates `ClaimsPrincipal` with claims (`sub`, `email`, `given_name`, `family_name`, `user_id`, `access_token`).
   - Issues `cv_session` cookie stored in `MemoryCacheTicketStore`.
2. **SSO Login (`GET /api/auth/login`)**:
   - Gateway generates random `state` and `nonce`, sets temporary cookies `oidc_state` and `oidc_nonce`.
   - Redirects browser to Keycloak OpenID Connect authorization endpoint (`kc_idp_hint` added if provider specified).
   - Keycloak redirects back to `GET /api/auth/callback`.
   - Gateway validates state against `oidc_state` cookie, exchanges authorization code for tokens, synchronizes user with backend, creates session, and redirects to frontend.
3. **Session Check (`GET /api/auth/me`)**:
   - Gateway reads `cv_session` cookie, returns user details, role, and tokens.
4. **Registration (`POST /api/auth/register`)**:
   - Gateway gets admin token from Keycloak (`admin-cli`).
   - Posts new user to Keycloak Admin REST API `/admin/realms/{realm}/users`.
   - Assigns `user` realm role.
   - Calls backend `POST /api/users` to create internal user record.
5. **Password Reset (`POST /api/auth/reset-password`)**:
   - Gateway uses Keycloak Admin API to search for user by email.
   - Resets password directly via Keycloak Admin API `PUT /admin/realms/{realm}/users/{id}/reset-password`.
6. **Backend Request Authentication**:
   - Gateway forwards `Authorization: Bearer <access_token>` and `X-User-Id: <internalUserId>`.
   - Backend `InternalServiceAuthHandler` accepts `X-User-Id` if valid Guid.
   - Backend `CurrentUserService` resolves `UserId` from `X-User-Id` or `sub` claim.

### 4.3 Database Model for Users
- Table `users`:
  - `Id`: `Guid` (Primary Key).
  - `KeycloakId`: `string(50)` (Required).
  - `Email`: `string(100)` (Unique index).
  - `FirstName`: `string(50)`.
  - `LastName`: `string(50)`.
  - `Role`: `Role` enum (`USER`, `ADMIN`).
  - Notice: **There is currently NO `PasswordHash` column on the `User` entity**. All passwords currently reside inside Keycloak.

---

## 5. Gmail Integration Audit

### 5.1 Architecture & Flow
- **Controller**: `monolith/backend/Controllers/GmailController.cs` (`api/gmail/*`).
- **Services**:
  - `GmailAuthService.cs`: Generates OAuth URL, handles code exchange, stores encrypted tokens.
  - `GmailTokenRefresher.cs`: Refreshes expired access tokens using the refresh token.
  - `GmailSendService.cs`: Uses `Google.Apis.Gmail.v1` and `MimeKit` to compose and deliver emails.
  - `AesEncryptionService.cs`: Encrypts/decrypts OAuth tokens using AES-256-GCM / CBC with key `GMAIL_TOKEN_ENCRYPTION_KEY`.
- **Database Model**:
  - Table `gmail_connections` (`GmailConnection` entity):
    - `Id`: `Guid`
    - `UserId`: `Guid`
    - `GmailAddress`: `string`
    - `EncryptedAccessToken`: `string`
    - `EncryptedRefreshToken`: `string`
    - `TokenExpiresAt`: `DateTime`
    - `ConnectedAt`: `DateTime`
    - `IsRevoked`: `bool`
    - `NeedsReauth`: `bool`

### 5.2 Keycloak Decoupling Status
- **Current Relationship**:
  - **The Gmail OAuth flow itself is already independent of Keycloak!** It communicates directly with Google's OAuth endpoints (`accounts.google.com/o/oauth2/v2/auth` and `oauth2.googleapis.com/token`).
  - Tokens are encrypted and stored directly in PostgreSQL (`gmail_connections`), **not** in Keycloak.
  - However:
    1. Keycloak was being configured in `monolith/gateway/Program.cs` to reuse the same `GOOGLE_CLIENT_ID` / `GOOGLE_CLIENT_SECRET` for user login.
    2. In `GmailAuthService.cs`, scopes requested included identity scopes (`openid`, `email`, `profile`) mixed with `https://www.googleapis.com/auth/gmail.send`.
    3. The application lacks an email delivery abstraction (`IEmailProvider`) to allow alternative email dispatchers (e.g., SMTP or Microsoft Graph).
    4. State parameter in `GetOAuthUrl` passes raw `userId` without HMAC signature or nonce verification, exposing a potential CSRF vector if an attacker supplies their own authorization code.

---

## 6. Identified Security Concerns

1. **OAuth CSRF in Gmail Integration**:
   - `GmailController.cs` and `GmailAuthService.cs` pass `state = userId` or `state = userId|returnUrl` without an HMAC signature or nonce. An attacker could theoretically induce a victim to bind the attacker's Google account or vice versa.
2. **Keycloak Admin Hardcoded Defaults**:
   - Gateway defaults `KEYCLOAK_ADMIN_USERNAME` and `KEYCLOAK_ADMIN_PASSWORD` to `admin`/`admin`.
3. **In-Memory Session Storage in Gateway**:
   - Gateway uses `MemoryCacheTicketStore` backed by `IDistributedCache` (in-memory). Any restart of the gateway container terminates all active user sessions.
4. **Public Read Bucket Policies in MinIO**:
   - `MinioStorageService.EnsureBucketAsync` automatically applies a wide-open `s3:GetObject` public policy to all buckets (`cv-artifacts`, `cover-letter-artifacts`, `mail-attachments`). Any user could guess or brute-force other users' CV or cover letter URLs if exposed publicly.
5. **Direct Password Reset Without Verification**:
   - `POST /api/auth/reset-password` accepts an email and new password and updates it immediately via Keycloak Admin API without email verification or token challenge.

---

## 7. Migration Risks & Constraints

1. **User Data Safety Rule**:
   - Account `bilalmagguilej5@gmail.com` contains real user production data in `cv_monolith` and MinIO. **No data modification or deletion is permitted** for this user.
   - Any database migrations must be additive and non-destructive.
2. **MinIO Object Key Compatibility**:
   - Existing records in PostgreSQL reference MinIO URLs (`http://localhost:9000/cv-artifacts/...`).
   - The storage abstraction must transparently handle existing MinIO URLs and object keys when migrating to Cloudflare R2.
3. **Single Application Image Requirement**:
   - The final artifact must remain a single Propel application image containing frontend, gateway, backend, and agent sidecar.
   - When running with `STORAGE_PROVIDER=CloudflareR2` and `AUTH_PROVIDER=Native`, the stack must boot cleanly without MinIO or Keycloak containers running.

---

## 8. Summary of Components Requiring Modification

| Component | Files | Modification Required |
|---|---|---|
| **Storage Abstraction** | `monolith/backend/Services/MinioStorageService.cs`, `monolith/backend/CV_Generator.csproj`, all controllers/services referencing `IMinioStorageService` | Introduce `IObjectStorage`, implement `MinioObjectStorage` and `CloudflareR2ObjectStorage` (AWSSDK.S3), create factory/DI registration based on `STORAGE_PROVIDER`. |
| **Authentication Modernization** | `monolith/gateway/Program.cs`, `monolith/gateway/appsettings.json`, `monolith/backend/Program.cs`, `monolith/backend/Models/User.cs`, migrations | Introduce hybrid/native authentication in Gateway & Backend. Add `PasswordHash` and auth fields to `User`. Support both Keycloak and Native Identity. |
| **Gmail Decoupling** | `monolith/backend/Services/GmailAuthService.cs`, `GmailSendService.cs`, `GmailController.cs` | Introduce `IEmailProvider` abstraction. Add HMAC-signed OAuth state. Ensure strict decoupling from authentication. |
| **Deployment & Compose** | `deploy/docker-compose.yml`, `deploy/docker-compose.data.yml`, `deploy/docker-compose.local.yml`, `deploy/.env.example`, `deploy/README.md` | Configure profiles (`local` vs `cloud`), allow running without MinIO and without Keycloak. |

---
*End of Audit Report.*
