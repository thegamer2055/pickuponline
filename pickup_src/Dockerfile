# Build the ASP.NET Core 8 SignalR server and its shared game library.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY PickUp.Core/PickUp.Core.csproj PickUp.Core/
COPY PickUp.Server/PickUp.Server.csproj PickUp.Server/
RUN dotnet restore PickUp.Server/PickUp.Server.csproj
COPY . .
RUN dotnet publish PickUp.Server/PickUp.Server.csproj -c Release -o /app/publish /p:UseAppHost=false --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=10000
EXPOSE 10000
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PickUp.Server.dll"]
