# ----------------------------------
# Containers
# ----------------------------------

# Build and start all containers
all:
	docker compose up --build -d

dev:
	docker compose up --build -d

# Start containers in background
up:
	docker compose up -d

# Start all services in background
start:
	docker compose up -d

# Stop all containers
down:
	docker compose down

# Stop and remove volumes
clean-down:
	docker compose down -v

# Restart all containers
restart:
	docker compose restart

# View logs (all services)
logs:
	docker compose logs -f

# View logs for specific service
logs-%:
	docker compose logs -f $*

# ----------------------------------
# Backend Microservices
# ----------------------------------

build-backend:
	docker compose build user-service user-content-service workflow-service application-service job-offer-service notification-service cv-service

rebuild-backend:
	docker compose up --build -d user-service user-content-service workflow-service application-service job-offer-service notification-service cv-service

# ----------------------------------
# Database
# ----------------------------------

db-user:
	docker exec -it cv-user-db psql -U postgres -d user_db

db-content:
	docker exec -it cv-content-db psql -U postgres -d content_db

db-workflow:
	docker exec -it cv-workflow-db psql -U postgres -d workflow_db

db-application:
	docker exec -it cv-application-db psql -U postgres -d application_db

db-job-offer:
	docker exec -it cv-job-offer-db psql -U postgres -d job_offer_db

db-notification:
	docker exec -it cv-notification-db psql -U postgres -d notification_db

db-cv:
	docker exec -it cv-cv-db psql -U postgres -d cv_db

db-keycloak:
	docker exec -it cv-keycloak-db psql -U postgres -d keycloak_db

db-reset:
	docker compose down -v
	docker compose up -d

# ----------------------------------
# EF Core Migrations
# ----------------------------------

migration:
	cd backend/src/$(service) && dotnet ef migrations add $(name)

migrate:
	cd backend/src/$(service) && dotnet ef database update

# ----------------------------------
# Frontend
# ----------------------------------

build-frontend:
	docker compose build frontend

rebuild-frontend:
	docker compose up --build -d frontend

# ----------------------------------
# AI Agents
# ----------------------------------

build-ai:
	docker compose build ai_agents

rebuild-ai:
	docker compose up --build -d ai_agents

test-template:
	cd ai_agents && PYTHONPATH=. .venv/Scripts/python.exe app/agents/template_agent/test_template_agent.py

# ----------------------------------
# Infrastructure
# ----------------------------------

kafka-topics:
	docker exec cv-kafka kafka-topics --bootstrap-server localhost:9092 --list

minio-console:
	@echo "MinIO Console: http://localhost:9001"
	@echo "MinIO API: http://localhost:9000"
	@echo "Credentials: minioadmin/minioadmin"

keycloak-admin:
	@echo "Keycloak Admin: http://localhost:8443"
	@echo "Credentials: admin/admin"

kafka-ui:
	@echo "Kafka UI: http://localhost:8090"

# ----------------------------------
# Cleanup
# ----------------------------------

clean:
	docker system prune -f

clean-all:
	docker compose down -v
	docker system prune -f

# ----------------------------------
# Feature generator
# ----------------------------------

feature:
	./backend/scripts/generate-feature.sh

# ----------------------------------
# Tests
# ----------------------------------

test:
	cd backend && dotnet test

# ----------------------------------
# Health Check
# ----------------------------------

health:
	docker compose ps

# ----------------------------------
# Help
# ----------------------------------

help:
	@echo "Available commands:"
	@echo ""
	@echo "  CONTAINER LIFECYCLE:"
	@echo "    make all / dev        → build + start all containers"
	@echo "    make up / start       → start containers in background"
	@echo "    make down             → stop containers"
	@echo "    make clean-down       → stop + remove volumes"
	@echo "    make restart          → restart all containers"
	@echo "    make logs             → view all logs"
	@echo "    make logs-SERVICE     → view service logs"
	@echo "    make health           → check container status"
	@echo ""
	@echo "  BACKEND MICROSERVICES:"
	@echo "    make build-backend    → build all backend services"
	@echo "    make rebuild-backend  → rebuild + restart backend services"
	@echo ""
	@echo "  DATABASE:"
	@echo "    make db-user          → open user database"
	@echo "    make db-content       → open content database"
	@echo "    make db-workflow      → open workflow database"
	@echo "    make db-application   → open application database"
	@echo "    make db-job-offer     → open job offer database"
	@echo "    make db-notification  → open notification database"
	@echo "    make db-cv            → open cv database"
	@echo "    make db-keycloak      → open keycloak database"
	@echo "    make db-reset         → reset all database containers"
	@echo ""
	@echo "  EF CORE MIGRATIONS:"
	@echo "    make migration service=XYZ name=ABC → create migration"
	@echo "    make migrate service=XYZ            → apply migrations"
	@echo ""
	@echo "  FRONTEND:"
	@echo "    make build-frontend   → build frontend container"
	@echo "    make rebuild-frontend → rebuild + restart frontend"
	@echo ""
	@echo "  AI AGENTS:"
	@echo "    make build-ai         → build ai agents container"
	@echo "    make rebuild-ai       → rebuild + restart ai agents"
	@echo "    make test-template    → run template agent tests"
	@echo ""
	@echo "  INFRASTRUCTURE:"
	@echo "    make kafka-topics     → list Kafka topics"
	@echo "    make minio-console    → show MinIO console info"
	@echo "    make keycloak-admin   → show Keycloak admin info"
	@echo "    make kafka-ui         → show Kafka UI info"
	@echo ""
	@echo "  CLEANUP:"
	@echo "    make clean            → prune unused docker resources"
	@echo "    make clean-all        → down -v + prune"
	@echo ""
	@echo "  MISC:"
	@echo "    make feature          → generate new feature"
	@echo "    make test             → run backend tests"
