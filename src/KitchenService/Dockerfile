# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/KitchenService/KitchenService.csproj", "src/KitchenService/"]
RUN dotnet restore "src/KitchenService/KitchenService.csproj"

COPY . .
RUN dotnet publish "src/KitchenService/KitchenService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "KitchenService.dll"]

