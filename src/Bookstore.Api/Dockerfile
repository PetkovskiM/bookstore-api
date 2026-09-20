FROM mcr.microsoft.com/dotnet/sdk:10.0.400 AS build
WORKDIR /source
COPY global.json ./
COPY src/Bookstore.Api/Bookstore.Api.csproj src/Bookstore.Api/
RUN dotnet restore src/Bookstore.Api/Bookstore.Api.csproj
COPY src/Bookstore.Api/ src/Bookstore.Api/
RUN dotnet publish src/Bookstore.Api/Bookstore.Api.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.12 AS final
USER root
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish ./
USER $APP_UID
EXPOSE 7100
ENTRYPOINT ["dotnet", "Bookstore.Api.dll"]
