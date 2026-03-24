# ----------- Build Stage -----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copy csproj first to leverage Docker cache for restoring dependencies
COPY *.csproj ./  

# Restore dependencies
RUN dotnet restore

# Copy the rest of the source code (including .env)
COPY . .

# Publish the application (release mode)
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# ----------- Runtime Stage -----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Copy the published app from the build stage
COPY --from=build /app/publish .

# Expose port (optional, if your app uses a specific port)
EXPOSE 80
EXPOSE 443

# Set the entrypoint
ENTRYPOINT ["dotnet", "backend.dll"]