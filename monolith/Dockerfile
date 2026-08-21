FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app
COPY backend/CV_Generator.csproj .
RUN dotnet restore
COPY backend/ .
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000
ENTRYPOINT ["dotnet", "CV_Generator.dll"]
