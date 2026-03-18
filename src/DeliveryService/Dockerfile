# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/DeliveryService/DeliveryService.csproj", "src/DeliveryService/"]
RUN dotnet restore "src/DeliveryService/DeliveryService.csproj"

COPY . .
RUN dotnet publish "src/DeliveryService/DeliveryService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "DeliveryService.dll"]

