import argparse
import pathlib
import re
import sys

RAIZ = pathlib.Path(__file__).resolve().parent.parent
CENARIO = re.compile(r"^\*\*([A-Z]+-\d{3}):")
STATUS = re.compile(r"^\*\*Status:\*\*\s*(.*)")
TRAIT = re.compile(r'Trait\(\s*"Especificacao"\s*,\s*"([A-Z]+-\d{3})"\s*\)')


def ler_cenarios():
    cenarios = {}
    for arquivo in sorted((RAIZ / "docs" / "especificacoes").glob("*.md")):
        atual = None
        for linha in arquivo.read_text(encoding="utf-8").splitlines():
            encontrado = CENARIO.match(linha)
            if encontrado:
                atual = encontrado.group(1)
                continue
            status = STATUS.match(linha)
            if status and atual:
                cenarios[atual] = status.group(1).strip()
                atual = None
    return cenarios


def ler_cobertos():
    cobertos = set()
    for arquivo in (RAIZ / "tests").rglob("*.cs"):
        cobertos.update(TRAIT.findall(arquivo.read_text(encoding="utf-8")))
    return cobertos


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    argumentos = argparse.ArgumentParser(description="Confere se os cenários implementados da especificação têm teste.")
    argumentos.add_argument("--estrito", action="store_true", help="termina com erro se algum cenário implementado não tiver teste")
    opcoes = argumentos.parse_args()

    cenarios = ler_cenarios()
    cobertos = ler_cobertos()
    implementados = sorted(id_ for id_, status in cenarios.items() if status.startswith("implementado"))
    sem_teste = [id_ for id_ in implementados if id_ not in cobertos]
    desconhecidos = sorted(id_ for id_ in cobertos if id_ not in cenarios)

    print("## Rastreabilidade especificação → testes")
    print()
    print(f"- Cenários na especificação: {len(cenarios)}")
    print(f"- Implementados: {len(implementados)}")
    print(f"- Implementados com teste: {len(implementados) - len(sem_teste)}")
    print(f"- Implementados sem teste: {len(sem_teste)}")
    if desconhecidos:
        print(f"- IDs citados em testes que não existem na especificação: {', '.join(desconhecidos)}")
    if sem_teste:
        print()
        print("<details><summary>Cenários implementados sem teste</summary>")
        print()
        print(", ".join(sem_teste))
        print()
        print("</details>")

    if desconhecidos or (opcoes.estrito and sem_teste):
        sys.exit(1)


if __name__ == "__main__":
    main()
