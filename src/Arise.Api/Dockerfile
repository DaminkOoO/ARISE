# Image de l'API ARISE.
# ATTENTION : le contexte de build est la RACINE du dépôt (voir docker-compose.yml),
# car les projets se référencent entre eux et Directory.Build.props centralise le
# framework cible à la racine. Ne pas builder depuis src/Arise.Api/.

# ---- Étape de build : SDK complet pour restaurer et publier ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# On copie d'abord les seuls manifestes (.csproj + Directory.Build.props) pour que la
# couche de restore soit mise en cache tant que les dépendances ne changent pas.
COPY Directory.Build.props ./
COPY src/Arise.Domain/Arise.Domain.csproj src/Arise.Domain/
COPY src/Arise.Application/Arise.Application.csproj src/Arise.Application/
COPY src/Arise.Infrastructure/Arise.Infrastructure.csproj src/Arise.Infrastructure/
COPY src/Arise.Api/Arise.Api.csproj src/Arise.Api/
RUN dotnet restore src/Arise.Api/Arise.Api.csproj

# Puis le reste des sources, et on publie sans re-restaurer.
COPY src/ src/
RUN dotnet publish src/Arise.Api/Arise.Api.csproj -c Release -o /app/publish --no-restore

# ---- Étape d'exécution : runtime ASP.NET seul, sans le SDK ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# L'API écoute en HTTP sur 5006 (aligné sur launchSettings.json).
ENV ASPNETCORE_HTTP_PORTS=5006
EXPOSE 5006

COPY --from=build /app/publish .

# Utilisateur non-root fourni par l'image runtime (APP_UID = 1654).
USER $APP_UID

ENTRYPOINT ["dotnet", "Arise.Api.dll"]
