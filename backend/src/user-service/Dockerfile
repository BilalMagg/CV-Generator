FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore common-protos first
COPY src/common-protos/common-protos.csproj src/common-protos/
RUN dotnet restore src/common-protos/common-protos.csproj

# Restore this service
COPY src/user-service/UserService.csproj src/user-service/
RUN dotnet restore src/user-service/UserService.csproj

# Copy source
COPY src/common-protos src/common-protos
COPY src/user-service src/user-service

WORKDIR /src/src/user-service
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS="http://0.0.0.0:8082"
ENTRYPOINT ["dotnet", "UserService.dll"]
