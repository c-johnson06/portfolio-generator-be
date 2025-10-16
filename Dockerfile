# Use the official .NET 8 SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
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
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Portfolio-Generator.api.dll"]
