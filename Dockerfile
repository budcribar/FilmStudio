# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY host/FilmStudio.Core/FilmStudio.Core.csproj host/FilmStudio.Core/
COPY host/FilmStudio.Engine/FilmStudio.Engine.csproj host/FilmStudio.Engine/
COPY host/FilmStudio.Fakes/FilmStudio.Fakes.csproj host/FilmStudio.Fakes/
COPY host/FilmStudio.Api/FilmStudio.Api.csproj host/FilmStudio.Api/
RUN dotnet restore host/FilmStudio.Api/FilmStudio.Api.csproj

# Copy remaining source code
COPY host/ host/
WORKDIR /src/host/FilmStudio.Api
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Install ffmpeg and font dependencies for Linux container
RUN apt-get update && apt-get install -y --no-install-recommends \
    ffmpeg \
    fonts-dejavu-core \
    fontconfig \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Environment defaults
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "FilmStudio.Api.dll"]
