FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/NekoHub.Api/NekoHub.Api.csproj", "src/NekoHub.Api/"]
COPY ["src/NekoHub.Application/NekoHub.Application.csproj", "src/NekoHub.Application/"]
COPY ["src/NekoHub.Domain/NekoHub.Domain.csproj", "src/NekoHub.Domain/"]
COPY ["src/NekoHub.Infrastructure/NekoHub.Infrastructure.csproj", "src/NekoHub.Infrastructure/"]
RUN dotnet restore "src/NekoHub.Api/NekoHub.Api.csproj"
COPY src ./src
WORKDIR "/src/src/NekoHub.Api"
RUN dotnet build "./NekoHub.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./NekoHub.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NekoHub.Api.dll"]
