# Use the official .NET 8 SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["Portfolio-Generator.api.csproj", "./"]
RUN dotnet restore "./Portfolio-Generator.api.csproj"

# Copy everything else and build the app
COPY . .
RUN dotnet publish "./Portfolio-Generator.api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install Chromium dependencies (needed by PuppeteerSharp)
RUN apt-get update && apt-get install -y \
    wget gnupg ca-certificates fonts-liberation \
    libappindicator3-1 libasound2 libatk-bridge2.0-0 libatk1.0-0 \
    libc6 libcairo2 libcups2 libdbus-1-3 libexpat1 libfontconfig1 \
    libgbm1 libglib2.0-0 libgtk-3-0 libnspr4 libnss3 libpango-1.0-0 \
    libpangocairo-1.0-0 libstdc++6 libx11-6 libx11-xcb1 libxcb1 \
    libxcomposite1 libxcursor1 libxdamage1 libxext6 libxfixes3 \
    libxi6 libxrandr2 libxrender1 libxss1 libxtst6 lsb-release xdg-utils \
    && rm -rf /var/lib/apt/lists/*

# Install Chromium for PuppeteerSharp
RUN apt-get update && apt-get install -y chromium && rm -rf /var/lib/apt/lists/*
ENV PUPPETEER_EXECUTABLE_PATH=/usr/bin/chromium

RUN useradd -m appuser
USER appuser

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Portfolio-Generator.api.dll"]
