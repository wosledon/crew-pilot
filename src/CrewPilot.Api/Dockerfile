# syntax=docker/dockerfile:1

# ---- Build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY CrewPilot.slnx ./
COPY src/CrewPilot.Core/CrewPilot.Core.csproj src/CrewPilot.Core/
COPY src/CrewPilot.Infrastructure/CrewPilot.Infrastructure.csproj src/CrewPilot.Infrastructure/
COPY src/CrewPilot.Agents/CrewPilot.Agents.csproj src/CrewPilot.Agents/
COPY src/CrewPilot.Api/CrewPilot.Api.csproj src/CrewPilot.Api/
RUN dotnet restore src/CrewPilot.Api/CrewPilot.Api.csproj

COPY src/ src/
RUN dotnet publish src/CrewPilot.Api/CrewPilot.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

# ---- Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# 数据目录（SQLite + 附件），compose 中挂载为卷
RUN mkdir -p /app/App_Data

COPY --from=build /app/publish ./
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    Database__AutoMigrate=true \
    Attachments__RootPath=/app/App_Data/attachments
EXPOSE 8080
VOLUME ["/app/App_Data"]
ENTRYPOINT ["dotnet", "CrewPilot.Api.dll"]
