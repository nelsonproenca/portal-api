FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY PortalApi.slnx ./
COPY src/PortalApi.Domain/PortalApi.Domain.csproj src/PortalApi.Domain/
COPY src/PortalApi.Application/PortalApi.Application.csproj src/PortalApi.Application/
COPY src/PortalApi.Infrastructure/PortalApi.Infrastructure.csproj src/PortalApi.Infrastructure/
COPY src/PortalApi.Api/PortalApi.Api.csproj src/PortalApi.Api/
RUN dotnet restore src/PortalApi.Api/PortalApi.Api.csproj

COPY src/ src/
RUN dotnet publish src/PortalApi.Api/PortalApi.Api.csproj -c Release -o /app --no-restore -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "PortalApi.Api.dll"]
