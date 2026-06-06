# CV Generator — AWS deployment (ECS on EC2)

Infrastructure-as-code to run CV Generator on AWS. **Terraform builds the infra;
Ansible deploys onto it.** This was authored from a fixed architecture spec —
the decisions below were made deliberately and should not be redesigned.

> Authoring note: the verbatim companion files for this deployment were not
> available, so these files were **authored from the spec**. They reflect that
> interpretation. A few corrections that require reading the application code
> were folded in and are called out under **Code-side corrections** below.

## Architecture (fixed)

- **ECS on EC2** (EC2 launch type, ASG-backed capacity provider) — not Fargate.
- **One RDS PostgreSQL 16** instance holds all 8 databases (7 app DBs +
  `keycloak`). pgvector is enabled on `workflow_db` and `cv_db`. A single shared
  master credential is reused (matches the app's existing single Postgres user).
- **ECR** — one repository per service (15 app services + a custom keycloak).
- **ALB :80** — `/api/*` → api-gateway, `/realms/*`,`/resources/*`,`/admin/*` →
  keycloak, everything else → frontend. HTTP only.
- **Kafka and MinIO run with EPHEMERAL storage** (demo trade-off: a restart
  loses in-flight events / stored CV PDFs; both regenerate cheaply).
- **NAT gateway** for private-subnet outbound (ECR pulls, LLM APIs, SMTP).
- **ECS Service Connect** provides in-cluster DNS (`user-service:8082`,
  `kafka:9092`, `minio:9000`, `keycloak:8080`, …), replacing compose name
  resolution. Network mode is `awsvpc`; **ENI trunking is enabled** (in
  `ecs-cluster.tf`) so each `.large` instance hosts ~10 tasks.
- **Secrets in SSM Parameter Store**, injected into tasks at launch.
- Region `us-east-1`, project prefix `cvgen`.

### Runs on the AWS Free plan

This stack is sized for a **free-plan account** (the 2025 free tier with up to
$200 credits): **ECS itself is free**, **ALB is free-tier eligible**, EC2 uses
**`m7i-flex.large`** (8 GiB — on the free-plan allowed instance list: `t3.micro`,
`t3.small`, `t4g.micro`, `t4g.small`, `c7i-flex.large`, `m7i-flex.large`; note
`t3.large` is NOT allowed), and RDS uses **`db.t3.micro`** (the only free-plan
RDS size). Usage draws from your credits — there are **no charges until you
choose to upgrade**. Cost levers: **stop the EC2 instances when not demoing**;
**dropping the NAT gateway** (~$32/mo) is the biggest saving if you move ECS
instances to public subnets (not done here to keep the design simple).

15 deployed app services (orchestrator is dormant, kafka-ui is dev-only — both
excluded) + 3 infra services (kafka, keycloak, minio).

## Layout

```
infrastructure/cloud/
├── README.md            ← this file
├── keycloak/Dockerfile  ← custom Keycloak image (realm baked in)
├── terraform/           ← all infrastructure (run `terraform` here)
└── ansible/             ← deploy automation (run `ansible-playbook` here)
```

## The env-var split

Each task definition splits its environment two ways:

- **Non-secret config → `environment`** (plain values): ports,
  `ASPNETCORE_ENVIRONMENT`, internal Service Connect URLs (e.g.
  `http://user-service:18082`, `kafka:9092`, `minio:9000`), MinIO bucket names.
- **Secrets → `secrets`** (value is an SSM parameter ARN): API keys, client
  secrets, passwords, and the full `ConnectionStrings__DefaultConnection` (it
  embeds the DB password, so the whole string is secret).

### external_host overlay (`/cvgen/ext/*`)

A handful of vars must resolve to the **public ALB DNS**, which only exists after
`terraform apply`. Terraform creates them as **placeholder** SSM params with
`lifecycle { ignore_changes = [value] }`; Ansible (`secrets_overlay`) overwrites
them with the real ALB DNS, and a later `terraform apply` won't revert them. The
set: api-gateway `GATEWAY_HOST/PORT`, `FRONTEND_HOST/PORT`,
`KEYCLOAK_EXTERNAL_HOST/PORT`; frontend `FRONTEND_HOST`; notification-service
`Gmail__CallbackBaseUrl`; keycloak `KC_HOSTNAME`. Pinning `KC_HOSTNAME` to the
ALB URL is what keeps the token `iss` consistent for the gateway's JWT validator.

## Prerequisites

- Terraform ≥ 1.5, AWS CLI v2 (configured credentials), Docker.
- The **AWS Session Manager plugin** (for the SSM tunnel used to reach private RDS).
- `psql` + the `community.postgresql` Ansible collection
  (`ansible-galaxy collection install community.postgresql`) + `psycopg2`.
- Ansible ≥ 2.14.

## Runbook

### 1) Provision infrastructure

```bash
cd infrastructure/cloud/terraform
cp terraform.tfvars.example terraform.tfvars     # set db_password (gitignored)
terraform init
terraform apply                                  # VPC/RDS/ECR/ALB/ECS/SSM placeholders
```

### 2) Deploy with Ansible (no Vault)

Ansible Vault is **optional** — the playbook reads `group_vars/secrets.yml` as plain
YAML. Create it unencrypted and just omit `--ask-vault-pass`. (`secrets.yml` is
gitignored.) RDS is private, so the **`database`** role reaches it through an SSM
port-forward tunnel; every other role runs directly.

```bash
cd ../ansible
cp group_vars/secrets.yml.example group_vars/secrets.yml   # fill values; db_password MUST match tfvars

# 0. confirm at least one ECS container instance is up & SSM-registered
aws ssm describe-instance-information --region us-east-1

# 1. build + push all images to ECR
ansible-playbook playbook.yml --tags images

# 2. write SSM secrets + ALB-DNS overlay (uses the REAL private RDS endpoint)
ansible-playbook playbook.yml --tags secrets

# 3. create the 8 DBs + pgvector — via an SSM tunnel to private RDS.
#    In a SECOND terminal, open the tunnel (leave it running):
aws ssm start-session --region us-east-1 \
  --target <ec2-instance-id> \
  --document-name AWS-StartPortForwardingSessionToRemoteHost \
  --parameters '{"host":["<rds-endpoint>"],"portNumber":["5432"],"localPortNumber":["15432"]}'
#    Then run ONLY the database role against the tunnel (extra-vars override the
#    real endpoint for this run only):
ansible-playbook playbook.yml --tags database -e rds_endpoint=localhost -e rds_port=15432

# 4. roll the ECS services + smoke-test
ansible-playbook playbook.yml --tags deploy
ansible-playbook playbook.yml --tags smoketest
```

`<ec2-instance-id>` comes from step 0 (or `aws ecs list-container-instances
--cluster cvgen-cluster`). `<rds-endpoint>` is the `rds_endpoint` Terraform output.

### 3) MANUAL one-time step — set OAuth redirect URIs (see Known limitations)

The app is then reachable at the `alb_url` Terraform output.

## Known limitations (deliberate / demo scope)

- **Ephemeral Kafka & MinIO** — restart loses in-flight events / stored PDFs.
- **HTTP only** — no TLS. Real TLS needs a custom domain + ACM cert on the ALB.
- **No HA** — single NAT, single-AZ RDS (Multi-AZ off), `desired_count = 1` per
  service.
- **template-agent has no `pdflatex`** in its `python:3.11-slim` image, so
  **LaTeX** templates raise at render time; **HTML** templates work (WeasyPrint
  is bundled). Add a TeX distribution to that image to enable LaTeX.
- **cv-service PDF/DOCX export is a stub** (`// TODO` in the code).
- **OAuth redirect URIs are MANUAL.** Google/GitHub console redirect URIs live in
  third-party consoles and cannot be templated. After you know the ALB DNS, set:
  `http://<alb-dns>/realms/cv-realm/broker/google/endpoint` and
  `http://<alb-dns>/realms/cv-realm/broker/github/endpoint`.

## Open items / things to verify before relying on this

1. **awsvpc ENI capacity — handled.** ENI trunking is enabled in Terraform
   (`aws_ecs_account_setting_default.awsvpc_trunking`), and the ASG depends on it
   so instances launch trunking-capable. 2× `m7i-flex.large` (~20 task-ENIs,
   16 GiB) covers the ~18 tasks. If you change the instance family, confirm it
   supports ENI trunking.
2. **RDS is private.** The Ansible `database` and `secrets_overlay`(DB strings)
   steps must reach RDS, which is not publicly accessible. Run the playbook from
   inside the VPC, or open an SSM port-forward / bastion tunnel to the RDS
   endpoint and override `rds_endpoint`/`rds_port` to the tunnel. (SSM
   `put-parameter`/ECS/ECR/Docker steps work fine from anywhere.)
3. **Keycloak realm + ALB exposure (code-side correction).** The realm is baked
   into a custom image (`keycloak/Dockerfile`) because ECS has no host
   bind-mount, and Keycloak is exposed via the ALB so browser login + IdP
   callbacks work. Confirm the realm export in
   `api_gateway/keycloak-realm/cv-realm-realm.json` is the one you want imported.
4. **Keycloak prod mode.** Runs `start --optimized --import-realm` with
   `KC_HTTP_ENABLED=true`, `KC_PROXY=edge`, `KC_HOSTNAME_STRICT=false`, and
   `KC_HOSTNAME` pinned to the ALB. If import doesn't take, check the task logs
   in CloudWatch (`/ecs/keycloak`).

## Code-side corrections folded in

- **ALB health checks:** api-gateway uses `/health` (it has no `/` route);
  frontend uses `/` (nginx SPA returns 200).
- **Image build contexts:** backend services build from `backend/`, AI agents
  from `ai_agents/` (their Dockerfiles COPY shared `common-protos` /
  `common-tools`) — not from the per-service directory.
- **Keycloak** image + ALB exposure as described above.
- **MinIO** task var names (`MINIO_ENDPOINT`, `MINIO_ROOT_USER`,
  `MINIO_ROOT_PASSWORD`, `MINIO_SECURE`, `MINIO_BUCKET`,
  `MINIO_TEMPLATES_BUCKET`) match `cvtools/core/tools/minio_storage.py`.

## CI

The repo's existing CI (`.github/workflows/ci.yml`) pushes images to
GHCR→DockerHub; this deployment's Ansible `images` role pushes to ECR from the
operator's machine. They coexist. Adding an ECR push to CI is optional and was
intentionally left out (no unprompted CI changes).
