# services.tf — the 15 application services (frontend, gateway, 7 .NET, 6 agents).
#
# Inventory confirmed against the codebase (ports, DBs, env var NAMES). Internal
# service-to-service URLs use Service Connect DNS names (the service's own name).
# external_host vars and all secrets are pulled from SSM via the task def
# `secrets` block (valueFrom) — note String SSM params (the /cvgen/ext/* overlay)
# are also injected this way so Ansible can set them to the real ALB DNS.

locals {
  kafka_bootstrap  = "kafka:9092"
  jwt_authority    = "http://keycloak:8080/realms/cv-realm"
  gateway_internal = "http://api-gateway:8080"

  app_services = {

    # ── Frontend (Angular/nginx) ─ ALB default target ──────────────────────
    frontend = {
      cpu    = 256, memory = 512, rest_port = 80, grpc_port = null
      alb_tg = aws_lb_target_group.frontend.arn, alb_port = 80
      environment = {
        GATEWAY_HOST  = "api-gateway"
        GATEWAY_PORT  = "8080"
        FRONTEND_PORT = "80"
      }
      secrets = {
        FRONTEND_HOST = local.ext_arn["frontend-host"] # external_host
      }
    }

    # ── API Gateway (YARP) ─ ALB /api/* target ─────────────────────────────
    "api-gateway" = {
      cpu    = 512, memory = 1024, rest_port = 8080, grpc_port = null
      alb_tg = aws_lb_target_group.api_gateway.arn, alb_port = 8080
      environment = {
        KEYCLOAK_HOST             = "keycloak"
        KEYCLOAK_PORT             = "8080"
        KEYCLOAK_REALM            = "cv-realm"
        KEYCLOAK_CLIENT_ID        = "cv-gateway"
        KEYCLOAK_ADMIN_USERNAME   = "admin"
        USER_SERVICE_HOST         = "user-service"
        USER_SERVICE_PORT         = "8082"
        CONTENT_SERVICE_HOST      = "user-content-service"
        CONTENT_SERVICE_PORT      = "8083"
        WORKFLOW_SERVICE_HOST     = "workflow-service"
        WORKFLOW_SERVICE_PORT     = "8084"
        APPLICATION_SERVICE_HOST  = "application-service"
        APPLICATION_SERVICE_PORT  = "8085"
        JOB_OFFER_SERVICE_HOST    = "job-offer-service"
        JOB_OFFER_SERVICE_PORT    = "8086"
        NOTIFICATION_SERVICE_HOST = "notification-service"
        NOTIFICATION_SERVICE_PORT = "8087"
        CV_SERVICE_HOST           = "cv-service"
        CV_SERVICE_PORT           = "8088"
        KAFKA_HOST                = "kafka"
        KAFKA_PORT                = "9092"
        # Internal Kestrel bind port (GATEWAY_PORT is the public ALB port via the
        # /cvgen/ext overlay, so the container must bind PORT, not GATEWAY_PORT).
        PORT = "8080"
      }
      secrets = {
        # external_host overlay (ALB DNS / public ports) — set by Ansible
        GATEWAY_HOST           = local.ext_arn["gateway-host"]
        GATEWAY_PORT           = local.ext_arn["gateway-port"]
        FRONTEND_HOST          = local.ext_arn["frontend-host"]
        FRONTEND_PORT          = local.ext_arn["frontend-port"]
        KEYCLOAK_EXTERNAL_HOST = local.ext_arn["keycloak-external-host"]
        KEYCLOAK_EXTERNAL_PORT = local.ext_arn["keycloak-external-port"]
        # real secrets
        KEYCLOAK_CLIENT_SECRET  = local.secret_arn["keycloak-client-secret"]
        KEYCLOAK_ADMIN_PASSWORD = local.secret_arn["keycloak-admin-password"]
        GOOGLE_CLIENT_ID        = local.secret_arn["google-client-id"]
        GOOGLE_CLIENT_SECRET    = local.secret_arn["google-client-secret"]
        GITHUB_CLIENT_ID        = local.secret_arn["github-client-id"]
        GITHUB_CLIENT_SECRET    = local.secret_arn["github-client-secret"]
      }
    }

    # ── .NET microservices ─────────────────────────────────────────────────
    "user-service" = {
      cpu    = 256, memory = 512, rest_port = 8082, grpc_port = 18082
      alb_tg = null, alb_port = null
      environment = {
        ASPNETCORE_ENVIRONMENT  = "Production"
        PORT                    = "8082"
        GRPC_PORT               = "18082"
        JWT_AUTHORITY           = local.jwt_authority
        KAFKA_BOOTSTRAP_SERVERS = local.kafka_bootstrap
      }
      secrets = { ConnectionStrings__DefaultConnection = local.secret_arn["conn-user-service"] }
    }

    "user-content-service" = {
      cpu    = 256, memory = 512, rest_port = 8083, grpc_port = 18083
      alb_tg = null, alb_port = null
      environment = {
        ASPNETCORE_ENVIRONMENT  = "Production"
        PORT                    = "8083"
        GRPC_PORT               = "18083"
        Kafka__BootstrapServers = local.kafka_bootstrap
      }
      secrets = { ConnectionStrings__DefaultConnection = local.secret_arn["conn-user-content-service"] }
    }

    "workflow-service" = {
      cpu    = 256, memory = 512, rest_port = 8084, grpc_port = 18084
      alb_tg = null, alb_port = null
      environment = {
        ASPNETCORE_ENVIRONMENT = "Production"
        PORT                   = "8084"
        GRPC_PORT              = "18084"
        JOB_EXTRACTOR_URL      = "http://job-extractor:8001/api/v1/"
        SEARCH_AGENT_URL       = "http://search-agent:8002/api/v1/"
        TEMPLATE_AGENT_URL     = "http://template-agent:8003/api/v1/"
        CV_OPTIMIZER_URL       = "http://cv-optimizer:8004/api/v1/"
        CONTACT_AGENT_URL      = "http://contact-agent:8005/api/v1/"
      }
      secrets = { ConnectionStrings__DefaultConnection = local.secret_arn["conn-workflow-service"] }
    }

    "application-service" = {
      cpu    = 256, memory = 512, rest_port = 8085, grpc_port = 18085
      alb_tg = null, alb_port = null
      environment = {
        ASPNETCORE_ENVIRONMENT  = "Production"
        PORT                    = "8085"
        GRPC_PORT               = "18085"
        KAFKA_BOOTSTRAP_SERVERS = local.kafka_bootstrap
        JWT_AUTHORITY           = local.jwt_authority
        USER_SERVICE_GRPC_URL   = "http://user-service:18082"
      }
      secrets = { ConnectionStrings__DefaultConnection = local.secret_arn["conn-application-service"] }
    }

    "job-offer-service" = {
      cpu    = 256, memory = 512, rest_port = 8086, grpc_port = null
      alb_tg = null, alb_port = null
      environment = {
        ASPNETCORE_ENVIRONMENT = "Production"
        PORT                   = "8086"
      }
      secrets = { ConnectionStrings__DefaultConnection = local.secret_arn["conn-job-offer-service"] }
    }

    "notification-service" = {
      cpu    = 256, memory = 512, rest_port = 8087, grpc_port = null
      alb_tg = null, alb_port = null
      environment = {
        ASPNETCORE_ENVIRONMENT       = "Production"
        PORT                         = "8087"
        Kafka__BootstrapServers      = local.kafka_bootstrap
        USER_SERVICE_GRPC_URL        = "http://user-service:18082"
        APPLICATION_SERVICE_GRPC_URL = "http://application-service:18085"
        Smtp__Host                   = "smtp.gmail.com"
        Smtp__Port                   = "587"
      }
      secrets = {
        ConnectionStrings__DefaultConnection = local.secret_arn["conn-notification-service"]
        GOOGLE_CLIENT_ID                     = local.secret_arn["google-client-id"]
        GOOGLE_CLIENT_SECRET                 = local.secret_arn["google-client-secret"]
        GMAIL_TOKEN_ENCRYPTION_KEY           = local.secret_arn["gmail-token-encryption-key"]
        Smtp__Username                       = local.secret_arn["smtp-username"]
        Smtp__Password                       = local.secret_arn["smtp-password"]
        Gmail__CallbackBaseUrl               = local.ext_arn["gmail-callback-base-url"] # external_host
      }
    }

    "cv-service" = {
      cpu    = 256, memory = 512, rest_port = 8088, grpc_port = 18088
      alb_tg = null, alb_port = null
      environment = {
        ASPNETCORE_ENVIRONMENT  = "Production"
        PORT                    = "8088"
        GRPC_PORT               = "18088"
        JWT_AUTHORITY           = local.jwt_authority
        KAFKA_BOOTSTRAP_SERVERS = local.kafka_bootstrap
      }
      secrets = { ConnectionStrings__DefaultConnection = local.secret_arn["conn-cv-service"] }
    }

    # ── Python AI agents ───────────────────────────────────────────────────
    "job-extractor" = {
      cpu    = 256, memory = 512, rest_port = 8001, grpc_port = null
      alb_tg = null, alb_port = null
      environment = {
        SERVICE_NAME      = "job-extractor"
        JOB_EXTRACTOR_URL = "http://job-extractor:8001"
        BACKEND_BASE_URL  = local.gateway_internal
      }
      secrets = { GROQ_API_KEY = local.secret_arn["groq-api-key"] }
    }

    "search-agent" = {
      cpu    = 256, memory = 512, rest_port = 8002, grpc_port = null
      alb_tg = null, alb_port = null
      environment = {
        SERVICE_NAME     = "search-agent"
        SEARCH_AGENT_URL = "http://search-agent:8002"
        BACKEND_BASE_URL = local.gateway_internal
      }
      secrets = {
        GROQ_API_KEY     = local.secret_arn["groq-api-key"]
        TAVILY_API_KEY   = local.secret_arn["tavily-api-key"]
        CRAWL4AI_API_KEY = local.secret_arn["crawl4ai-api-key"]
      }
    }

    # template-agent — the MinIO writer. Var names match minio_storage.py (§4).
    "template-agent" = {
      cpu    = 256, memory = 512, rest_port = 8003, grpc_port = null
      alb_tg = null, alb_port = null
      environment = {
        SERVICE_NAME           = "template-agent"
        TEMPLATE_AGENT_URL     = "http://template-agent:8003"
        BACKEND_BASE_URL       = local.gateway_internal
        MINIO_ENDPOINT         = "minio:9000"
        MINIO_SECURE           = "false"
        MINIO_BUCKET           = "cv-pdfs"
        MINIO_TEMPLATES_BUCKET = "cv-templates"
      }
      secrets = {
        GROQ_API_KEY        = local.secret_arn["groq-api-key"]
        MINIO_ROOT_USER     = local.secret_arn["minio-root-user"]
        MINIO_ROOT_PASSWORD = local.secret_arn["minio-root-password"]
      }
    }

    # cv-optimizer — shares cv_db via a libpq DSN (DATABASE_URL), uses pgvector.
    "cv-optimizer" = {
      cpu    = 256, memory = 512, rest_port = 8004, grpc_port = null
      alb_tg = null, alb_port = null
      environment = {
        SERVICE_NAME     = "cv-optimizer"
        CV_OPTIMIZER_URL = "http://cv-optimizer:8004"
        BACKEND_BASE_URL = local.gateway_internal
      }
      secrets = {
        GROQ_API_KEY = local.secret_arn["groq-api-key"]
        DATABASE_URL = local.secret_arn["cvoptimizer-database-url"]
      }
    }

    "contact-agent" = {
      cpu    = 256, memory = 512, rest_port = 8005, grpc_port = null
      alb_tg = null, alb_port = null
      environment = {
        SERVICE_NAME      = "contact-agent"
        CONTACT_AGENT_URL = "http://contact-agent:8005"
        SMTP_SERVER       = "smtp.gmail.com"
        SMTP_PORT         = "587"
      }
      secrets = {
        GROQ_API_KEY  = local.secret_arn["groq-api-key"]
        SMTP_USERNAME = local.secret_arn["smtp-username"]
        SMTP_PASSWORD = local.secret_arn["smtp-password"]
      }
    }

    "job-crawler" = {
      cpu    = 256, memory = 512, rest_port = 8006, grpc_port = null
      alb_tg = null, alb_port = null
      environment = {
        SERVICE_NAME            = "job-crawler"
        JOB_CRAWLER_URL         = "http://job-crawler:8006"
        KAFKA_BOOTSTRAP_SERVERS = local.kafka_bootstrap
        CONSUME_TOPIC           = "trigger-live-crawl"
        PRODUCE_TOPIC           = "raw-job-urls"
        KAFKA_GROUP_ID          = "job-crawler-group"
        MAX_RESULTS_PER_SITE    = "10"
      }
      secrets = {}
    }
  }
}

module "service" {
  source   = "./modules/service"
  for_each = local.app_services

  name      = each.key
  image     = "${local.ecr_repo_urls[each.key]}:${var.image_tag}"
  cpu       = each.value.cpu
  memory    = each.value.memory
  rest_port = each.value.rest_port
  grpc_port = each.value.grpc_port

  environment = each.value.environment
  secrets     = each.value.secrets

  alb_target_group_arn = each.value.alb_tg
  alb_container_port   = each.value.alb_port

  cluster_arn                   = aws_ecs_cluster.main.arn
  execution_role_arn            = aws_iam_role.task_execution.arn
  task_role_arn                 = aws_iam_role.task.arn
  subnets                       = local.private_subnet_ids
  security_groups               = [aws_security_group.ecs_tasks.id]
  service_connect_namespace_arn = aws_service_discovery_private_dns_namespace.main.arn
  region                        = var.region

  depends_on = [aws_lb_listener.http]
}
