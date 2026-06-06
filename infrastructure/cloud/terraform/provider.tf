# provider.tf — AWS provider + Terraform settings.
#
# State is intentionally LOCAL (no remote backend) for this demo. The
# .gitignore at terraform/cloud excludes *.tfstate* so state never lands in git.
# For a team setup you would add an S3 backend + DynamoDB lock here.

terraform {
  required_version = ">= 1.5.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }
}

provider "aws" {
  region = var.region

  default_tags {
    tags = {
      Project   = var.project
      ManagedBy = "terraform"
      Stack     = "cv-generator-cloud"
    }
  }
}

# Account id / partition helpers used when building ARNs by hand.
data "aws_caller_identity" "current" {}
data "aws_region" "current" {}
