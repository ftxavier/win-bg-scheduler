# Win BG Scheduler

Agenda papéis de parede do Windows: por data, dia da semana, mês, ano ou período, o dia todo ou em faixas de horário, com imagem e ajuste por monitor, rotação de pastas, papel padrão, aviso antes da troca e ícone na bandeja.

## Requisitos

- Windows 10 1809+ / Windows 11
- .NET 10 SDK (só para compilar; o exe publicado já leva o runtime)

## Rodar

```powershell
dotnet run --project src/WinBgScheduler.App
dotnet test
```

## Publicar (exe único)

```powershell
dotnet publish src/WinBgScheduler.App -p:PublishProfile=win-x64
# → publish/WinBgScheduler.exe
```

Cada push na `main` (inclusive merge de PR) roda os testes, publica o exe e cria uma release no GitHub com `WinBgScheduler.exe` anexado (`.github/workflows/release.yml`). A versão sobe 0.1 a cada release: `v0.1`, `v0.2`, `v0.3`...

## Estrutura

| Projeto | Conteúdo |
|---|---|
| `src/WinBgScheduler.Core` | Modelos, recorrência, resolução de conflitos ("mais específico vence"), rotação, armazenamento JSON. Sem UI. |
| `src/WinBgScheduler.App` | WPF + WPF-UI: telas, bandeja (H.NotifyIcon), toasts, `IDesktopWallpaper`, inicialização com o Windows. |
| `tests/WinBgScheduler.Core.Tests` | Testes xUnit do motor de agendamento. |

## Primeira execução

1. O papel de parede atual do Windows vira o **papel padrão** (cópias gerenciadas pelo Windows, como `TranscodedWallpaper`, são copiadas para `%AppData%\WinBgScheduler\wallpapers`).
2. O app pergunta se deve **instalar**: copia o exe para `%LocalAppData%\Programs\WinBgScheduler`, cria atalho no menu Iniciar, registra a inicialização com o Windows e passa a rodar na bandeja. Quem escolher "Agora não" pode instalar depois em Configurações.

## Bandeja

Botão direito no ícone: **Configurações** (abre a janela) e **Fechar** (pede confirmação, aplica o papel padrão, para os agendamentos e encerra). Fechar a janela só a esconde.

## Onde ficam os dados

- Configuração: `%AppData%\WinBgScheduler\config.json`
- Imagens geradas (ajustes mistos entre monitores, miniaturas): `%LocalAppData%\WinBgScheduler\cache`
- Instalação: `%LocalAppData%\Programs\WinBgScheduler\WinBgScheduler.exe` + atalho em `Menu Iniciar\Programas\Win BG Scheduler.lnk`
- Inicialização: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` → `Win BG Scheduler`

## Como funciona

- **Prioridade**: quando dois agendamentos coincidem, vence data única → anual → mensal → semanal → diário → período; empate → janela mais curta; empate → o criado por último.
- **Monitores**: cada agendamento tem conteúdo para "Todos os monitores" e, opcionalmente, por monitor. O Windows só aceita um modo de ajuste global; se os monitores usam ajustes diferentes, o app pré-renderiza cada imagem na resolução do monitor e aplica como "Preencher".
- **Timer**: um único timer até a próxima troca, reavaliado ao mudar o relógio, voltar da suspensão, desbloquear a sessão ou mudar monitores.

## Licença

[MIT](LICENSE): use, copie, modifique e distribua à vontade, mantendo o aviso de copyright.
