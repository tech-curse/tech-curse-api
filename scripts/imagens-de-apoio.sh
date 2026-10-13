#!/usr/bin/env bash
set -euo pipefail

arquivo="${1:?informe o arquivo do cache, por exemplo ~/imagens-de-apoio.tar}"
imagens=(
  public.ecr.aws/docker/library/postgres:17
  public.ecr.aws/docker/library/redis:7
)

if [ -f "$arquivo" ]; then
  docker load --quiet --input "$arquivo"
  echo "Imagens de apoio carregadas do cache."
  exit 0
fi

for imagem in "${imagens[@]}"; do
  for tentativa in 1 2 3 4; do
    docker pull --quiet "$imagem" && break
    [ "$tentativa" -eq 4 ] && exit 1
    sleep $((tentativa * 20))
  done
done

docker save --output "$arquivo" "${imagens[@]}"
echo "Imagens de apoio baixadas e salvas para o cache."
