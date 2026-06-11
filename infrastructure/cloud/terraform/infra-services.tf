# infra-services.tf — Kafka and Keycloak. Written directly (not via the service
# module) because their task defs differ enough from the app pattern.
#
# Both join the same Service Connect namespace, so they resolve as
# kafka:9092 and keycloak:8080 inside the cluster.

# ─────────────────────────────────────────────────────────────────────────────
# Kafka (KRaft, single node, EPHEMERAL storage — a restart loses in-flight
# events by deliberate demo choice; topics auto-create).
# ─────────────────────────────────────────────────────────────────────────────
resource "aws_cloudwatch_log_group" "kafka" {
  name              = "/ecs/kafka"
  retention_in_days = 7
}

resource "aws_ecs_task_definition" "kafka" {
  family                   = "kafka"
  requires_compatibilities = ["EC2"]
  network_mode             = "awsvpc"
  cpu                      = 512
  memory                   = 1024
  execution_role_arn       = aws_iam_role.task_execution.arn
  task_role_arn            = aws_iam_role.task.arn

  container_definitions = jsonencode([
    {
      name      = "kafka"
      image     = "confluentinc/cp-kafka:latest" # pulled from public registry via NAT
      essential = true
      portMappings = [
        { name = "kafka", containerPort = 9092, protocol = "tcp" },
        { containerPort = 9093, protocol = "tcp" } # controller, intra-task only
      ]
      environment = [
        { name = "KAFKA_NODE_ID", value = "1" },
        { name = "KAFKA_PROCESS_ROLES", value = "broker,controller" },
        # Single-node controller talks to itself; use localhost (port 9093 is not
        # registered in Service Connect, so the kafka: alias only covers 9092).
        { name = "KAFKA_CONTROLLER_QUORUM_VOTERS", value = "1@localhost:9093" },
        { name = "KAFKA_CONTROLLER_LISTENER_NAMES", value = "CONTROLLER" },
        { name = "KAFKA_LISTENER_SECURITY_PROTOCOL_MAP", value = "PLAINTEXT:PLAINTEXT,CONTROLLER:PLAINTEXT" },
        # Advertise the Service Connect DNS name so clients reach the broker.
        { name = "KAFKA_ADVERTISED_LISTENERS", value = "PLAINTEXT://kafka:9092" },
        { name = "KAFKA_LISTENERS", value = "PLAINTEXT://0.0.0.0:9092,CONTROLLER://0.0.0.0:9093" },
        { name = "KAFKA_INTER_BROKER_LISTENER_NAME", value = "PLAINTEXT" },
        { name = "KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR", value = "1" },
        { name = "KAFKA_AUTO_CREATE_TOPICS_ENABLE", value = "true" },
        { name = "CLUSTER_ID", value = "MkU3OEVBNTcwNTJENDM2Qk" },
        { name = "KAFKA_LOG_DIRS", value = "/var/lib/kafka/data" },
        { name = "KAFKA_HEAP_OPTS", value = "-Xmx512m -Xms256m" }
      ]
      logConfiguration = {
        logDriver = "awslogs"
        options = {
          "awslogs-group"         = aws_cloudwatch_log_group.kafka.name
          "awslogs-region"        = var.region
          "awslogs-stream-prefix" = "kafka"
        }
      }
    }
  ])
}

resource "aws_ecs_service" "kafka" {
  name            = "kafka"
  cluster         = aws_ecs_cluster.main.arn
  task_definition = aws_ecs_task_definition.kafka.arn
  desired_count   = 1

  # Pin to the EC2 capacity provider explicitly (avoids drift-driven replacement).
  capacity_provider_strategy {
    capacity_provider = aws_ecs_capacity_provider.main.name
    weight            = 1
    base              = 1
  }

  network_configuration {
    subnets          = local.private_subnet_ids
    security_groups  = [aws_security_group.ecs_tasks.id]
    assign_public_ip = false
  }

  service_connect_configuration {
    enabled   = true
    namespace = aws_service_discovery_private_dns_namespace.main.arn
    service {
      port_name      = "kafka"
      discovery_name = "kafka"
      client_alias {
        dns_name = "kafka"
        port     = 9092
      }
    }
  }

  lifecycle { ignore_changes = [desired_count] }
}

# ─────────────────────────────────────────────────────────────────────────────
# Keycloak (custom image w/ realm baked in; state in RDS `keycloak` DB).
# Prod `start --optimized --import-realm`; KC_HOSTNAME pinned to the ALB DNS
# (via the /cvgen/ext overlay) to fix the "iss claim mismatch" behind the ALB.
# ─────────────────────────────────────────────────────────────────────────────
resource "aws_cloudwatch_log_group" "keycloak" {
  name              = "/ecs/keycloak"
  retention_in_days = 7
}

resource "aws_ecs_task_definition" "keycloak" {
  family                   = "keycloak"
  requires_compatibilities = ["EC2"]
  network_mode             = "awsvpc"
  cpu                      = 512
  memory                   = 1024
  execution_role_arn       = aws_iam_role.task_execution.arn
  task_role_arn            = aws_iam_role.task.arn

  container_definitions = jsonencode([
    {
      name      = "keycloak"
      image     = "${local.ecr_repo_urls["keycloak"]}:${var.image_tag}"
      essential = true
      command   = ["start-dev", "--import-realm"]
      portMappings = [
        { name = "keycloak", containerPort = 8080, protocol = "tcp" }
      ]
      environment = [
        { name = "KC_DB", value = "postgres" },
        { name = "KC_DB_URL_HOST", value = aws_db_instance.main.address },
        { name = "KC_DB_URL_DATABASE", value = "keycloak" },
        { name = "KC_DB_USERNAME", value = var.db_username },
        { name = "KC_HEALTH_ENABLED", value = "true" },
        { name = "KC_HTTP_ENABLED", value = "true" }, # TLS terminates at the ALB
        { name = "KC_PROXY", value = "edge" },
        { name = "KC_HOSTNAME_STRICT", value = "false" },
        { name = "KEYCLOAK_ADMIN", value = "admin" }
      ]
      secrets = [
        { name = "KC_DB_PASSWORD", valueFrom = local.secret_arn["postgres-password"] },
        { name = "KEYCLOAK_ADMIN_PASSWORD", valueFrom = local.secret_arn["keycloak-admin-password"] },
        # KC_HOSTNAME must be the public ALB URL — written by Ansible to the overlay.
        { name = "KC_HOSTNAME", valueFrom = local.ext_arn["kc-hostname"] },
        # Available for realm-import substitution of the social IdPs.
        { name = "GOOGLE_CLIENT_ID", valueFrom = local.secret_arn["google-client-id"] },
        { name = "GOOGLE_CLIENT_SECRET", valueFrom = local.secret_arn["google-client-secret"] },
        { name = "GITHUB_CLIENT_ID", valueFrom = local.secret_arn["github-client-id"] },
        { name = "GITHUB_CLIENT_SECRET", valueFrom = local.secret_arn["github-client-secret"] }
      ]
      logConfiguration = {
        logDriver = "awslogs"
        options = {
          "awslogs-group"         = aws_cloudwatch_log_group.keycloak.name
          "awslogs-region"        = var.region
          "awslogs-stream-prefix" = "keycloak"
        }
      }
    }
  ])
}

resource "aws_ecs_service" "keycloak" {
  name            = "keycloak"
  cluster         = aws_ecs_cluster.main.arn
  task_definition = aws_ecs_task_definition.keycloak.arn
  desired_count   = 1

  # Pin to the EC2 capacity provider explicitly (avoids drift-driven replacement).
  capacity_provider_strategy {
    capacity_provider = aws_ecs_capacity_provider.main.name
    weight            = 1
    base              = 1
  }

  network_configuration {
    subnets          = local.private_subnet_ids
    security_groups  = [aws_security_group.ecs_tasks.id]
    assign_public_ip = false
  }

  service_connect_configuration {
    enabled   = true
    namespace = aws_service_discovery_private_dns_namespace.main.arn
    service {
      port_name      = "keycloak"
      discovery_name = "keycloak"
      client_alias {
        dns_name = "keycloak"
        port     = 8080
      }
    }
  }

  # Exposed via the ALB at /realms/* etc. so browser OIDC login + IdP callbacks
  # work (see alb.tf keycloak rule / target group).
  load_balancer {
    target_group_arn = aws_lb_target_group.keycloak.arn
    container_name   = "keycloak"
    container_port   = 8080
  }

  health_check_grace_period_seconds = 180 # Keycloak boot + realm import is slow

  # Needs the `keycloak` database to exist (Ansible role: database) and RDS up.
  depends_on = [aws_db_instance.main, aws_lb_listener.http]

  lifecycle { ignore_changes = [desired_count] }
}
