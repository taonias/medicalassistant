FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY AI ./AI
RUN dotnet restore AI/src/MedicalAssistance.Ingestion.Api/MedicalAssistance.Ingestion.Api.csproj
RUN dotnet publish AI/src/MedicalAssistance.Ingestion.Api/MedicalAssistance.Ingestion.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=5s --retries=18 --start-period=45s \
    CMD curl --fail http://localhost:8080/swagger/v1/swagger.json || exit 1
ENTRYPOINT ["dotnet", "MedicalAssistance.Ingestion.Api.dll"]
