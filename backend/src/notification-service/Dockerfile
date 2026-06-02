FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore common-protos first (needed by notification-service)
COPY src/common-protos/common-protos.csproj src/common-protos/
RUN dotnet restore src/common-protos/common-protos.csproj

# Restore shared library
COPY src/shared/CVGenerator.Shared.csproj src/shared/
RUN dotnet restore src/shared/CVGenerator.Shared.csproj

# Restore this service
COPY src/notification-service/notification-service.csproj src/notification-service/
RUN dotnet restore src/notification-service/notification-service.csproj

# Copy source
COPY src/common-protos src/common-protos
COPY src/shared src/shared
COPY src/notification-service src/notification-service

WORKDIR /src/src/notification-service
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV PORT=8087
ENV ASPNETCORE_URLS="http://0.0.0.0:${PORT}"
EXPOSE ${PORT}
ENTRYPOINT ["dotnet", "notification-service.dll"]
