# modules/service/main.tf — CloudWatch log group + task definition + ECS
# service (awsvpc, Service Connect, optional ALB attachment) for one service.

locals {
  log_group = "/ecs/${var.name}"

  # Port mappings. The `name` is what Service Connect references as port_name.
  port_mappings = concat(
    [{
      name          = "${var.name}-rest"
      containerPort = var.rest_port
      protocol      = "tcp"
    }],
    var.grpc_port == null ? [] : [{
      name          = "${var.name}-grpc"
      containerPort = var.grpc_port
      protocol      = "tcp"
    }]
  )

  environment = [for k, v in var.environment : { name = k, value = v }]
  secrets     = [for k, v in var.secrets : { name = k, valueFrom = v }]

  # Service Connect: REST always; gRPC alias (same DNS name, different port)
  # only when grpc_port is set. Both resolve as http://<name>:<port>.
  sc_services = concat(
    [{
      port_name      = "${var.name}-rest"
      discovery_name = var.name
      alias_port     = var.rest_port
    }],
    var.grpc_port == null ? [] : [{
      port_name      = "${var.name}-grpc"
      discovery_name = "${var.name}-grpc"
      alias_port     = var.grpc_port
    }]
  )
}

resource "aws_cloudwatch_log_group" "this" {
  name              = local.log_group
  retention_in_days = var.log_retention_days
}

resource "aws_ecs_task_definition" "this" {
  family                   = var.name
  requires_compatibilities = ["EC2"]
  network_mode             = "awsvpc"
  cpu                      = var.cpu
  memory                   = var.memory
  execution_role_arn       = var.execution_role_arn
  task_role_arn            = var.task_role_arn

  container_definitions = jsonencode([
    {
      name         = var.name
      image        = var.image
      essential    = true
      portMappings = local.port_mappings
      environment  = local.environment
      secrets      = local.secrets
      logConfiguration = {
        logDriver = "awslogs"
        options = {
          "awslogs-group"         = local.log_group
          "awslogs-region"        = var.region
          "awslogs-stream-prefix" = var.name
        }
      }
    }
  ])
}

resource "aws_ecs_service" "this" {
  name            = var.name
  cluster         = var.cluster_arn
  task_definition = aws_ecs_task_definition.this.arn
  desired_count   = var.desired_count

  # Pin to the EC2 ASG capacity provider explicitly. This mirrors the cluster
  # default, but declaring it here prevents Terraform from reading the
  # ECS-recorded strategy as drift and force-replacing the service on every apply.
  capacity_provider_strategy {
    capacity_provider = var.capacity_provider_name
    weight            = 1
    base              = 1
  }

  network_configuration {
    subnets          = var.subnets
    security_groups  = var.security_groups
    assign_public_ip = false
  }

  service_connect_configuration {
    enabled   = true
    namespace = var.service_connect_namespace_arn

    dynamic "service" {
      for_each = local.sc_services
      content {
        port_name      = service.value.port_name
        discovery_name = service.value.discovery_name
        client_alias {
          dns_name = var.name
          port     = service.value.alias_port
        }
      }
    }
  }

  dynamic "load_balancer" {
    for_each = var.alb_target_group_arn == null ? [] : [1]
    content {
      target_group_arn = var.alb_target_group_arn
      container_name   = var.name
      container_port   = var.alb_container_port
    }
  }

  # Give containers time to boot before ALB health checks fail the task.
  health_check_grace_period_seconds = var.alb_target_group_arn == null ? null : 120

  lifecycle {
    # Ansible drives rollouts with --force-new-deployment against a fixed tag;
    # don't let TF fight it over desired_count drift.
    ignore_changes = [desired_count]
  }
}
