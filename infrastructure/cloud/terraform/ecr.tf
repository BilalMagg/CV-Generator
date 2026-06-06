# ecr.tf — one ECR repository per service (15 app services + custom keycloak).
#
# Chosen over DockerHub to avoid anonymous pull rate limits during ECS task
# starts. Ansible (role: images) builds and pushes :${image_tag} into these.
# Stock infra images (confluentinc/cp-kafka, minio/minio) are pulled from their
# public registries through the NAT gateway and are NOT mirrored here.

resource "aws_ecr_repository" "service" {
  for_each = toset(var.service_names)

  name                 = "${var.project}/${each.key}"
  image_tag_mutability = "MUTABLE"
  force_delete         = true # demo: allow `terraform destroy` even with images present

  image_scanning_configuration {
    scan_on_push = true
  }

  tags = { Name = "${var.project}-${each.key}" }
}

locals {
  # service name -> repository URL (used by services.tf / infra-services.tf and outputs)
  ecr_repo_urls = { for name, repo in aws_ecr_repository.service : name => repo.repository_url }
}
