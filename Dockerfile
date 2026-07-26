# One image that serves BOTH the API and the built React PWA (same origin — simplest, robust cookie auth).
# Build context = repo root.

# 1) Build the React PWA → static files
FROM node:20-alpine AS frontend
WORKDIR /fe
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

# 2) Build + publish the .NET API (module DLLs are included so ModuleLoader can discover them)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend
WORKDIR /src
COPY backend/ ./
RUN dotnet restore src/HomeOs.Api/HomeOs.Api.csproj
RUN dotnet publish src/HomeOs.Api/HomeOs.Api.csproj -c Release -o /app /p:UseAppHost=false

# 3) Runtime: API + SPA in wwwroot
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=backend /app ./
COPY --from=frontend /fe/dist ./wwwroot

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["dotnet", "HomeOs.Api.dll"]
