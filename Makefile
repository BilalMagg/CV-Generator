# ----------------------------------
# Containers
# ----------------------------------

FRONTEND_CONTAINER=cv-frontend
BACKEND_CONTAINER=cv-backend
DB_CONTAINER=cv-postgres
AI_CONTAINER=cv-ai-agents

# Build and start all containers
dev:
	docker compose up --build -d

# Start containers in background
up:
	docker compose up -d

# Stop containers
down:
	docker compose down

# Restart containers
restart:
	docker compose restart

# View logs
logs:
	docker compose logs -f

# ----------------------------------
# Backend (Dotnet)
# ----------------------------------

run-backend:
	cd backend && dotnet run

restore-backend:
	cd backend && dotnet restore

build-backend:
	cd backend && dotnet build

watch-backend:
	cd backend && dotnet watch run

# ----------------------------------
# Database
# ----------------------------------

db:
	docker exec -it $(DB_CONTAINER) psql -U postgres -d cv_db

db-reset:
	docker compose down -v
	docker compose up -d

# ----------------------------------
# EF Core Migrations
# ----------------------------------

migration:
	cd backend && dotnet ef migrations add $(name)

migrations-remove:
	cd backend && dotnet ef migrations remove

migrate:
	cd backend && dotnet ef database update

# ----------------------------------
# Frontend
# ----------------------------------

build-frontend:
	cd frontend && docker build -t frontend-image .

run-frontend:
	docker run -d -p 4200:80 --name $(FRONTEND_CONTAINER) frontend-image

# ----------------------------------
# AI Agents
# ----------------------------------

build-ai:
	cd ai_agents && docker build -t ai-agents-image .

run-ai:
	docker run -d -p 8000:8000 --name $(AI_CONTAINER) ai-agents-image

test-template:
	cd ai_agents && PYTHONPATH=. .venv/Scripts/python.exe app/agents/template_agent/test_template_agent.py

# ----------------------------------
# Cleanup
# ----------------------------------

clean:
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
# Help
# ----------------------------------

help:
	@echo "Available commands:"
	@echo "make dev               → build + run all containers"
	@echo "make up                → start containers in background"
	@echo "make down              → stop containers"
	@echo "make logs              → view logs"
	@echo "make run-backend       → run backend"
	@echo "make build-frontend    → build frontend container"
	@echo "make run-frontend      → run frontend container"
	@echo "make build-ai          → build ai_agents container"
	@echo "make run-ai            → run ai_agents container"
	@echo "make db                → open postgres console"
	@echo "make db-reset          → reset db containers"
	@echo "make migration name=XYZ → create EF migration"
	@echo "make migrate           → apply migrations"
	@echo "make clean             → cleanup docker system"