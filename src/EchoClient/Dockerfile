# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/EchoClient/EchoClient.csproj", "src/EchoClient/"]
RUN dotnet restore "src/EchoClient/EchoClient.csproj"

COPY . .
RUN dotnet publish "src/EchoClient/EchoClient.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "EchoClient.dll"]

