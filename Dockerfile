# Build stage. A single `dotnet publish` produces everything the runtime image needs; the
# separate `dotnet build` this file used to run compiled the project a second time and its
# output was never copied anywhere.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG VERSION=0.0.0-dev
WORKDIR /src

# Restore against the project file alone so the layer is cached until dependencies change.
COPY ["src/TargetApiSimulator/TargetApiSimulator.csproj", "src/TargetApiSimulator/"]
RUN dotnet restore "src/TargetApiSimulator/TargetApiSimulator.csproj"

COPY . .

# The version has to be passed in: .dockerignore excludes .git, so SourceLink cannot derive it
# and /version would otherwise always report 1.0.0.
RUN dotnet publish "src/TargetApiSimulator/TargetApiSimulator.csproj" \
    --configuration $BUILD_CONFIGURATION \
    --no-restore \
    -p:UseAppHost=false \
    -p:Version=$VERSION \
    -p:InformationalVersion=$VERSION \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
ARG VERSION=0.0.0-dev

LABEL org.opencontainers.image.title="TargetApiSimulator" \
      org.opencontainers.image.description="Stub API that validates whether a POSTed body is well-formed JSON." \
      org.opencontainers.image.source="https://github.com/Mysttic/TargetApiSimulator" \
      org.opencontainers.image.licenses="MIT" \
      org.opencontainers.image.version="${VERSION}"

WORKDIR /app
COPY --from=build /app/publish .

# Drop root. The base image defines APP_UID=1654; 8080 is above 1024, so no capability is needed
# to bind it.
USER $APP_UID

# Only the plain HTTP port. The previous EXPOSE 8081 advertised an HTTPS listener that never
# existed, because no certificate is provisioned in this image.
EXPOSE 8080

ENTRYPOINT ["dotnet", "TargetApiSimulator.dll"]
