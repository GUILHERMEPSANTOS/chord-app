FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /src

COPY backend/src/ChordApp.Domain/ChordApp.Domain.csproj backend/src/ChordApp.Domain/
COPY backend/src/ChordApp.Infrastructure/ChordApp.Infrastructure.csproj backend/src/ChordApp.Infrastructure/
COPY backend/src/ChordApp.Application/ChordApp.Application.csproj backend/src/ChordApp.Application/
COPY backend/src/ChordApp.Api/ChordApp.Api.csproj backend/src/ChordApp.Api/

RUN dotnet restore backend/src/ChordApp.Api/ChordApp.Api.csproj

COPY backend/src/ChordApp.Domain/ backend/src/ChordApp.Domain/
COPY backend/src/ChordApp.Infrastructure/ backend/src/ChordApp.Infrastructure/
COPY backend/src/ChordApp.Application/ backend/src/ChordApp.Application/
COPY backend/src/ChordApp.Api/ backend/src/ChordApp.Api/

RUN dotnet publish backend/src/ChordApp.Api/ChordApp.Api.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0
RUN apt-get update && apt-get install -y --no-install-recommends ffmpeg && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /out .
ENTRYPOINT ["dotnet", "ChordApp.Api.dll"]
