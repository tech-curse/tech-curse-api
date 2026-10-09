#!/usr/bin/env bash
set -euo pipefail

raiz="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
arquivo="$raiz/.env"

if [ ! -f "$arquivo" ]; then
    echo "Arquivo .env não encontrado em $raiz. Copie o .env.example para .env e preencha os valores." >&2
    exit 1
fi

while IFS= read -r linha || [ -n "$linha" ]; do
    linha="${linha%$'\r'}"
    case "$linha" in
        '' | '#'*) continue ;;
    esac
    chave="${linha%%=*}"
    [ "$chave" = "$linha" ] && continue
    export "$chave=${linha#*=}"
done < "$arquivo"

if [ "$#" -eq 0 ]; then
    set -- dotnet run --project "$raiz/src/Api"
fi

exec "$@"
