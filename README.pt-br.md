<div align="center">
  <img src="assets/logo.svg" width="120" alt="ollim" />
  <h1>ollim-telemetry</h1>
  <p>Acompanhe o uso de tokens das suas sessões de programação com IA — localmente, com privacidade, e com leaderboard opcional.</p>

  [![NuGet](https://img.shields.io/nuget/v/ollim-telemetry?label=NuGet&color=5c2d91)](https://www.nuget.org/packages/ollim-telemetry)
  [![npm](https://img.shields.io/npm/v/ollim-telemetry?label=npm&color=cb3837)](https://www.npmjs.com/package/ollim-telemetry)
  [![Licença: MIT](https://img.shields.io/badge/licen%C3%A7a-MIT-blue.svg)](./LICENSE)
  [![Plataforma](https://img.shields.io/badge/plataforma-Linux%20%7C%20macOS-lightgrey)](https://github.com/Bruno0M/OllimTelemetry/releases)

  [🇺🇸 English](./README.md)
</div>

---

Uma CLI leve que se conecta a agentes de IA, lê apenas o campo `usage` dos logs locais, e (com consentimento explícito) envia contagens anônimas de tokens para o [ollim.dev](https://ollim.dev) para comparação no leaderboard. Sem daemon em segundo plano — tudo roda via Stop hooks.

Construído com NativeAOT — **~9 MB de binário, ~5 ms de inicialização, ~10 MB de RAM**.

## Instalação

**macOS / Linux (recomendado)**

```bash
curl -fsSL https://ollim.dev/install.sh | bash
```

**npm**

```bash
npm install -g ollim-telemetry
```

> Requer Node ≥ 18.

**NuGet**

```bash
dotnet tool install -g ollim-telemetry
```

> Requer .NET 10 SDK.

**Binários pré-compilados**

Baixe em [Releases](https://github.com/Bruno0M/OllimTelemetry/releases):

| Plataforma | Arquivo |
|---|---|
| Linux x64 | `ollim-linux-x64.tar.gz` |
| Linux arm64 | `ollim-linux-arm64.tar.gz` |
| macOS arm64 | `ollim-osx-arm64.tar.gz` |

## Primeiros passos

```bash
ollim start   # primeiro uso dispara o fluxo de opt-in e registra os hooks nos agentes detectados
ollim status  # exibe o estado dos hooks, configurações de compartilhamento e fila pendente
```

## Como funciona

1. `ollim start` registra um **Stop hook** em `~/.claude/settings.json` (Claude Code) e `~/.codex/hooks.json` (Codex, se instalado)
2. Ao fim de cada sessão, o hook lê apenas o campo `usage` do arquivo de log do agente — o conteúdo das mensagens nunca é lido
3. As contagens de tokens são armazenadas em uma fila SQLite local em `~/.local/share/ollim/queue.db`
4. O hook então tenta enviar a fila para `api.ollim.dev`, se o compartilhamento estiver habilitado
5. Falhas HTTP são reenviadas com backoff exponencial na próxima invocação — nenhum dado é perdido

## Privacidade

- Apenas contagens de tokens são coletadas: `input_tokens`, `output_tokens`, `cache_read_tokens`, `cache_write_tokens`
- Conteúdo de mensagens, prompts e respostas **nunca são lidos ou transmitidos**
- Compartilhamento está **desabilitado por padrão** — o fluxo de primeiro uso pede consentimento explícito
- Compartilhamento requer uma conta GitHub — execute `ollim login` após optar por participar
- O nome do repositório pode ser compartilhado para contexto no leaderboard, mas também é opt-in
- Um UUID aleatório é gerado localmente e vinculado à sua conta GitHub

## Comandos

| Comando | Descrição |
|---|---|
| `ollim start` | Registra hooks em todos os agentes detectados (executa onboarding no primeiro uso e preenche histórico) |
| `ollim stop` | Remove todos os hooks e envia os lotes pendentes |
| `ollim status` | Exibe estado dos hooks, configuração de compartilhamento e contagem de lotes pendentes |
| `ollim login` | Vincula uma conta GitHub (necessário para sincronizar dados) |
| `ollim logout` | Desvincula a conta GitHub e desabilita o compartilhamento com o leaderboard |
| `ollim submit` | Envia manualmente todos os lotes pendentes para o backend (reseta os temporizadores de retry) |
| `ollim config` | Abre `~/.config/ollim/config.json` no `$VISUAL`/`$EDITOR`/`vi` |
| `ollim uninstall` | Remove os hooks e apaga todos os dados locais |

## Configuração

A configuração fica em `~/.config/ollim/config.json` (especificação XDG Base Directory):

```json
{
  "ShareGlobal": false,
  "ShareRepoName": false,
  "SyncIntervalMinutes": 5,
  "Agent": "claude-code",
  "BackendUrl": "https://api.ollim.dev"
}
```

Execute `ollim config` para abrir diretamente, ou edite manualmente.

## Compilando a partir do código-fonte

Requer .NET 10 SDK. NativeAOT no Linux também requer `clang` e `zlib1g-dev`.

```bash
# Compilar e rodar os testes
dotnet build
dotnet test

# Rodar a CLI sem NativeAOT (iteração rápida)
dotnet run --project src/OllimTelemetry.Cli --launch-profile dev -- status

# Publicar binário NativeAOT para um único RID
dotnet publish src/OllimTelemetry.Cli/OllimTelemetry.Cli.csproj \
  -c Release -r linux-x64 --self-contained true /p:PublishAot=true -o dist/linux-x64

# Compilar todas as plataformas (requer dotnet-script)
dotnet script scripts/build.cs
```

## Agentes suportados

| Agente | Status |
|---|---|
| [Claude Code](https://claude.ai/code) | ✅ Suportado |
| [Codex CLI](https://github.com/openai/codex) | ✅ Suportado |
| [Gemini CLI](https://github.com/google-gemini/gemini-cli) | 🚧 Em breve |
| [Cursor](https://cursor.com) | 🚧 Em breve |
| [GitHub Copilot](https://github.com/features/copilot) | 🚧 Em breve |

## Licença

[MIT](./LICENSE)
