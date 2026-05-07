FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore shared library
COPY src/shared/CVGenerator.Shared.csproj src/shared/
RUN dotnet restore src/shared/CVGenerator.Shared.csproj

# Restore this service
COPY src/cv-service/CvService.csproj src/cv-service/
RUN dotnet restore src/cv-service/CvService.csproj

# Copy source
COPY src/shared src/shared
COPY src/cv-service src/cv-service

WORKDIR /src/src/cv-service
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS="http://0.0.0.0:8088"
ENTRYPOINT ["dotnet", "CvService.dll"]
