APP_NAME=cv-backend
DB_CONTAINER=cv-postgres
KAFKA_CONTAINER=cv-kafka
MONOLITH_DIR=src/monolith-service
MONOLITH_PROJECT=$(MONOLITH_DIR)/MonolithService.csproj

SERVICE_DIR=src/$(service)

#################################
# Docker (all services)
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

build:
	docker compose build

build-single:
	docker compose build $(service)

#################################
# Single service (standalone)
#################################

run-service:
	$(MAKE) -C $(SERVICE_DIR) up

stop-service:
	$(MAKE) -C $(SERVICE_DIR) down

logs-service:
	$(MAKE) -C $(SERVICE_DIR) logs

build-service:
	$(MAKE) -C $(SERVICE_DIR) build

rebuild-service:
	$(MAKE) -C $(SERVICE_DIR) down
	$(MAKE) -C $(SERVICE_DIR) up

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
# Development (monolith)
#################################

run:
	dotnet run --project $(MONOLITH_PROJECT)

restore:
	dotnet restore $(MONOLITH_PROJECT)

build-monolith:
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
# Kafka
#################################

kafka-topics:
	docker exec -it $(KAFKA_CONTAINER) kafka-topics --bootstrap-server localhost:9092 --list

kafka-create-topic:
	docker exec -it $(KAFKA_CONTAINER) kafka-topics --bootstrap-server localhost:9092 --create --topic $(topic) --partitions 1 --replication-factor 1

kafka-delete-topic:
	docker exec -it $(KAFKA_CONTAINER) kafka-topics --bootstrap-server localhost:9092 --delete --topic $(topic)

kafka-consume:
	docker exec -it $(KAFKA_CONTAINER) kafka-console-consumer --bootstrap-server localhost:9092 --topic $(topic) --from-beginning

kafka-produce:
	docker exec -it $(KAFKA_CONTAINER) kafka-console-producer --bootstrap-server localhost:9092 --topic $(topic)

kafka-describe:
	docker exec -it $(KAFKA_CONTAINER) kafka-topics --bootstrap-server localhost:9092 --describe --topic $(topic)

kafka-ui:
	@echo "Kafka UI: http://localhost:8090"

#################################
# Cleanup
#################################

clean:
	docker system prune -f

clean-all:
	docker compose down -v --rmi all
	docker system prune -f

#################################
# Help
#################################

help:
	@echo "Backend commands:"
	@echo ""
	@echo "  ALL SERVICES (docker compose)"
	@echo "  make dev                → build + start all services"
	@echo "  make up                 → start all in background"
	@echo "  make down               → stop all"
	@echo "  make build              → build all images"
	@echo "  make build-single service=application-service → build one service"
	@echo ""
	@echo "  SINGLE SERVICE (standalone)"
	@echo "  make run-service service=user-service        → run one service + its db"
	@echo "  make stop-service service=user-service       → stop one service"
	@echo "  make logs-service service=user-service       → view logs"
	@echo "  make rebuild-service service=user-service    → rebuild + restart"
	@echo ""
	@echo "  Services: user-service, user-content-service, workflow-service"
	@echo "            application-service, job-offer-service, notification-service"
	@echo ""
	@echo "  DATABASE"
	@echo "  make db                   → open postgres console"
	@echo ""
	@echo "  KAFKA"
	@echo "  make kafka-topics         → list topics"
	@echo "  make kafka-create-topic topic=X"
	@echo "  make kafka-consume topic=X"
	@echo "  make kafka-produce topic=X"
	@echo "  make kafka-ui             → http://localhost:8090"
	@echo ""
	@echo "  MONOLITH (local dev)"
	@echo "  make run                  → run monolith locally"
	@echo "  make watch                → hot reload monolith"
	@echo "  make feature              → scaffold new feature"
	@echo "  make migration name=X     → add EF migration"
	@echo "  make migrate              → apply migrations"
	@echo "  make test                 → run tests"
