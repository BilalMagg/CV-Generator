# ssm.tf — SSM Parameter Store placeholders.
#
# Two classes of parameters are created here as PLACEHOLDERS so that ECS task
# definitions can reference their ARNs at apply time, BEFORE Ansible knows the
# real values:
#
#   /cvgen/secrets/*  — SecureString secrets (passwords, API keys, connection
#                       strings). Ansible roles `database` + `secrets_overlay`
#                       overwrite these with real values after RDS exists.
#
#   /cvgen/ext/*      — the external-host overlay. These MUST resolve to the
#                       public ALB DNS, which only exists after apply. Ansible
#                       (role: secrets_overlay) writes the real ALB DNS here.
#                       This is the fix for OAuth redirect / CORS / Keycloak
#                       "iss claim mismatch" behind the ALB.
#
# Every parameter uses `ignore_changes = [value]` so a later `terraform apply`
# does NOT revert the values Ansible wrote. Terraform owns the parameter's
# existence; Ansible owns its value.

locals {
  # Secret parameters (SecureString). Connection strings embed the DB password,
  # so the whole string is a secret.
  secret_param_names = [
    # per-service EF Core connection strings (Host=...;Database=...;Password=...)
    "conn-user-service",
    "conn-user-content-service",
    "conn-workflow-service",
    "conn-application-service",
    "conn-job-offer-service",
    "conn-notification-service",
    "conn-cv-service",
    # cv-optimizer uses a libpq DSN instead of an EF connection string
    "cvoptimizer-database-url",
    # shared DB master password (also consumed as keycloak KC_DB_PASSWORD)
    "postgres-password",
    # LLM provider keys (groq is required; the rest are optional fallbacks)
    "groq-api-key",
    "openai-api-key",
    "mistral-api-key",
    # search-agent optional web-search keys
    "tavily-api-key",
    "crawl4ai-api-key",
    # Keycloak
    "keycloak-client-secret",
    "keycloak-admin-password",
    # social IdPs (also consumed by notification-service for Gmail)
    "google-client-id",
    "google-client-secret",
    "github-client-id",
    "github-client-secret",
    # notification-service Gmail token encryption + SMTP
    "gmail-token-encryption-key",
    "smtp-username",
    "smtp-password",
    # MinIO credentials
    "minio-root-user",
    "minio-root-password",
  ]

  # External-host overlay (plain String — these are hostnames, not secrets).
  ext_param_names = [
    "gateway-host",
    "gateway-port",
    "frontend-host",
    "frontend-port",
    "keycloak-external-host",
    "keycloak-external-port",
    "kc-hostname",
    "gmail-callback-base-url",
  ]
}

resource "aws_ssm_parameter" "secret" {
  for_each = toset(local.secret_param_names)

  name  = "/${var.project}/secrets/${each.key}"
  type  = "SecureString"
  value = "PLACEHOLDER" # overwritten by Ansible; never the real value in TF

  lifecycle {
    ignore_changes = [value]
  }

  tags = { Name = "${var.project}-secret-${each.key}" }
}

resource "aws_ssm_parameter" "ext" {
  for_each = toset(local.ext_param_names)

  name  = "/${var.project}/ext/${each.key}"
  type  = "String"
  value = "PLACEHOLDER" # overwritten by Ansible with the real ALB DNS / ports

  lifecycle {
    ignore_changes = [value]
  }

  tags = { Name = "${var.project}-ext-${each.key}" }
}

locals {
  # Convenience accessors: env-var-name -> SSM parameter ARN, for task defs.
  secret_arn = { for k, p in aws_ssm_parameter.secret : k => p.arn }
  ext_arn    = { for k, p in aws_ssm_parameter.ext : k => p.arn }
}
