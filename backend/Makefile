APP_NAME=cv-backend
DB_CONTAINER=cv-postgres
MONOLITH_DIR=src/monolith-service
MONOLITH_PROJECT=$(MONOLITH_DIR)/MonolithService.csproj

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
	dotnet ef migrations add $(name) --project $(MONOLITH_PROJECT)

migrations-remove:
	dotnet ef migrations remove --project $(MONOLITH_PROJECT)

migrate:
	dotnet ef database update --project $(MONOLITH_PROJECT)

#################################
# Development
#################################

run:
	dotnet run --project $(MONOLITH_PROJECT)

restore:
	dotnet restore $(MONOLITH_PROJECT)

build:
	dotnet build $(MONOLITH_PROJECT)

watch:
	dotnet watch run --project $(MONOLITH_PROJECT)

#################################
# Tests
#################################

test:
	dotnet test

#################################
# Feature generator
#################################

feature:
	./$(MONOLITH_DIR)/scripts/generate-feature.sh

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