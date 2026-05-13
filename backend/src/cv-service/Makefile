build:
	docker compose build

up:
	docker compose up -d

down:
	docker compose down

logs:
	docker compose logs -f

restart: down up

migrate:
	dotnet ef migrations add InitialCreate --project CvService.csproj
	dotnet ef database update --project CvService.csproj
