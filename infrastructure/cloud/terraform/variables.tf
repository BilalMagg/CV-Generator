# variables.tf — inputs for the whole stack.

variable "region" {
  description = "AWS region to deploy into."
  type        = string
  default     = "us-east-1"
}

variable "project" {
  description = "Project prefix applied to every resource name and SSM path."
  type        = string
  default     = "cvgen"
}

variable "vpc_cidr" {
  description = "CIDR for the VPC."
  type        = string
  default     = "10.20.0.0/16"
}

variable "az_count" {
  description = "Number of AZs to spread subnets across. ALB requires >= 2."
  type        = number
  default     = 2
}

# ── RDS ────────────────────────────────────────────────────────────────────
# A single shared master credential is reused for all 8 logical databases,
# exactly as the app already reuses one POSTGRES_USER/POSTGRES_PASSWORD pair.
variable "db_username" {
  description = "Master username for the shared RDS PostgreSQL instance."
  type        = string
  default     = "postgres"
}

variable "db_password" {
  description = "Master password for RDS. Provide via tfvars or TF_VAR_db_password; never commit it."
  type        = string
  sensitive   = true
}

variable "db_instance_class" {
  description = "RDS instance class. db.t3.micro is the only size allowed on AWS Free-plan accounts; bump up if you upgrade the account plan."
  type        = string
  default     = "db.t3.micro"
}

variable "db_allocated_storage" {
  description = "RDS allocated storage in GB."
  type        = number
  default     = 20
}

# ── ECS EC2 capacity ─────────────────────────────────────────────────────────
# awsvpc network mode (required by Service Connect) gives each task its own ENI,
# so instance ENI limits cap tasks-per-host. ENI trunking (enabled in
# ecs-cluster.tf) lets a *.large host ~10 tasks instead of ~2, so 2 instances
# cover the ~18 tasks.
#
# m7i-flex.large (2 vCPU / 8 GiB) is on the AWS free-plan allowed EC2 list
# (post-2025-07-15 accounts): t3.micro, t3.small, t4g.micro, t4g.small,
# c7i-flex.large (4 GiB), m7i-flex.large (8 GiB). t3.large is NOT allowed.
# Usage draws from the account's free-plan credits.
variable "ecs_instance_type" {
  description = "EC2 instance type for ECS container instances. Must be on the free-plan allowed list (e.g. m7i-flex.large / c7i-flex.large)."
  type        = string
  default     = "m7i-flex.large"
}

variable "ecs_min_size" {
  description = "Minimum number of ECS EC2 instances in the ASG."
  type        = number
  default     = 5
}

variable "ecs_max_size" {
  description = "Maximum number of ECS EC2 instances in the ASG."
  type        = number
  default     = 6
}

variable "ecs_desired_size" {
  description = "Desired number of ECS EC2 instances in the ASG. The ~18 tasks are CPU-bound and need ~5x m7i-flex.large to place (the original '2' under-provisioned and forced manual scaling to 5). Lower min once notification-service is fixed and real fit is confirmed."
  type        = number
  default     = 5
}

variable "image_tag" {
  description = "Container image tag deployed for every service (Ansible pushes this tag to ECR)."
  type        = string
  default     = "latest"
}

# ── Service inventory ──────────────────────────────────────────────────────
# The authoritative list of services that get an ECR repo. Keep in sync with
# services.tf / infra-services.tf. (orchestrator is dormant and kafka-ui is
# dev-only — both excluded. keycloak gets a CUSTOM image so the realm is baked
# in — see §5.5 / infra-services.tf — so it needs an ECR repo too.)
variable "service_names" {
  description = "Logical names of all deployed app/infra services that need an ECR repo."
  type        = list(string)
  default = [
    # frontend + gateway
    "frontend",
    "api-gateway",
    # .NET microservices
    "user-service",
    "user-content-service",
    "workflow-service",
    "application-service",
    "job-offer-service",
    "notification-service",
    "cv-service",
    # Python AI agents
    "job-extractor",
    "search-agent",
    "template-agent",
    "cv-optimizer",
    "contact-agent",
    "job-crawler",
    # custom keycloak image (realm baked in)
    "keycloak",
  ]
}
