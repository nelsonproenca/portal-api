# PortalApi

Backend do Portal do Cliente (site institucional `nelson-proenca-info.com.br`) — projeto independente do
Watchtower, ver [spec completa (issue #12)](https://github.com/nelsonproenca/ai-agent-playground/issues/12).

Este repositório cobre o ticket [#13](https://github.com/nelsonproenca/ai-agent-playground/issues/13) e os
seguintes da mesma spec.

## Stack

- .NET 10 / ASP.NET Core Minimal APIs, Clean Architecture (`Domain` / `Application` / `Infrastructure` / `Api`)
- EF Core + `Pomelo.EntityFrameworkCore.MySql` (MySQL na hospedagem separada, fora da VPS)

## Rodando localmente

Requer uma instância MySQL acessível (local ou remota). Ajuste `ConnectionStrings:PortalDb` em
`src/PortalApi.Api/appsettings.Development.json` (não commitado com credenciais reais — o valor atual
é um placeholder).

```bash
dotnet build
dotnet run --project src/PortalApi.Api
```

- Health check: `GET /health` — confirma conexão real com o banco (não só que o processo subiu)
- Docs (dev only): `/scalar` (Scalar UI sobre o OpenAPI)

## Migrations (EF Core)

```bash
dotnet tool restore
dotnet ef migrations add <Nome> --project src/PortalApi.Infrastructure --startup-project src/PortalApi.Api
dotnet ef database update --project src/PortalApi.Infrastructure --startup-project src/PortalApi.Api
```

## Deploy

Container Docker novo, irmão do `watchtower-api`, na mesma stack Docker/Caddy da VPS (209.61.37.142).

- Build: `docker build -t portal-api .`
- Porta interna do container: `8080` (`ASPNETCORE_URLS=http://+:8080`)
- Exposto publicamente em `nelson-proenca-info.com.br/api/portal/*` — o Caddy deve usar
  `handle_path /api/portal/*` (que remove o prefixo antes de repassar), já que as rotas da API não
  conhecem esse prefixo (ex: o health check é só `/health`, não `/api/portal/health`)
- `appsettings.Production.json` (com a connection string real) vive **só na VPS**, nunca no repositório
  — mesma convenção do backend do Watchtower. Precisa conter:
  ```json
  { "ConnectionStrings": { "PortalDb": "Server=<host>;Port=3306;Database=<db>;User=<user>;Password=<senha>;" } }
  ```

### Pendências de infraestrutura (fora do que este código resolve sozinho)

- Criar o banco/usuário MySQL no painel da hospedagem separada e liberar acesso remoto pro IP da VPS
  (hosts desse tipo costumam bloquear conexão remota por padrão)
- Adicionar o container novo na stack Docker/Caddy existente na VPS (sem compose file versionado neste
  repo — feito direto por SSH, como já acontece com o `watchtower-api`)
