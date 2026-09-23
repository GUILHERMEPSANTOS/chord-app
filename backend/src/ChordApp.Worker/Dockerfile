FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY backend/src/ChordApp.Domain/ChordApp.Domain.csproj backend/src/ChordApp.Domain/
COPY backend/src/ChordApp.Infrastructure/ChordApp.Infrastructure.csproj backend/src/ChordApp.Infrastructure/
COPY backend/src/ChordApp.Worker/ChordApp.Worker.csproj backend/src/ChordApp.Worker/
RUN dotnet restore backend/src/ChordApp.Worker/ChordApp.Worker.csproj
COPY backend/src/ChordApp.Domain/ backend/src/ChordApp.Domain/
COPY backend/src/ChordApp.Infrastructure/ backend/src/ChordApp.Infrastructure/
COPY backend/src/ChordApp.Worker/ backend/src/ChordApp.Worker/
RUN dotnet publish backend/src/ChordApp.Worker/ChordApp.Worker.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/runtime:9.0
WORKDIR /app
COPY --from=build /out .
ENTRYPOINT ["dotnet", "ChordApp.Worker.dll"]
