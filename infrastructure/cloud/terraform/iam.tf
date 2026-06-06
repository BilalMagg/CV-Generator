# iam.tf — roles for ECS task execution, the task itself, and EC2 instances.

data "aws_iam_policy_document" "ecs_tasks_assume" {
  statement {
    actions = ["sts:AssumeRole"]
    principals {
      type        = "Service"
      identifiers = ["ecs-tasks.amazonaws.com"]
    }
  }
}

data "aws_iam_policy_document" "ec2_assume" {
  statement {
    actions = ["sts:AssumeRole"]
    principals {
      type        = "Service"
      identifiers = ["ec2.amazonaws.com"]
    }
  }
}

# ── Task EXECUTION role: pulls images, reads SSM secrets, writes logs ─────────
resource "aws_iam_role" "task_execution" {
  name               = "${var.project}-task-execution"
  assume_role_policy = data.aws_iam_policy_document.ecs_tasks_assume.json
}

resource "aws_iam_role_policy_attachment" "task_execution_managed" {
  role       = aws_iam_role.task_execution.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy"
}

# Allow the execution role to read the /cvgen/* SSM parameters and decrypt the
# SecureString values (default aws/ssm KMS key). Scoped to this project's path.
data "aws_iam_policy_document" "ssm_read" {
  statement {
    sid     = "ReadProjectParameters"
    actions = ["ssm:GetParameters", "ssm:GetParameter", "ssm:GetParametersByPath"]
    resources = [
      "arn:aws:ssm:${var.region}:${data.aws_caller_identity.current.account_id}:parameter/${var.project}/*"
    ]
  }
  statement {
    sid       = "DecryptSecureStrings"
    actions   = ["kms:Decrypt"]
    resources = ["*"] # demo: default aws/ssm key. Scope to the key ARN for prod.
  }
}

resource "aws_iam_role_policy" "task_execution_ssm" {
  name   = "${var.project}-task-execution-ssm"
  role   = aws_iam_role.task_execution.id
  policy = data.aws_iam_policy_document.ssm_read.json
}

# ── Task role: identity the app code runs as. Minimal for this app (no AWS
# SDK calls today — MinIO/RDS/Kafka are reached over the network, not via IAM). ─
resource "aws_iam_role" "task" {
  name               = "${var.project}-task"
  assume_role_policy = data.aws_iam_policy_document.ecs_tasks_assume.json
}

# ── EC2 container-instance role ──────────────────────────────────────────────
resource "aws_iam_role" "ecs_instance" {
  name               = "${var.project}-ecs-instance"
  assume_role_policy = data.aws_iam_policy_document.ec2_assume.json
}

resource "aws_iam_role_policy_attachment" "ecs_instance_ecs" {
  role       = aws_iam_role.ecs_instance.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonEC2ContainerServiceforEC2Role"
}

# SSM Session Manager access to the instances (handy for demos; no SSH keys).
resource "aws_iam_role_policy_attachment" "ecs_instance_ssm" {
  role       = aws_iam_role.ecs_instance.name
  policy_arn = "arn:aws:iam::aws:policy/AmazonSSMManagedInstanceCore"
}

resource "aws_iam_instance_profile" "ecs_instance" {
  name = "${var.project}-ecs-instance"
  role = aws_iam_role.ecs_instance.name
}
