FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

# CACHE BUSTING - Force fresh build on Render.com
ARG CACHE_BUST=20250723002500
RUN echo "Cache invalidation: $CACHE_BUST - Finshark SVG sized to 72x31" > /tmp/cache_bust.txt

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["WebCashier/WebCashier.csproj", "WebCashier/"]
RUN dotnet restore "WebCashier/WebCashier.csproj"
COPY . .
WORKDIR "/src/WebCashier"
RUN dotnet build "WebCashier.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "WebCashier.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
RUN adduser --disabled-password --home /app --gecos '' appuser && chown -R appuser /app
USER appuser
ENTRYPOINT ["dotnet", "WebCashier.dll"]