# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/WeatherService/WeatherService.csproj", "src/WeatherService/"]
RUN dotnet restore "src/WeatherService/WeatherService.csproj"

COPY . .
RUN dotnet publish "src/WeatherService/WeatherService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "WeatherService.dll"]

