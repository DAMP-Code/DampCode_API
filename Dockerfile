# syntax=docker/dockerfile:1

# Etapa de build: publica a API e gera o bundle das migrations (efbundle).
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY dotnet-tools.json ./
RUN dotnet tool restore

COPY DampCode_API/DampCode_API.csproj DampCode_API/
RUN dotnet restore DampCode_API/DampCode_API.csproj

COPY DampCode_API/ DampCode_API/
RUN dotnet publish DampCode_API/DampCode_API.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# O dotnet ef sobe a API para achar o DbContext: a connection string abaixo só existe no build e não conecta em nada.
RUN ConnectionStrings__PostgreSql="Host=build" dotnet ef migrations bundle \
    --project DampCode_API --startup-project DampCode_API \
    --configuration Release --output /app/publish/efbundle

# Etapa final: só o runtime do ASP.NET.
# A mesma imagem roda a API (padrão) e as migrations (comando ./efbundle, usado pelo Job do Kubernetes e pelo docker compose).
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

USER $APP_UID
ENTRYPOINT ["dotnet", "DampCode_API.dll"]
