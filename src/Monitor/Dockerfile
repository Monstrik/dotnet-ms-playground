# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/Monitor/Monitor.csproj", "src/Monitor/"]
RUN dotnet restore "src/Monitor/Monitor.csproj"

COPY . .
RUN dotnet publish "src/Monitor/Monitor.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Monitor.dll"]

