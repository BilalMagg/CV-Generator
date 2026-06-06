# outputs.tf — values Ansible (and the operator) consume after `terraform apply`.

output "region" {
  value = var.region
}

output "project" {
  value = var.project
}

output "alb_dns_name" {
  description = "Public ALB DNS name (the app's entry point). Ansible writes this into /cvgen/ext/*."
  value       = aws_lb.main.dns_name
}

output "alb_url" {
  description = "Convenience http:// URL for the ALB."
  value       = "http://${aws_lb.main.dns_name}"
}

output "rds_endpoint" {
  description = "RDS host (no port). Ansible uses this to build connection strings and create the 8 DBs."
  value       = aws_db_instance.main.address
}

output "rds_port" {
  value = aws_db_instance.main.port
}

output "db_username" {
  value = var.db_username
}

output "ecs_cluster_name" {
  value = aws_ecs_cluster.main.name
}

output "service_connect_namespace" {
  value = aws_service_discovery_private_dns_namespace.main.name
}

output "ecr_repository_urls" {
  description = "Map of service name -> ECR repository URL (Ansible images role pushes here)."
  value       = local.ecr_repo_urls
}

output "ecs_service_names" {
  description = "All ECS service names (app + infra) for force-new-deployment rollouts."
  value = concat(
    keys(local.app_services),
    ["kafka", "keycloak", "minio"],
  )
}
