FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY src/OrderService.Domain/OrderService.Domain.csproj src/OrderService.Domain/
COPY src/OrderService.Application/OrderService.Application.csproj src/OrderService.Application/
COPY src/OrderService.Infrastructure/OrderService.Infrastructure.csproj src/OrderService.Infrastructure/
COPY src/OrderService.Api/OrderService.Api.csproj src/OrderService.Api/

RUN dotnet restore src/OrderService.Api/OrderService.Api.csproj

COPY src/ ./src/
RUN dotnet publish src/OrderService.Api/OrderService.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OrderService.Api.dll"]
