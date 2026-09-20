FROM mcr.microsoft.com/dotnet/sdk:10.0.400 AS build
WORKDIR /source
COPY global.json ./
COPY src/Bookstore.Auth/Bookstore.Auth.csproj src/Bookstore.Auth/
RUN dotnet restore src/Bookstore.Auth/Bookstore.Auth.csproj
COPY src/Bookstore.Auth/ src/Bookstore.Auth/
RUN dotnet publish src/Bookstore.Auth/Bookstore.Auth.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.12 AS final
USER root
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /data-protection-keys && chown app:app /data-protection-keys
WORKDIR /app
COPY --from=build /app/publish ./
USER $APP_UID
EXPOSE 7200
ENTRYPOINT ["dotnet", "Bookstore.Auth.dll"]
