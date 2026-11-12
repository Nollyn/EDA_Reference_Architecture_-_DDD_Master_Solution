FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY src/OrderService.Domain/OrderService.Domain.csproj src/OrderService.Domain/
COPY src/OrderService.Application/OrderService.Application.csproj src/OrderService.Application/
COPY src/OrderService.Infrastructure/OrderService.Infrastructure.csproj src/OrderService.Infrastructure/
COPY src/OrderService.Worker/OrderService.Worker.csproj src/OrderService.Worker/

RUN dotnet restore src/OrderService.Worker/OrderService.Worker.csproj

COPY src/ ./src/
RUN dotnet publish src/OrderService.Worker/OrderService.Worker.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OrderService.Worker.dll"]
