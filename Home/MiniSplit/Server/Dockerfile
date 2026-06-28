FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY Fobelity.sln ./
COPY Home/MiniSplit/Domain/ Home/MiniSplit/Domain/
COPY Home/MiniSplit/Rules/ Home/MiniSplit/Rules/
COPY Home/MiniSplit/Backend/ Home/MiniSplit/Backend/
COPY Home/MiniSplit/Server/ Home/MiniSplit/Server/
COPY Home/MiniSplit/Service/ Home/MiniSplit/Service/
COPY Home/MiniSplit/Client/ Home/MiniSplit/Client/
COPY Home/MiniSplit/Shared/ Home/MiniSplit/Shared/
COPY Home/MiniSplit/Temperature/ Home/MiniSplit/Temperature/
COPY Home/ServiceDefaults/ Home/ServiceDefaults/
COPY Home/Artificial/McpServer/ Home/Artificial/McpServer/
COPY Home/Artificial/McpTools/ Home/Artificial/McpTools/
COPY Home/Artificial/AIClient/ Home/Artificial/AIClient/
COPY Home/Artificial/McpLocal/ Home/Artificial/McpLocal/

WORKDIR /src/Home/MiniSplit/Server
RUN dotnet restore "Fobelity.Home.MiniSplit.Server.csproj"
RUN dotnet publish "Fobelity.Home.MiniSplit.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Fobelity.Home.MiniSplit.Server.dll"]