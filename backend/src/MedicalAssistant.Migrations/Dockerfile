FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY backend ./backend
RUN dotnet restore backend/src/MedicalAssistant.Migrations/MedicalAssistant.Migrations.csproj
RUN dotnet publish backend/src/MedicalAssistant.Migrations/MedicalAssistant.Migrations.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MedicalAssistant.Migrations.dll"]
