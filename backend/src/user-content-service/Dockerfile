FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY src/common-protos/common-protos.csproj src/common-protos/
RUN dotnet restore src/common-protos/common-protos.csproj

COPY src/user-content-service/UserContentService.csproj src/user-content-service/
RUN dotnet restore src/user-content-service/UserContentService.csproj

COPY src/user-content-service src/user-content-service/

WORKDIR /src/src/user-content-service
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS="http://0.0.0.0:8083"
ENTRYPOINT ["dotnet", "UserContentService.dll"]