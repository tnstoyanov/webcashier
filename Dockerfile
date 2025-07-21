# Use the official .NET 9.0 runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

# Use the .NET 9.0 SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY ["WebCashier/WebCashier.csproj", "WebCashier/"]
RUN dotnet restore "WebCashier/WebCashier.csproj"

# Copy all source files
COPY . .
WORKDIR "/src/WebCashier"

# Build the application
RUN dotnet build "WebCashier.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "WebCashier.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage - runtime image
FROM base AS final
WORKDIR /app

# Copy published application
COPY --from=publish /app/publish .

# Set environment variables for production
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

# Create a non-root user for security
RUN adduser --disabled-password --home /app --gecos '' appuser && chown -R appuser /app
USER appuser

ENTRYPOINT ["dotnet", "WebCashier.dll"]
