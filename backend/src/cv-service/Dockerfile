FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore common-protos first
COPY src/common-protos/common-protos.csproj src/common-protos/
RUN dotnet restore src/common-protos/common-protos.csproj

# Restore shared library
COPY src/shared/CVGenerator.Shared.csproj src/shared/
RUN dotnet restore src/shared/CVGenerator.Shared.csproj

# Restore this service
COPY src/cv-service/CvService.csproj src/cv-service/
RUN dotnet restore src/cv-service/CvService.csproj

# Copy source
COPY src/common-protos src/common-protos
COPY src/shared src/shared
COPY src/cv-service src/cv-service

WORKDIR /src/src/cv-service
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV PORT=8088
ENV GRPC_PORT=18088
ENV ASPNETCORE_URLS="http://0.0.0.0:${PORT}"
EXPOSE ${PORT}
EXPOSE ${GRPC_PORT}
ENTRYPOINT ["dotnet", "CvService.dll"]
