FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore common-protos first
COPY src/common-protos/common-protos.csproj src/common-protos/
RUN dotnet restore src/common-protos/common-protos.csproj

# Restore shared library
COPY src/shared/CVGenerator.Shared.csproj src/shared/
RUN dotnet restore src/shared/CVGenerator.Shared.csproj

# Restore this service
COPY src/user-content-service/UserContentService.csproj src/user-content-service/
RUN dotnet restore src/user-content-service/UserContentService.csproj

# Copy source
COPY src/common-protos src/common-protos
COPY src/shared src/shared
COPY src/user-content-service src/user-content-service

WORKDIR /src/src/user-content-service
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV PORT=8083
ENV GRPC_PORT=18083
ENV ASPNETCORE_URLS="http://0.0.0.0:${PORT}"
EXPOSE ${PORT}
EXPOSE ${GRPC_PORT}
ENTRYPOINT ["dotnet", "UserContentService.dll"]
