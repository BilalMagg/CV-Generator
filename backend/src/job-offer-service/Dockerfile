FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 1. Restore shared library (must be done from the root context)
COPY src/shared/CVGenerator.Shared.csproj src/shared/
RUN dotnet restore src/shared/CVGenerator.Shared.csproj

# 2. Restore the Job Offer Service
COPY src/job-offer-service/job-offer-service.csproj src/job-offer-service/
RUN dotnet restore src/job-offer-service/job-offer-service.csproj

# 3. Copy the actual source code for both
COPY src/shared src/shared
COPY src/job-offer-service src/job-offer-service

# 4. Move into the service folder to build
WORKDIR /src/src/job-offer-service
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
# Added the Kafka linux dependency since your service uses Kafka
RUN apt-get update && apt-get install -y libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS="http://0.0.0.0:8086"
EXPOSE 8086
ENTRYPOINT ["dotnet", "job-offer-service.dll"]