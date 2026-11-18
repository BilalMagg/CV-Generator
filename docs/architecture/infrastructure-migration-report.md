# Propel — Infrastructure Decoupling, Storage Abstraction & Authentication Modernization Report

## 1. Executive Summary

This report documents the architectural modernization and infrastructure decoupling executed on the **Propel** platform (formerly CV_Generator).

### Objectives Achieved:
1. **Object Storage Abstraction**: Replaced direct MinIO SDK coupling across the backend with a provider-agnostic `IObjectStorage` abstraction. Implemented native support for **MinIO** and **Cloudflare R2** (via S3-compatible API using `AWSSDK.S3`), selected dynamically via configuration (`STORAGE_PROVIDER=MinIO` or `CloudflareR2`). Created a safe, non-destructive `StorageMigrationService` to sync assets between providers.
2. **Authentication Modernization**: Removed hard dependency on Keycloak by introducing native ASP.NET Core Identity password hashing (`IPasswordHasher<User>`), native registration, authentication, and password reset endpoints in Backend and Gateway. Preserved 100% backward compatibility for existing Keycloak accounts via `AUTH_PROVIDER=Native`, `Keycloak`, or `Hybrid`.
3. **Decoupled Per-User Gmail Integration**: Decoupled Google OAuth from Keycloak entirely. Introduced `IEmailProvider` and `GmailEmailProvider`. Implemented HMAC-SHA256 signed, time-limited (15-minute) CSRF state tokens for Google OAuth callbacks.
4. **Preserved Single Propel Image & Simplified Compose**: Maintained the unified single container image (`deploy/Dockerfile`). Updated Docker Compose files to support Compose profiles (`minio`, `keycloak`, `full`), allowing Propel to run in minimal cloud mode (PostgreSQL + App image only) or full local self-hosted mode.
5. **Strict DB & Data Safety**: All existing records and files for user `bilalmagguilej5@gmail.com` and all MinIO data buckets remain 100% intact and untouched.

---

## 2. Architecture Comparison: Before vs. After

| Capability | Previous Implementation | Modernized Implementation |
|---|---|---|
| **Object Storage API** | Direct `MinioClient` SDK in all controllers & services | Provider-agnostic `IObjectStorage` abstraction |
| **Supported Storage Providers** | MinIO only (local container / host) | MinIO and Cloudflare R2 (S3-compatible API) |
| **Storage Selection** | Hardcoded MinIO initialization | Config-driven (`STORAGE_PROVIDER=MinIO` / `CloudflareR2`) |
| **File URLs** | Fixed `http://localhost:9000/...` MinIO URLs in DB | Uniform relative keys & provider-aware presigned / streaming URLs |
| **Authentication Flow** | Keycloak mandatory; gateway stalled on Keycloak retries | Native password hashing + Keycloak hybrid; no stall when native |
| **User Entity** | Mandatory `KeycloakId` | Nullable `KeycloakId` + `PasswordHash`, `SecurityStamp`, `EmailConfirmed` |
| **Gmail OAuth State** | Simple unauthenticated userId passed in state | Cryptographically signed HMAC-SHA256 state with 15-minute expiration |
| **Email Delivery** | Direct `GmailSendService` only | Provider-agnostic `IEmailProvider` with `GmailEmailProvider` |
| **Compose Infrastructure** | Mandatory MinIO, Keycloak, Keycloak-DB containers | Optional infrastructure via Docker Compose profiles (`COMPOSE_PROFILES`) |
| **Deployment Footprint** | 4-5 mandatory containers | 2 containers (PostgreSQL + App) in minimal cloud mode |

---

## 3. Files Created and Modified

### 3.1 Object Storage Abstraction
- `monolith/backend/Services/Storage/IObjectStorage.cs` (Created): Clean abstraction for bucket management, stream uploads/downloads, existence checks, presigned URLs, object listing, and deletion.
- `monolith/backend/Services/Storage/StorageOptions.cs` (Created): Strongly-typed options for MinIO and Cloudflare R2 with validation.
- `monolith/backend/Services/Storage/MinioObjectStorage.cs` (Created): MinIO implementation using `MinioClient` and `ListObjectsEnumAsync`.
- `monolith/backend/Services/Storage/CloudflareR2ObjectStorage.cs` (Created): Cloudflare R2 implementation using S3-compatible `AmazonS3Client`.
- `monolith/backend/Services/Storage/StorageMigrationService.cs` (Created): Administrative migration tool to copy objects between storage providers without deleting source objects.
- `monolith/backend/Services/MinioStorageService.cs` (Modified): Maintained as backward-compatible wrapper implementing `IMinioStorageService`.
- Controllers and services migrated to `IObjectStorage`:
  - `CvsController.cs`
  - `CvVersionsController.cs`
  - `CoverLettersController.cs`
  - `CoverLetterVersionsController.cs`
  - `ImagesController.cs`
  - `MailboxController.cs`
  - `ApplicationsController.cs`
  - `EmailDraftsController.cs`
  - `ApplyService.cs`
  - `EmailDraftService.cs`
  - `EmailScheduleService.cs`
  - `TemplateRenderService.cs`

### 3.2 Authentication Modernization
- `monolith/backend/Models/User.cs` (Modified): Made `KeycloakId` nullable; added `PasswordHash`, `SecurityStamp`, `EmailConfirmed`, `PasswordResetToken`, `PasswordResetExpiresAt`.
- `monolith/backend/Dto/UserDtos.cs` (Modified): Added `AuthenticateRequestDto`, `RegisterNativeUserDto`, `ResetPasswordNativeDto`.
- `monolith/backend/Controllers/UsersController.cs` (Modified): Added `/api/users/authenticate`, `/api/users/register-native`, `/api/users/reset-password-native`.
- `monolith/backend/Migrations/20261118105053_AddNativeAuthSupport.cs` (Created): EF Core migration adding native auth columns and making `keycloak_id` nullable.
- `monolith/gateway/Program.cs` (Modified): Updated auth routing; bypasses Keycloak retry loop when `AUTH_PROVIDER=Native`; issues and validates `cv_session` cookie natively.

### 3.3 Decoupled Gmail Integration
- `monolith/backend/Services/Email/IEmailProvider.cs` (Created): Provider-agnostic contract for email delivery.
- `monolith/backend/Services/Email/GmailEmailProvider.cs` (Created): Implementation wrapping Google Gmail API.
- `monolith/backend/Services/IGmailAuthService.cs` & `GmailAuthService.cs` (Modified): Added HMAC-SHA256 signed OAuth state token generation and verification with 15-minute expiry.
- `monolith/backend/Controllers/GmailController.cs` (Modified): Enforced state signature verification before processing Google callback.

### 3.4 Deployment & Compose Simplification
- `deploy/docker-compose.yml` (Modified): Added Docker Compose profiles (`minio`, `keycloak`, `full`), environment variable overrides for `STORAGE_PROVIDER`, `AUTH_PROVIDER`, and Cloudflare R2 options.
- `deploy/.env.example` (Modified): Documented profiles and configuration variables.
- `deploy/docker-compose.data.yml` (Modified): Added profile support for dev services.
- `deploy/docker-compose.local.yml` (Modified): Added provider options while preserving local dev volume mounts.

### 3.5 Unit & Integration Tests
- `monolith/tests/CV_Generator.Tests/CV_Generator.Tests.csproj` (Created)
- `monolith/tests/CV_Generator.Tests/Storage/StorageTests.cs` (Created)
- `monolith/tests/CV_Generator.Tests/Auth/NativeAuthTests.cs` (Created)
- `monolith/tests/CV_Generator.Tests/Gmail/GmailAuthServiceTests.cs` (Created)
- `monolith/tests/CV_Generator.Tests/Email/EmailProviderTests.cs` (Created)

---

## 4. Verification and Validation Results

Unit tests cover:
- Storage options validation for MinIO and Cloudflare R2.
- Storage migration between providers without source object deletion.
- Native password hashing and verification using `IPasswordHasher<User>`.
- Decoupled user creation and password reset token expiration.
- HMAC-SHA256 OAuth signed state generation and tamper detection.
- Expiration check on OAuth states exceeding 15 minutes.
- Email provider delivery contract and error isolation.

---

## 5. Rollback and Disaster Recovery Plan

If any issue arises:
1. **Database Rollback**:
   To revert the migration: `dotnet ef database update 20261019_FixCompanyGeoLocation --project monolith/backend/CV_Generator.csproj`.
   Existing `KeycloakId` values are unchanged; making the column non-nullable again requires ensuring all native users have an identifier.
2. **Storage Rollback**:
   Set `STORAGE_PROVIDER=MinIO`. Since `StorageMigrationService` never deletes source files from MinIO, all existing files remain available in MinIO.
3. **Auth Rollback**:
   Set `AUTH_PROVIDER=Keycloak` in `.env`. Keycloak continues to operate normally with its realm database.
