# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/OrderProducerService/OrderProducerService.csproj", "src/OrderProducerService/"]
RUN dotnet restore "src/OrderProducerService/OrderProducerService.csproj"

COPY . .
RUN dotnet publish "src/OrderProducerService/OrderProducerService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "OrderProducerService.dll"]

