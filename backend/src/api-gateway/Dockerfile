FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY src/api-gateway/ApiGateway.csproj src/api-gateway/
RUN dotnet restore src/api-gateway/ApiGateway.csproj

COPY src/api-gateway src/api-gateway/

WORKDIR /src/src/api-gateway
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS="http://0.0.0.0:8080"
ENTRYPOINT ["dotnet", "ApiGateway.dll"]