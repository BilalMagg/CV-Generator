# modules/service/variables.tf — inputs for one ECS-on-EC2 service.

variable "name" {
  description = "Logical service name (also the Service Connect DNS name)."
  type        = string
}

variable "image" {
  description = "Full container image reference (ECR URL:tag)."
  type        = string
}

variable "cpu" {
  description = "Task CPU units."
  type        = number
  default     = 256
}

variable "memory" {
  description = "Task hard memory limit (MiB)."
  type        = number
  default     = 512
}

variable "rest_port" {
  description = "Primary (HTTP/REST) container port."
  type        = number
}

variable "grpc_port" {
  description = "Optional gRPC container port. A second Service Connect port is registered only when set."
  type        = number
  default     = null
}

variable "environment" {
  description = "Non-secret env vars (plain values) injected into the task."
  type        = map(string)
  default     = {}
}

variable "secrets" {
  description = "Secret env vars: map of ENV_VAR_NAME -> SSM parameter ARN."
  type        = map(string)
  default     = {}
}

variable "cluster_arn" {
  type = string
}

variable "execution_role_arn" {
  type = string
}

variable "task_role_arn" {
  type = string
}

variable "subnets" {
  type = list(string)
}

variable "security_groups" {
  type = list(string)
}

variable "service_connect_namespace_arn" {
  type = string
}

variable "region" {
  type = string
}

variable "desired_count" {
  type    = number
  default = 1
}

# ALB attachment is optional — only frontend and api-gateway pass these.
variable "alb_target_group_arn" {
  type    = string
  default = null
}

variable "alb_container_port" {
  type    = number
  default = null
}

variable "log_retention_days" {
  type    = number
  default = 7
}
