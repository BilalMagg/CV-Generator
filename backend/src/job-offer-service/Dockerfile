FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copy project file and restore dependencies
COPY job-offer-service.csproj ./
RUN dotnet restore job-offer-service.csproj

# Copy the rest of the source code
COPY . ./
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS="http://0.0.0.0:8086"
EXPOSE 8086
ENTRYPOINT ["dotnet", "job-offer-service.dll"]
