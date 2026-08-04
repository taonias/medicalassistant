FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY backend ./backend
RUN dotnet restore backend/src/MedicalAssistant.Transcription.Worker/MedicalAssistant.Transcription.Worker.csproj
RUN dotnet publish backend/src/MedicalAssistant.Transcription.Worker/MedicalAssistant.Transcription.Worker.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MedicalAssistant.Transcription.Worker.dll"]
