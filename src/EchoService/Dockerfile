# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/EchoService/EchoService.csproj", "src/EchoService/"]
RUN dotnet restore "src/EchoService/EchoService.csproj"

COPY . .
RUN dotnet publish "src/EchoService/EchoService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "EchoService.dll"]

