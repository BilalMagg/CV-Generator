# alb.tf — public Application Load Balancer on :80.
#
# Routing:
#   /api/*  -> api-gateway target group   (health check: /health)
#   /*      -> frontend target group       (health check: /  — nginx SPA returns 200)
#
# HTTP only. TLS requires a custom domain + ACM cert, which is out of scope for
# this demo (see README known limitations).

resource "aws_security_group" "alb" {
  name        = "${var.project}-alb-sg"
  description = "Public ALB ingress on :80"
  vpc_id      = aws_vpc.main.id

  ingress {
    description = "HTTP from anywhere"
    from_port   = 80
    to_port     = 80
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = { Name = "${var.project}-alb-sg" }
}

resource "aws_lb" "main" {
  name               = "${var.project}-alb"
  internal           = false
  load_balancer_type = "application"
  security_groups    = [aws_security_group.alb.id]
  subnets            = local.public_subnet_ids

  tags = { Name = "${var.project}-alb" }
}

# Target type "ip" because tasks run with the awsvpc network mode (each task
# has its own ENI/IP).
resource "aws_lb_target_group" "frontend" {
  name        = "${var.project}-tg-frontend"
  port        = 80
  protocol    = "HTTP"
  vpc_id      = aws_vpc.main.id
  target_type = "ip"

  health_check {
    path                = "/" # nginx SPA fallback returns 200 at /
    matcher             = "200"
    interval            = 30
    healthy_threshold   = 2
    unhealthy_threshold = 3
  }

  tags = { Name = "${var.project}-tg-frontend" }
}

resource "aws_lb_target_group" "api_gateway" {
  name        = "${var.project}-tg-gateway"
  port        = 8080
  protocol    = "HTTP"
  vpc_id      = aws_vpc.main.id
  target_type = "ip"

  health_check {
    # The gateway has no route at "/", but exposes GET /health -> 200.
    # Using "/" here would mark a healthy gateway unhealthy. (§5.2 correction.)
    path                = "/health"
    matcher             = "200"
    interval            = 30
    healthy_threshold   = 2
    unhealthy_threshold = 3
  }

  tags = { Name = "${var.project}-tg-gateway" }
}

# Keycloak target group. NOTE (added during code-side verification): Keycloak
# must be reachable publicly for the browser OIDC redirect (KEYCLOAK_EXTERNAL_*)
# and for the Google/GitHub broker callback URLs
# (http://<alb-dns>/realms/cv-realm/broker/...). The original spec's ALB only
# routed /api/* and /*, which would break login. We expose Keycloak under the
# same ALB host at its well-known paths. Pinning KC_HOSTNAME to this ALB URL
# (set by Ansible) keeps the token `iss` consistent for the gateway's validator.
resource "aws_lb_target_group" "keycloak" {
  name        = "${var.project}-tg-keycloak"
  port        = 8080
  protocol    = "HTTP"
  vpc_id      = aws_vpc.main.id
  target_type = "ip"

  health_check {
    # Keycloak's /health is on the mgmt port (9000) in KC 26; the realm
    # endpoint reliably returns 200 on 8080, so health-check that instead.
    path                = "/realms/master"
    matcher             = "200"
    interval            = 30
    healthy_threshold   = 2
    unhealthy_threshold = 3
  }

  tags = { Name = "${var.project}-tg-keycloak" }
}

resource "aws_lb_listener" "http" {
  load_balancer_arn = aws_lb.main.arn
  port              = 80
  protocol          = "HTTP"

  default_action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.frontend.arn
  }
}

resource "aws_lb_listener_rule" "api" {
  listener_arn = aws_lb_listener.http.arn
  priority     = 10

  action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.api_gateway.arn
  }

  condition {
    path_pattern {
      values = ["/api/*"]
    }
  }
}

resource "aws_lb_listener_rule" "keycloak" {
  listener_arn = aws_lb_listener.http.arn
  priority     = 20

  action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.keycloak.arn
  }

  condition {
    path_pattern {
      values = ["/realms/*", "/resources/*", "/admin/*"]
    }
  }
}
