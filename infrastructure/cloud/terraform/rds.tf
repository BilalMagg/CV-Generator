# rds.tf — ONE PostgreSQL 16 instance holding all 8 logical databases
# (7 app DBs + keycloak). The 8 databases and the pgvector extension on
# workflow_db / cv_db are created by Ansible (role: database), because RDS
# only creates the single initial database at provision time.
#
# Single-AZ, not publicly accessible, demo-sized. No read replica, no Multi-AZ.

resource "aws_db_subnet_group" "main" {
  name       = "${var.project}-db-subnets"
  subnet_ids = local.private_subnet_ids
  tags       = { Name = "${var.project}-db-subnets" }
}

resource "aws_security_group" "rds" {
  name        = "${var.project}-rds-sg"
  description = "Allow PostgreSQL from ECS task ENIs only."
  vpc_id      = aws_vpc.main.id

  ingress {
    description     = "PostgreSQL from ECS tasks"
    from_port       = 5432
    to_port         = 5432
    protocol        = "tcp"
    security_groups = [aws_security_group.ecs_tasks.id]
  }

  # Outbound is unrestricted (RDS itself initiates little; kept simple for demo).
  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = { Name = "${var.project}-rds-sg" }
}

resource "aws_db_instance" "main" {
  identifier     = "${var.project}-postgres"
  engine         = "postgres"
  engine_version = "16"

  instance_class    = var.db_instance_class
  allocated_storage = var.db_allocated_storage
  storage_type      = "gp2" # gp2 is free-tier eligible; gp3 may be rejected on free-plan accounts

  # The instance's bootstrap database. The remaining 7 are created by Ansible.
  db_name  = "postgres"
  username = var.db_username
  password = var.db_password

  db_subnet_group_name   = aws_db_subnet_group.main.name
  vpc_security_group_ids = [aws_security_group.rds.id]
  publicly_accessible    = false
  multi_az               = false

  # Demo conveniences: skip the final snapshot and allow teardown.
  skip_final_snapshot = true
  deletion_protection = false
  apply_immediately   = true

  tags = { Name = "${var.project}-postgres" }
}
