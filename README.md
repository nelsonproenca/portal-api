# PortalApi

Backend do Portal do Cliente (site institucional `nelson-proenca-info.com.br`) — projeto independente do
Watchtower, ver [spec completa (issue #12)](https://github.com/nelsonproenca/ai-agent-playground/issues/12).

Este repositório cobre o ticket [#13](https://github.com/nelsonproenca/ai-agent-playground/issues/13) e os
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

Roda na VPS (209.61.37.142) via `docker-compose.yml` deste repo — dois containers (`portal-api` +
`portal-mysql`), independentes da stack Docker/Caddy do Watchtower (que continua com seu próprio
banco Postgres/Supabase, sem relação com este).

- Na VPS: `git clone` deste repo, criar `.env` (nunca commitado — mesmas chaves do `.env.example`,
  com senhas de produção reais), `docker compose up -d --build`
- `portal-api` expõe `127.0.0.1:5299` (loopback only) — o Caddy que já roda na VPS faz reverse proxy
  pra lá e cuida do domínio/TLS
- Exposto publicamente em `nelson-proenca-info.com.br/api/portal/*` — o Caddy deve usar
  `handle_path /api/portal/*` (remove o prefixo antes de repassar), já que as rotas da API não
  conhecem esse prefixo (ex: o health check é só `/health`, não `/api/portal/health`)
- `portal-mysql` não expõe porta pro host — só o `portal-api` acessa, via rede interna do compose
- Dado persiste em volume Docker nomeado (`portal-mysql-data`) — sobrevive a `docker compose down`
  (sem `-v`) e a updates da imagem

### Pendências de infraestrutura (fora do que este código resolve sozinho)

- Clonar o repo na VPS e criar o `.env` de produção com senhas reais
- Adicionar o bloco de rota no Caddy existente na VPS (sem compose/Caddyfile versionado neste repo
  pro Watchtower — feito direto por SSH, como já acontece com o `watchtower-api`)
- O volume `portal-mysql-data` fica no disco da própria VPS, então o snapshot/backup de máquina
  inteira que o provedor da VPS já faz (mesma decisão tomada pra `artefatos` na spec) cobre o banco
  também — confirmar que isso realmente inclui volumes Docker, não só o filesystem "principal"
