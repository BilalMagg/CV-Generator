FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore shared library
COPY src/shared/CVGenerator.Shared.csproj src/shared/
RUN dotnet restore src/shared/CVGenerator.Shared.csproj

# Restore this service
COPY src/application-service/ApplicationService.csproj src/application-service/
RUN dotnet restore src/application-service/ApplicationService.csproj

# Copy source
COPY src/shared src/shared
COPY src/application-service src/application-service

WORKDIR /src/src/application-service
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN apt-get update && apt-get install -y libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS="http://0.0.0.0:8085"
ENTRYPOINT ["dotnet", "ApplicationService.dll"]
