# ecs-cluster.tf — ECS cluster (EC2 launch type), Service Connect namespace,
# task/instance security groups, launch template, ASG, and capacity provider.

# ── Service Connect private DNS namespace ────────────────────────────────────
# Replaces docker-compose name resolution. Services become reachable inside the
# cluster at http://<service-name>:<port> (e.g. http://user-service:8082).
resource "aws_service_discovery_private_dns_namespace" "main" {
  name        = "${var.project}.local"
  description = "Service Connect namespace for ${var.project}"
  vpc         = aws_vpc.main.id
}

# NOTE on the ECS service-linked role (AWSServiceRoleForECS):
# We do NOT manage it here. It is an ACCOUNT-GLOBAL role that AWS auto-creates
# the first time an ECS cluster is made, and it persists across stacks and
# `terraform destroy` cycles. Trying to create it in Terraform fails once it
# exists ("role name AWSServiceRoleForECS has been taken"), and importing it
# would risk a teardown deleting shared account infrastructure. The cluster
# references it implicitly via ECS, so no Terraform resource is needed.
resource "aws_ecs_cluster" "main" {
  name = "${var.project}-cluster"

  setting {
    name  = "containerInsights"
    value = "enabled"
  }

  service_connect_defaults {
    namespace = aws_service_discovery_private_dns_namespace.main.arn
  }
}

# ── Security groups ──────────────────────────────────────────────────────────
# Task ENIs (awsvpc): reachable from the ALB on the public ports, and freely
# among themselves (Service Connect / gRPC / Kafka / MinIO on any port).
resource "aws_security_group" "ecs_tasks" {
  name        = "${var.project}-ecs-tasks-sg"
  description = "ECS task ENIs"
  vpc_id      = aws_vpc.main.id

  ingress {
    description     = "From ALB (frontend :80 and gateway :8080 are fronted; rule covers both)"
    from_port       = 0
    to_port         = 65535
    protocol        = "tcp"
    security_groups = [aws_security_group.alb.id]
  }

  ingress {
    description = "Intra-cluster service-to-service (Service Connect, gRPC, Kafka, MinIO)"
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    self        = true
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = { Name = "${var.project}-ecs-tasks-sg" }
}

# EC2 container instances.
resource "aws_security_group" "ecs_instances" {
  name        = "${var.project}-ecs-instances-sg"
  description = "ECS EC2 container instances"
  vpc_id      = aws_vpc.main.id

  ingress {
    description = "All traffic from within the VPC"
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = [var.vpc_cidr]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = { Name = "${var.project}-ecs-instances-sg" }
}

# ── Launch template using the ECS-optimized Amazon Linux 2023 AMI ────────────
data "aws_ssm_parameter" "ecs_ami" {
  name = "/aws/service/ecs/optimized-ami/amazon-linux-2023/recommended/image_id"
}

resource "aws_launch_template" "ecs" {
  name_prefix   = "${var.project}-ecs-"
  image_id      = data.aws_ssm_parameter.ecs_ami.value
  instance_type = var.ecs_instance_type

  iam_instance_profile {
    arn = aws_iam_instance_profile.ecs_instance.arn
  }

  vpc_security_group_ids = [aws_security_group.ecs_instances.id]

  # Register the instance with our cluster.
  user_data = base64encode(<<-EOF
    #!/bin/bash
    echo "ECS_CLUSTER=${aws_ecs_cluster.main.name}" >> /etc/ecs/ecs.config
    echo "ECS_ENABLE_CONTAINER_METADATA=true" >> /etc/ecs/ecs.config
  EOF
  )

  tag_specifications {
    resource_type = "instance"
    tags          = { Name = "${var.project}-ecs-instance" }
  }
}

# ENI trunking: lets each *.large container instance attach ~10 "branch" ENIs
# instead of the ~2 default, so awsvpc tasks pack densely (2 instances for ~18
# tasks instead of ~9). Account-level setting; instances must be launched AFTER
# it's enabled, hence the ASG depends_on below.
resource "aws_ecs_account_setting_default" "awsvpc_trunking" {
  name  = "awsvpcTrunking"
  value = "enabled"
}

resource "aws_autoscaling_group" "ecs" {
  name                = "${var.project}-ecs-asg"
  vpc_zone_identifier = local.private_subnet_ids
  min_size            = var.ecs_min_size
  max_size            = var.ecs_max_size
  desired_capacity    = var.ecs_desired_size

  launch_template {
    id      = aws_launch_template.ecs.id
    version = "$Latest"
  }

  depends_on = [aws_ecs_account_setting_default.awsvpc_trunking]

  # Required for the ECS managed capacity provider to manage scaling.
  protect_from_scale_in = false

  tag {
    key                 = "Name"
    value               = "${var.project}-ecs-instance"
    propagate_at_launch = true
  }

  tag {
    key                 = "AmazonECSManaged"
    value               = "true"
    propagate_at_launch = true
  }
}

# ── Capacity provider links the ASG to the cluster ───────────────────────────
resource "aws_ecs_capacity_provider" "main" {
  name = "${var.project}-cp"

  auto_scaling_group_provider {
    auto_scaling_group_arn         = aws_autoscaling_group.ecs.arn
    managed_termination_protection = "DISABLED"

    managed_scaling {
      status          = "ENABLED"
      target_capacity = 100
    }
  }
}

resource "aws_ecs_cluster_capacity_providers" "main" {
  cluster_name       = aws_ecs_cluster.main.name
  capacity_providers = [aws_ecs_capacity_provider.main.name]

  default_capacity_provider_strategy {
    capacity_provider = aws_ecs_capacity_provider.main.name
    weight            = 1
    base              = 1
  }
}
