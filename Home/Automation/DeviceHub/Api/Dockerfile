
# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Home/Automation/DeviceHub/Api/Fobelity.Home.Automation.DeviceHub.Api.csproj", "Home/Automation/DeviceHub/Api/"]
COPY ["Home/Automation/Services/Lights.Service/Fobelity.Home.Automation.Lights.Service.csproj", "Home/Automation/Services/Lights.Service/"]
COPY ["Home/ServiceDefaults/Fobelity.Home.ServiceDefaults.csproj", "Home/ServiceDefaults/"]
COPY ["Home/Automation/Adapters/Seam/Fobelity.Home.Automation.Adapters.Seam.csproj", "Home/Automation/Adapters/Seam/"]
COPY ["Home/Automation/CrossCutting/Observability/Fobelity.Home.Automation.Observability.csproj", "Home/Automation/CrossCutting/Observability/"]
COPY ["Home/Automation/DeviceHub/Core/Fobelity.Home.Automation.DeviceHub.Core.csproj", "Home/Automation/DeviceHub/Core/"]
COPY ["Home/Automation/Adapters/Tuya/Fobelity.Home.Automation.Adapters.Tuya.csproj", "Home/Automation/Adapters/Tuya/"]
COPY ["Home/Automation/CrossCutting/Security/Fobelity.Home.Automation.Security.csproj", "Home/Automation/CrossCutting/Security/"]
RUN dotnet restore "./Home/Automation/DeviceHub/Api/Fobelity.Home.Automation.DeviceHub.Api.csproj"
COPY . .
WORKDIR "/src/Home/Automation/DeviceHub/Api"
RUN dotnet build "./Fobelity.Home.Automation.DeviceHub.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Fobelity.Home.Automation.DeviceHub.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Fobelity.Home.Automation.DeviceHub.Api.dll"]