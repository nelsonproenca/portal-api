#!/usr/bin/env bash
# Roda NA VPS (PuTTY), na pasta do portal-api (/opt/portal-backend-src). Subcomandos:
#
#   bash scripts/vps-portal.sh set-n8n-secret   # grava N8N_WEBHOOK_SECRET no .env (digitado sem eco) e recria o portal-api
#   bash scripts/vps-portal.sh import [--apply] # importa o backup do Supabase (padrão: simulação, nada é gravado)
#   bash scripts/vps-portal.sh smoke            # confere endpoints, contagens das tabelas e o isolamento do admin
#
# Nunca imprime segredos nem conteúdo de dados (só códigos HTTP e contagens). O backup precisa estar em
# $BACKUP_DIR (padrão /root/portal-import, com as pastas tables/ e uploads/) e deve ser APAGADO depois do import.

set -euo pipefail

PORTAL_DIR=${PORTAL_DIR:-/opt/portal-backend-src}
ENV_FILE="$PORTAL_DIR/.env"
BACKUP_DIR=${BACKUP_DIR:-/root/portal-import}
LOCAL_URL=${LOCAL_URL:-http://127.0.0.1:5299}

fail() { echo "ERRO: $*" >&2; exit 1; }
ok()   { echo "OK:   $*"; }

cd "$PORTAL_DIR" || fail "pasta $PORTAL_DIR não existe"
test -f "$ENV_FILE" || fail "$ENV_FILE não existe"

case "${1:-}" in

set-n8n-secret)
  # O mesmo valor precisa estar na credencial "SiteNPI Webhook Secret" do n8n (Header Auth, nome X-Webhook-Secret).
  read -rs -p "Cole o segredo (o mesmo da credencial do n8n; não aparece na tela): " SECRET
  echo
  test -n "$SECRET" || fail "segredo vazio"
  test "${#SECRET}" -ge 32 || fail "segredo curto demais (use 32+ caracteres)"
  case "$SECRET" in *[[:space:]]*|*'"'*|*"'"*|*'$'*|*'\'*) fail "o segredo não pode ter espaço, aspas, \$ nem barra invertida" ;; esac

  umask 077
  TMP=$(mktemp)
  grep -vE '^N8N_WEBHOOK_SECRET=' "$ENV_FILE" > "$TMP" || true
  { cat "$TMP"; echo "N8N_WEBHOOK_SECRET=$SECRET"; } > "$ENV_FILE"
  rm -f "$TMP"
  chmod 600 "$ENV_FILE"
  ok "N8N_WEBHOOK_SECRET gravado em $ENV_FILE (não exibido)"

  docker compose up -d --force-recreate portal-api || fail "docker compose falhou"
  ok "portal-api recriado com o segredo"
  echo "Próximo: bash scripts/vps-portal.sh smoke"
  ;;

import)
  APPLY=0; [ "${2:-}" = "--apply" ] && APPLY=1
  test -d "$BACKUP_DIR/tables" || fail "$BACKUP_DIR/tables não existe (copie as pastas tables/ e uploads/ do backup para lá)"
  FLAG="--dry-run"; [ "$APPLY" = "1" ] && FLAG=""
  echo "== import-crm ${FLAG:-(GRAVANDO)} =="
  docker compose run --rm --no-deps -v "$BACKUP_DIR":/backup:ro portal-api import-crm /backup $FLAG
  if [ -d "$BACKUP_DIR/uploads" ]; then
    echo "== import-uploads ${FLAG:-(GRAVANDO)} =="
    docker compose run --rm --no-deps -v "$BACKUP_DIR":/backup:ro portal-api import-uploads /backup $FLAG
  fi
  if [ "$APPLY" = "1" ]; then
    ok "importação concluída. Agora APAGUE o backup da VPS:  rm -rf $BACKUP_DIR"
  else
    echo "Simulação concluída. Se as contagens estiverem certas: bash scripts/vps-portal.sh import --apply"
  fi
  ;;

smoke)
  FALHAS=0
  # esperado = código HTTP; imprime só OK/FALHOU
  checa() {
    local nome=$1 esperado=$2; shift 2
    local code
    code=$(curl -s -o /dev/null -w '%{http_code}' --max-time 15 "$@") || code=000
    if [ "$code" = "$esperado" ]; then ok "$nome → $code"; else echo "FALHOU: $nome → $code (esperado $esperado)"; FALHAS=$((FALHAS+1)); fi
  }
  checa "GET /health"                         200 "$LOCAL_URL/health"
  checa "GET /colaboradores (público)"        200 "$LOCAL_URL/colaboradores"
  checa "GET /convites (público)"             200 "$LOCAL_URL/convites"
  checa "GET /leads sem login"                401 "$LOCAL_URL/leads"
  checa "GET /agendamentos sem login"         401 "$LOCAL_URL/agendamentos"
  checa "GET /contatos-clientes sem login"    401 "$LOCAL_URL/contatos-clientes"
  checa "GET /auth/cliente/me sem sessão"     401 "$LOCAL_URL/auth/cliente/me"
  checa "POST /agendamentos sem segredo"      401 -X POST -H 'Content-Type: application/json' -d '{}' "$LOCAL_URL/agendamentos"
  checa "PATCH callback sem segredo"          401 -X PATCH -H 'Content-Type: application/json' -d '{"analiseIa":"x"}' "$LOCAL_URL/leads/00000000-0000-0000-0000-000000000000/analise"
  checa "POST /auth/cliente/solicitar (e-mail inexistente, não envia nada)" 200 -X POST -H 'Content-Type: application/json' -d '{"email":"nao-existe@exemplo.invalid"}' "$LOCAL_URL/auth/cliente/solicitar"

  echo "== contagens (esperado: Clientes 2, Colaboradores 3, ContatosClientes 0, LeadsIa 5, Agendamentos 5, EnrichCompany 2, PlaygroundAnalises 0) =="
  docker compose exec -T portal-mysql sh -c 'mysql -u"$MYSQL_USER" -p"$MYSQL_PASSWORD" "$MYSQL_DATABASE" -N -e "
    select \"Clientes\", count(*) from Clientes union all
    select \"Colaboradores\", count(*) from Colaboradores union all
    select \"ContatosClientes\", count(*) from ContatosClientes union all
    select \"LeadsIa\", count(*) from LeadsIa union all
    select \"Agendamentos\", count(*) from Agendamentos union all
    select \"EnrichCompany\", count(*) from EnrichCompany union all
    select \"PlaygroundAnalises\", count(*) from PlaygroundAnalises union all
    select \"ClienteLoginTokens\", count(*) from ClienteLoginTokens;" 2>/dev/null' || echo "AVISO: não consegui ler as contagens"

  if grep -q '^N8N_WEBHOOK_SECRET=.\+' "$ENV_FILE"; then ok "N8N_WEBHOOK_SECRET presente no .env"; else echo "FALHOU: N8N_WEBHOOK_SECRET ausente no .env"; FALHAS=$((FALHAS+1)); fi
  if ss -ltn 2>/dev/null | grep -E '0\.0\.0\.0:5299|\*:5299|\[::\]:5299' >/dev/null; then echo "FALHOU: porta 5299 exposta fora do loopback"; FALHAS=$((FALHAS+1)); else ok "porta 5299 só em loopback"; fi

  test "$FALHAS" -eq 0 && ok "smoke test sem falhas" || fail "$FALHAS verificação(ões) falharam"
  ;;

*)
  echo "Uso: bash scripts/vps-portal.sh {set-n8n-secret|import [--apply]|smoke}"
  exit 1
  ;;
esac
