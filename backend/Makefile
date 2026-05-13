KAFKA=cv-kafka

SERVICES := user-service user-content-service workflow-service \
            application-service job-offer-service notification-service \
            cv-service

#################################
# Docker Compose (all services)
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
	$(MAKE) -C src/$(service) up

stop-service:
	$(MAKE) -C src/$(service) down

logs-service:
	$(MAKE) -C src/$(service) logs

rebuild-service:
	$(MAKE) -C src/$(service) down
	$(MAKE) -C src/$(service) up

#################################
# Database
#################################

db:
	docker exec -it cv-user-db psql -U postgres -d user_db

db-reset:
	docker compose down -v
	docker compose up -d

#################################
# Migrations (EF Core)
#################################

migration:
	dotnet ef migrations add $(name) --project src/$(service)/$(service).csproj

migrate:
	dotnet ef database update --project src/$(service)/$(service).csproj

#################################
# Kafka
#################################

kafka-topics:
	docker exec -it $(KAFKA) kafka-topics --bootstrap-server localhost:9092 --list

kafka-create-topic:
	docker exec -it $(KAFKA) kafka-topics --bootstrap-server localhost:9092 --create --topic $(topic) --partitions 1 --replication-factor 1

kafka-delete-topic:
	docker exec -it $(KAFKA) kafka-topics --bootstrap-server localhost:9092 --delete --topic $(topic)

kafka-consume:
	docker exec -it $(KAFKA) kafka-console-consumer --bootstrap-server localhost:9092 --topic $(topic) --from-beginning

kafka-produce:
	docker exec -it $(KAFKA) kafka-console-producer --bootstrap-server localhost:9092 --topic $(topic)

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
	@echo "  make dev                → build + start all"
	@echo "  make up                 → start all in background"
	@echo "  make down               → stop all"
	@echo "  make build              → build all images"
	@echo "  make build-single service=application-service"
	@echo ""
	@echo "  SINGLE SERVICE (standalone)"
	@echo "  make run-service service=user-service    → run one service + db"
	@echo "  make stop-service service=user-service   → stop one service"
	@echo "  make logs-service service=user-service   → view logs"
	@echo "  make rebuild-service service=user-service"
	@echo ""
	@echo "  Services: $(SERVICES)"
	@echo ""
	@echo "  DATABASE"
	@echo "  make db                 → open postgres console"
	@echo "  make db-reset           → recreate all databases"
	@echo ""
	@echo "  MIGRATIONS"
	@echo "  make migration name=X service=application-service"
	@echo "  make migrate service=application-service"
	@echo ""
	@echo "  KAFKA"
	@echo "  make kafka-topics       → list topics"
	@echo "  make kafka-create-topic topic=X"
	@echo "  make kafka-consume topic=X"
	@echo "  make kafka-ui           → http://localhost:8090"
	@echo ""
	@echo "  CLEANUP"
	@echo "  make clean              → prune unused images"
	@echo "  make clean-all          → remove all containers + volumes + images"
