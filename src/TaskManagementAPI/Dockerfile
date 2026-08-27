# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/TaskManagementAPI/TaskManagementAPI.csproj", "src/TaskManagementAPI/"]
RUN dotnet restore "src/TaskManagementAPI/TaskManagementAPI.csproj"

COPY . .
RUN dotnet publish "src/TaskManagementAPI/TaskManagementAPI.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ---- runtime ----
# -noble = Ubuntu 24.04 base: has apt + a predefined non-root user ($APP_UID / "app").
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID

HEALTHCHECK --interval=30s --timeout=3s --start-period=20s --retries=3 \
    CMD curl -fsS http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "TaskManagementAPI.dll"]
