#!/usr/bin/env bash
set -euo pipefail

endereco="${ENDERECO_API:-http://127.0.0.1:5130}"
imagem="${API_IMAGE:-tech-curse-api:dev}"

falhar() {
  echo "FALHOU: $*" >&2
  exit 1
}

valor_do_env() {
  grep -E "^$1=" .env | head -n 1 | cut -d= -f2-
}

usuario=$(docker image inspect "$imagem" --format '{{.Config.User}}')
[ -n "$usuario" ] && [ "$usuario" != "root" ] && [ "$usuario" != "0" ] || falhar "a imagem roda como root"
echo "ok: a imagem roda como '$usuario'"

[ "$(curl -s -o /dev/null -w '%{http_code}' "$endereco/health/live")" = "200" ] || falhar "/health/live"
[ "$(curl -s "$endereco/health/ready")" = '{"status":"Healthy"}' ] || falhar "/health/ready"
echo "ok: liveness e readiness"

corpo=$(printf '{"email":"%s","password":"%s"}' "$(valor_do_env Seed__Admin__Email)" "$(valor_do_env Seed__Admin__Password)")
token=$(curl -sf -H 'Content-Type: application/json' -d "$corpo" "$endereco/tech-curse/Auth/login" \
  | sed -E 's/.*"accessToken":"([^"]+)".*/\1/') || falhar "login do Admin do seed"
[ "$(curl -s -o /dev/null -w '%{http_code}' -H "Authorization: Bearer $token" "$endereco/tech-curse/Course")" = "200" ] \
  || falhar "consulta autenticada"
echo "ok: login e consulta autenticada"

inicio=$(date +%s)
docker compose stop api >/dev/null 2>&1
duracao=$(( $(date +%s) - inicio ))
codigo=$(docker inspect "$(docker compose ps -a -q api)" --format '{{.State.ExitCode}}')
[ "$codigo" = "0" ] || falhar "a API terminou com código $codigo"
[ "$duracao" -lt 30 ] || falhar "a API levou ${duracao}s para encerrar"
echo "ok: encerramento limpo em ${duracao}s, código 0"
