# SafeLearn Kids web app. Build:  docker build -t safelearnkids .
# Runs as a non-root user on port 8080; put it behind the Caddy service in docker-compose.yml for HTTPS.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Restore first so package downloads are cached between builds.
COPY SafeLearnKids.Web.csproj ./
RUN dotnet restore SafeLearnKids.Web.csproj
# Front-end libraries (bootstrap, jquery validation) come from libman.json, they are not in Git.
RUN dotnet tool install --tool-path /tools Microsoft.Web.LibraryManager.Cli --version 3.0.114
COPY libman.json ./
RUN /tools/libman restore
COPY . .
RUN dotnet publish SafeLearnKids.Web.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
# Study time, daily limits and weekly reports follow the families' clock (Vietnam).
ENV TZ=Asia/Ho_Chi_Minh \
    ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DataProtection__KeysPath=/app/App_Data/keys
RUN apt-get update \
    && apt-get install -y --no-install-recommends tzdata \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app ./
# Uploaded files and sign-in keys live on volumes (see docker-compose.yml); the app user must be able to write them.
RUN mkdir -p /app/wwwroot/uploads /app/App_Data/keys && chown -R $APP_UID /app/wwwroot/uploads /app/App_Data
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "SafeLearnKids.Web.dll"]
