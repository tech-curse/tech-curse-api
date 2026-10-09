# Twelve-Factor na Tech Curse API

A API segue a metodologia [Twelve-Factor App](https://12factor.net/pt_br/): uma imagem só, que roda igual em qualquer ambiente, configurada de fora e fácil de substituir. Este checklist mostra, fator por fator, o que já está atendido, o que falta e qual cenário da [especificação](especificacoes/README.md) ou fase do projeto resolve cada lacuna. Ele é atualizado no mesmo PR que muda a situação de um fator.

Legenda: ✅ atendido · ⚠️ atendido em parte · ❌ não atendido.

| Fator | Situação | Como é atendido | O que falta | Onde se resolve |
| --- | --- | --- | --- | --- |
| **I. Base de código** | ✅ | Um repositório (`tech-curse/tech-curse-api`) e uma imagem por versão, usada em staging e em produção | | |
| **II. Dependências** | ⚠️ | Versões fixas no `Directory.Packages.props` (Central Package Management) e SDK fixo no `global.json` | A globalização depende da ICU do sistema (`InvariantGlobalization=false`); o Dockerfile precisa declará-la ou a API precisa dispensá-la | Fase 4 |
| **III. Configuração** | ⚠️ | Tudo o que varia entre ambientes vem de variáveis de ambiente; funcionalidades ligadas por chave explícita, nunca pelo nome do ambiente (`TRV-025`, `TRV-026`) | `appsettings.Development.json` e User Secrets ainda existem; o tempo de vida do access token está fixo no código | `TRV-034` (Fase 4); `AUTH-034` (Fase 5) |
| **IV. Serviços de apoio** | ✅ | PostgreSQL e Redis por connection string; o gateway de pagamento é trocado por configuração (`TRV-025`, `TRV-033`); o coletor OpenTelemetry será um endereço em `OTEL_EXPORTER_OTLP_ENDPOINT` | | `TRV-038` (Fase 8) |
| **V. Build, release, run** | ⚠️ | Imagem imutável no GHCR com tag de versão ou SHA; a configuração entra só na release | As migrations rodam no startup, misturando release e execução | `TRV-024` (Fase 7) |
| **VI. Processos** | ⚠️ | Sem sessão no servidor (JWT); cache e idempotência no Redis; chaves do Data Protection e refresh tokens no PostgreSQL | **Exceção aceita:** o rate limiting guarda o estado na memória do processo. Vale enquanto a API rodar com uma réplica só | `transversais.md`, seção "Limites de requisição" |
| **VII. Vínculo de porta** | ⚠️ | Kestrel atende HTTP direto, sem servidor de aplicação externo | `UseHttpsRedirection()` atrás do proxy, sem tratar `X-Forwarded-*` | `TRV-037`, `TRV-013` (Fase 6) |
| **VIII. Concorrência** | ✅ | Escala por réplicas do mesmo processo, quando os fatores VI, IX e XII estiverem resolvidos | Mais de uma réplica exige rever a exceção do fator VI | |
| **IX. Descartabilidade** | ⚠️ | Liveness e readiness separados (`TRV-020`, `TRV-021`); a API espera até 30 s pelas requisições em andamento | Startup lento por causa das migrations; o Docker mata o processo em 10 s | `TRV-024` (Fase 7); `TRV-036` (Fase 4) |
| **X. Paridade dev/prod** | ⚠️ | As mesmas versões de PostgreSQL e Redis em desenvolvimento, nos testes (Testcontainers) e em produção; comportamento igual em todo ambiente (`TRV-025`, `TRV-026`) | Configuração de desenvolvimento num arquivo por ambiente | `TRV-034` (Fase 4) |
| **XI. Logs** | ⚠️ | Logs estruturados em JSON no stdout (Serilog) | Exportação via OpenTelemetry (logs, traces e métricas), com o destino decidido pelo coletor | `TRV-038` (Fase 8) |
| **XII. Processos administrativos** | ❌ | | Não há como criar o primeiro Admin fora de `Development`; as migrations precisam virar processo avulso na imagem da release | `TRV-035` (antes do 1º deploy); `TRV-024` (Fase 7) |

## Exceções aceitas

Pontos em que a API se afasta da metodologia de propósito, com o motivo:

| Exceção | Motivo | Quando rever |
| --- | --- | --- |
| Rate limiting com estado em memória (fator VI) | Uma réplica só; levar o limite para o Redis custaria trabalho sem ganho hoje | Antes de subir uma segunda réplica |
| Nome do ambiente em travas de segurança: o gateway simulado nunca em `Production` (`TRV-033`) e o seed de Admin só em `Development` (`AUTH-038`) (fator X) | São segundas barreiras contra um erro de configuração, não um jeito de ligar funcionalidade (regra 6 de `transversais.md`) | Não há previsão |
