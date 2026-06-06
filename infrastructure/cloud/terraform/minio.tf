# minio.tf — MinIO object store, EPHEMERAL storage (no EBS volume).
#
# Deliberate demo trade-off: a restart loses stored CV PDFs. template-agent's
# storage layer self-creates the bucket on first write, so a fresh/empty MinIO
# degrades gracefully. Reachable in-cluster as minio:9000 (Service Connect).
#
# The only consumer that needs MinIO config is template-agent (see services.tf
# and §4). The credentials here MUST match the minio-root-user / -password SSM
# secrets that template-agent reads.

resource "aws_cloudwatch_log_group" "minio" {
  name              = "/ecs/minio"
  retention_in_days = 7
}

resource "aws_ecs_task_definition" "minio" {
  family                   = "minio"
  requires_compatibilities = ["EC2"]
  network_mode             = "awsvpc"
  cpu                      = 256
  memory                   = 512
  execution_role_arn       = aws_iam_role.task_execution.arn
  task_role_arn            = aws_iam_role.task.arn

  container_definitions = jsonencode([
    {
      name      = "minio"
      image     = "minio/minio:latest" # pulled from public registry via NAT
      essential = true
      command   = ["server", "/data", "--console-address", ":9001"]
      portMappings = [
        { name = "minio", containerPort = 9000, protocol = "tcp" },
        { containerPort = 9001, protocol = "tcp" } # console, intra-cluster only
      ]
      secrets = [
        { name = "MINIO_ROOT_USER", valueFrom = local.secret_arn["minio-root-user"] },
        { name = "MINIO_ROOT_PASSWORD", valueFrom = local.secret_arn["minio-root-password"] }
      ]
      logConfiguration = {
        logDriver = "awslogs"
        options = {
          "awslogs-group"         = aws_cloudwatch_log_group.minio.name
          "awslogs-region"        = var.region
          "awslogs-stream-prefix" = "minio"
        }
      }
    }
  ])
}

resource "aws_ecs_service" "minio" {
  name            = "minio"
  cluster         = aws_ecs_cluster.main.arn
  task_definition = aws_ecs_task_definition.minio.arn
  desired_count   = 1

  network_configuration {
    subnets          = local.private_subnet_ids
    security_groups  = [aws_security_group.ecs_tasks.id]
    assign_public_ip = false
  }

  service_connect_configuration {
    enabled   = true
    namespace = aws_service_discovery_private_dns_namespace.main.arn
    service {
      port_name      = "minio"
      discovery_name = "minio"
      client_alias {
        dns_name = "minio"
        port     = 9000
      }
    }
  }

  lifecycle { ignore_changes = [desired_count] }
}
