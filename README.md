# PortalApi

Backend do Portal do Cliente (site institucional `nelson-proenca-info.com.br`), ver [spec completa (issue #12)](https://github.com/nelsonproenca/portal-web/issues/12).

Este repositório cobre o ticket [#13](https://github.com/nelsonproenca/portal-web/issues/13) e os
seguintes da mesma spec.

## Stack

- .NET 10 / ASP.NET Core Minimal APIs, Clean Architecture (`Domain` / `Application` / `Infrastructure` / `Api`)
- EF Core + `Pomelo.EntityFrameworkCore.MySql` — MySQL **self-hosted na própria VPS** (container Docker,
  não mais um plano de hospedagem separado — ver nota abaixo)

> **Mudança de decisão:** o plano original (issue #12/#13) era usar MySQL de um plano de hospedagem
> separado (Locaweb). Na prática, hospedagem compartilhada da Locaweb não permite acesso remoto ao
> MySQL (limitação do plano, não configuração pendente). Decisão revisada: MySQL roda como container
> na mesma VPS do backend, eliminando essa dependência externa.

## Rodando localmente

```bash
cp .env.example .env   # ajuste as senhas
docker compose up -d
dotnet build
dotnet run --project src/PortalApi.Api
```

O `appsettings.Development.json` já aponta pro MySQL do `docker-compose.yml` local (`localhost:3306`,
mesmas credenciais do `.env.example` — ajuste se você mudou o `.env`).

- Health check: `GET /health` — confirma conexão real com o banco (não só que o processo subiu)
- Docs (dev only): `/scalar` (Scalar UI sobre o OpenAPI)

## Migrations (EF Core)

```bash
dotnet tool restore
dotnet ef migrations add <Nome> --project src/PortalApi.Infrastructure --startup-project src/PortalApi.Api
dotnet ef database update --project src/PortalApi.Infrastructure --startup-project src/PortalApi.Api
```

## Deploy

Roda na VPS via `docker-compose.yml` deste repo — dois containers (`portal-api` +
`portal-mysql`). O Caddy real da VPS não é do Watchtower — é um container (`n8n-caddy-1`) definido em
`/opt/n8n/docker-compose.yml`, na rede Docker `n8n_default`, e alcança outros serviços pelo **nome do
container**, não por `localhost` (é assim que ele já fala com `watchtower-stack-watchtower-api-1:5000`
hoje). Por isso `portal-api` entra também na rede `n8n_default` (externa, `networks: n8n_default:
external: true` no compose) — o Caddy passa a alcançá-lo em `portal-api:8080`.

- Na VPS: clonar este repo, criar `.env` (nunca commitado — mesmas chaves do `.env.example`, com
  senhas de produção reais), `docker compose up -d --build`
- `portal-mysql` não entra na rede `n8n_default` — só `portal-api` acessa, via rede interna (default)
  do próprio compose. Isolado de qualquer outro stack da VPS.
- No Caddyfile (`/opt/n8n/Caddyfile`), dentro do bloco `www.nelson-proenca-info.com.br { ... }`
  (mesmo padrão dos blocos `handle /api/*` e `handle /hls/*` já existentes ali):

  ```caddy
  handle_path /api/portal/* {
      reverse_proxy portal-api:8080
  }
  ```

  `handle_path` (não `handle`) porque as rotas da API não conhecem esse prefixo — o health check é
  `/health`, não `/api/portal/health`. Depois de editar, `docker restart n8n-caddy-1` (ou
  `docker compose restart caddy` de dentro de `/opt/n8n`) pra recarregar.
- Dado persiste em volume Docker nomeado (`portal-mysql-data`) — sobrevive a `docker compose down`
  (sem `-v`) e a updates da imagem

### Pendências de infraestrutura (fora do que este código resolve sozinho)

- Clonar o repo na VPS e criar o `.env` de produção com senhas reais
- Adicionar o bloco `handle_path /api/portal/*` no `/opt/n8n/Caddyfile` (acima) e reiniciar o Caddy
- O volume `portal-mysql-data` fica no disco da própria VPS, então o snapshot/backup de máquina
  inteira que o provedor da VPS já faz (mesma decisão tomada pra `artefatos` na spec) cobre o banco
  também — confirmar que isso realmente inclui volumes Docker, não só o filesystem "principal"
