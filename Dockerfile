# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

# Create wwwroot and temp-reports folder, set permissions
RUN mkdir -p /app/wwwroot/temp-reports

# Copy ownership to the app user
# $APP_UID is passed by VS when building the container
RUN chown -R $APP_UID:$APP_UID /app

# Switch to non-root user AFTER permissions are fixed
USER $APP_UID

EXPOSE 8080
EXPOSE 8081


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["HFiles_Backend.API/HFiles_Backend.API.csproj", "HFiles_Backend.API/"]
COPY ["HFiles_Backend.Application/HFiles_Backend.Application.csproj", "HFiles_Backend.Application/"]
COPY ["HFiles_Backend.Domain/HFiles_Backend.Domain.csproj", "HFiles_Backend.Domain/"]
COPY ["HFiles_Backend.Infrastructure/HFiles_Backend.Infrastructure.csproj", "HFiles_Backend.Infrastructure/"]
RUN dotnet restore "./HFiles_Backend.API/HFiles_Backend.API.csproj"
COPY . .
WORKDIR "/src/HFiles_Backend.API"
RUN dotnet build "./HFiles_Backend.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./HFiles_Backend.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HFiles_Backend.API.dll"]
