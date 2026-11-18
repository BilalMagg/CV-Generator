# Propel Target Infrastructure Architecture Proposal

**Document:** Architecture Design & Implementation Strategy  
**Version:** 1.0  
**Author:** Senior Software Architect  
**Status:** Approved for Implementation  

---

## 1. Architectural Vision

Propel’s primary deployment objective is to achieve **complete infrastructure decoupling** while maintaining:
1. The **single application Docker image** containing the Angular frontend, .NET YARP gateway, .NET backend monolith, and Python FastAPI agent sidecar.
2. **Provider independence**:
   - **Object Storage**: Configurable between local MinIO and Cloudflare R2 (S3-compatible) with zero business logic changes.
   - **Authentication**: Configurable between Native Authentication (ASP.NET Core Identity / PasswordHasher + JWT/Cookie) and Keycloak (Hybrid support for zero-downtime migration).
   - **Email / Gmail**: Provider-abstracted (`IEmailProvider`), independent of Keycloak, supporting per-user OAuth credentials and secure AES encryption.
3. **Strict Database Safety**: Preserving all existing user data (specifically `bilalmagguilej5@gmail.com`) and retaining compatibility with existing PostgreSQL schemas and MinIO objects.

---

## 2. Storage Provider Abstraction (`IObjectStorage`)

### 2.1 Interface Definition
A new unified interface `IObjectStorage` will replace direct calls to `IMinioStorageService`.

```csharp
namespace CV_Generator.Services.Storage;

public interface IObjectStorage
{
    Task EnsureBucketAsync(string bucket, CancellationToken ct = default);
    Task<string> UploadAsync(string bucket, string objectKey, Stream stream, string contentType, long size, CancellationToken ct = default);
    Task<byte[]> GetObjectAsync(string bucket, string objectKey, CancellationToken ct = default);
    Task<Stream> OpenReadStreamAsync(string bucket, string objectKey, CancellationToken ct = default);
    Task DeleteAsync(string bucket, string objectKey, CancellationToken ct = default);
    Task<bool> ExistsAsync(string bucket, string objectKey, CancellationToken ct = default);
    Task<string> GetPresignedUrlAsync(string bucket, string objectKey, TimeSpan expiry, HttpVerb verb = HttpVerb.GET, CancellationToken ct = default);
}
```

### 2.2 Provider Implementations
1. **`MinioObjectStorage`**:
   - Uses `MinioClient` (from existing `Minio` SDK 6.0.3).
   - Implements `IObjectStorage`.
   - Supports local development and existing bucket layout (`cv-artifacts`, `cover-letter-artifacts`, `mail-attachments`, `profile-images`).
   - Maintains compatibility with existing MinIO public URLs.
2. **`CloudflareR2ObjectStorage`**:
   - Uses `AWSSDK.S3` (Amazon S3 compatible client).
   - Endpoint: `https://{AccountId}.r2.cloudflarestorage.com`.
   - Uses AWS S3 `AmazonS3Config` with `ForcePathStyle = true`.
   - Generates presigned URLs using `GetPreSignedURLAsync`.
   - Supports private storage with signed download/upload links and public CDN URLs when `PublicBaseUrl` is configured.

### 2.3 Configuration Structure
```json
{
  "Storage": {
    "Provider": "MinIO", // "MinIO" or "CloudflareR2"
    "MinIO": {
      "Endpoint": "localhost:9000",
      "AccessKey": "minioadmin",
      "SecretKey": "minioadmin",
      "Secure": false,
      "PublicBaseUrl": "http://localhost:9000"
    },
    "CloudflareR2": {
      "AccountId": "",
      "AccessKey": "",
      "SecretKey": "",
      "BucketName": "propel",
      "PublicBaseUrl": ""
    }
  }
}
```
Controlled via environment variable: `STORAGE_PROVIDER=MinIO` or `STORAGE_PROVIDER=CloudflareR2`.

### 2.4 Backward Compatibility & Transitional Bridge
To ensure that all existing controllers and background services continue compiling without breaking changes, `IMinioStorageService` will inherit from `IObjectStorage`, and both `IObjectStorage` and `IMinioStorageService` will resolve to the active provider implementation in DI.

---

## 3. Authentication Modernization: Strategy & Evaluation

### 3.1 Evaluation of Options

| Criterion | Option A: Hybrid (Keycloak + Native) | Option B: Pure Native Migration |
|---|---|---|
| **Independence from Keycloak** | Complete when `AUTH_PROVIDER=Native` is set. | Immediate and total. |
| **Existing User Continuity** | Existing Keycloak sessions continue working; users can migrate smoothly. | Keycloak passwords cannot be read (one-way hashed); all existing users must reset passwords immediately. |
| **Operational Simplicity** | Configurable switch: can run with Keycloak or without Keycloak. | Keycloak completely removed from code. |
| **Implementation Complexity** | Moderate: Gateway and backend accept both schemes or route based on config. | Lower in runtime, higher in migration disruption. |
| **Verdict** | **SELECTED (Hybrid with Native as Primary/Default)** | Rejected as disruptive to existing live data. |

### 3.2 Target Authentication Architecture
1. **Configuration**:
   - `AUTH_PROVIDER`: `Native` (default for standalone/cloud), `Keycloak`, or `Hybrid`.
2. **Gateway Responsibilities**:
   - When `AUTH_PROVIDER=Native` or `Hybrid`:
     - `POST /api/auth/login`: Authenticates email/password directly against `AppDbContext` using ASP.NET Core `IPasswordHasher<User>`.
     - `POST /api/auth/register`: Validates password, hashes with `IPasswordHasher<User>`, creates `User` row directly in database.
     - `POST /api/auth/reset-password`: Implements secure password reset with HMAC token or direct authenticated update.
     - `GET /api/auth/me`: Reads session cookie, returns current user info.
     - `GET /api/auth/logout`: Clears session cookie and redirects.
     - Sets session cookie `cv_session` and forwards `X-User-Id` and `Authorization: Bearer <jwt>` to downstream backend.
   - When `AUTH_PROVIDER=Keycloak`:
     - Retains existing Keycloak OpenID Connect and Admin API flow.
3. **Backend Responsibilities**:
   - Backend accepts `X-User-Id` (internal gateway header via `InternalServiceAuthHandler`) and/or validated JWT Bearer tokens.
   - `CurrentUserService` resolves internal `Guid UserId` seamlessly.
4. **Database Changes**:
   - Add columns to `users` table:
     - `PasswordHash` (nullable string, for native credentials)
     - `SecurityStamp` (string)
     - `EmailConfirmed` (bool, default true)
     - `PasswordResetToken` (nullable string)
     - `PasswordResetExpiresAt` (nullable DateTime)
   - `KeycloakId` becomes nullable (or defaulted to empty string for purely native users).

---

## 4. Gmail Integration Architecture

### 4.1 Email Provider Abstraction
Introduce `IEmailProvider` to decouple the application’s notification and application delivery flows from Google’s proprietary SDK:

```csharp
namespace CV_Generator.Services.Email;

public class EmailSendRequest
{
    public required string To { get; set; }
    public required string Subject { get; set; }
    public required string Body { get; set; }
    public string? CvPdfUrl { get; set; }
    public string? CvTitle { get; set; }
    public IReadOnlyList<EmailAttachmentDto>? Attachments { get; set; }
}

public class EmailSendResult
{
    public bool Success { get; set; }
    public string? MessageId { get; set; }
    public string? ThreadId { get; set; }
    public string? ErrorMessage { get; set; }
}

public interface IEmailProvider
{
    Task<EmailSendResult> SendEmailAsync(Guid userId, EmailSendRequest request, CancellationToken ct = default);
}
```

### 4.2 Security Hardening
1. **OAuth State Protection**:
   - In `GmailAuthService.GetOAuthUrl`: Generate an HMAC-SHA256 signature of `{userId}:{nonce}:{timestamp}` using `GMAIL_TOKEN_ENCRYPTION_KEY`.
   - In `GmailController.Callback`: Verify the signature before processing the callback, preventing CSRF account-linking attacks.
2. **Token Encryption**:
   - Access and refresh tokens remain encrypted at rest in `gmail_connections` using authenticated AES encryption (`IAesEncryptionService`).
3. **Scope Minimization**:
   - Scope requested is strictly `https://www.googleapis.com/auth/gmail.send` along with `email` for address discovery.

---

## 5. Deployment Profiles & Compose Optimization

### 5.1 Docker Compose Profiles
The root and `deploy/docker-compose.yml` configurations will define profiles:

1. **Default / Cloud Profile (`docker compose up`)**:
   - Runs `app` (unified image) + `postgres`.
   - Uses `STORAGE_PROVIDER=CloudflareR2` (or local MinIO if selected) and `AUTH_PROVIDER=Native`.
   - **Keycloak and Keycloak-DB containers are NOT started.**
2. **Local Full Profile (`docker compose --profile full up`)**:
   - Starts `app`, `postgres`, `minio`, and `keycloak`.
3. **Dev Data Services (`docker-compose.data.yml`)**:
   - Provides individual local services (Postgres, MinIO, Keycloak) for developers running backend/frontend on host OS.

---

## 6. Migration and Rollback Strategy

1. **Database Migration**:
   - A new EF Core migration (`AddNativeAuthAndStorageOptions`) will add `PasswordHash`, `SecurityStamp`, `EmailConfirmed`, and make `KeycloakId` optional.
   - Non-destructive: Existing user rows (including `bilalmagguilej5@gmail.com`) remain intact with existing IDs.
2. **Object Storage Migration Utility**:
   - An administrative CLI/service method `MigrateObjectsAsync(sourceBucket, targetBucket)` that reads objects from MinIO and writes them to Cloudflare R2 without deleting the originals.
3. **Rollback**:
   - Simply setting `AUTH_PROVIDER=Keycloak` and `STORAGE_PROVIDER=MinIO` restores 100% of the previous behavior with zero database schema reverts required.

---
*End of Architecture Proposal.*
