APP_NAME=cv-backend
DB_CONTAINER=cv-postgres

#################################
# Docker
#################################

dev:
	docker compose up --build

up:
	docker compose up -d

down:
	docker compose down

restart:
	docker compose restart

logs:
	docker compose logs -f

#################################
# Database
#################################

db:
	docker exec -it $(DB_CONTAINER) psql -U postgres -d cv_db

db-reset:
	docker compose down -v
	docker compose up -d

#################################
# Migrations (EF Core)
#################################

migration:
	dotnet ef migrations add $(name)

migrate:
	dotnet ef database update

#################################
# Development
#################################

run:
	dotnet run

restore:
	dotnet restore

build:
	dotnet build

#################################
# Tests
#################################

test:
	dotnet test

#################################
# Feature generator
#################################

feature:
	./scripts/generate-feature.sh

#################################
# Cleanup
#################################

clean:
	docker system prune -f

#################################
# Help
#################################

help:
	@echo "Available commands:"
	@echo "make dev        → start backend + db"
	@echo "make down       → stop containers"
	@echo "make db         → open postgres"
	@echo "make feature    → generate new feature"
	@echo "make test       → run tests"