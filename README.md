# Game Perf Lab

Protótipo Windows para investigar travadas em jogos e medir o efeito de uma alteração por vez. Nome de trabalho. Analisa capturas já gravadas e compara sessões antes/depois (A/B). Disponibiliza analisador de referência em Python 3.12 e aplicação desktop em C# com WinUI 3 empacotada em MSIX para Windows x64.

## Primeira execução (Python - Referência Offline)

Abra esta pasta como workspace no Antigravity. No terminal da raiz, execute:

```powershell
python -m unittest discover -s tests -v
python -m gameperf examples/synthetic.csv
python -m gameperf examples/synthetic.csv examples/synthetic_optimized.csv
```

O exemplo de dez intervalos é sintético:
- Na análise individual de `examples/synthetic.csv`, o esperado é P50 de 4 ms, P99 de 20 ms e 1% low de 50 FPS.
- Na comparação A/B com `examples/synthetic_optimized.csv`, a ferramenta exibe lado a lado P99 (20 ms → 15 ms, melhora de -25%), 1% low (50 FPS → 66,67 FPS, melhora de +33,3%), e a taxa de intervalos lentos acima de 7,14 ms e 16,67 ms por minuto. O PID e a swap chain mudam entre as partidas e são aceitos normalmente.
- Entradas incompatíveis (jogos diferentes ou métricas divergentes) são rejeitadas:
  ```powershell
  python -m gameperf examples/synthetic.csv examples/synthetic_incompatible_app.csv
  ```
- Para exportação em JSON estruturado com metadados e diferenciação de métricas, use a flag `--json`:
  ```powershell
  python -m gameperf examples/synthetic.csv examples/synthetic_optimized.csv --json
  ```

## Aplicação Desktop C# (WinUI 3 - MSIX x64)

A versão desktop em C# com interface WinUI 3 permite carregar e comparar visualmente duas capturas do PresentMon lado a lado. O cálculo é executado fora da thread de interface (`Task.Run`) e a aplicação roda de forma independente, sem necessidade de Python instalado.

### 1. Testes de equivalência numérica (C# vs Python)
```powershell
dotnet test tests_csharp/GamePerfLab.Tests/GamePerfLab.Tests.csproj -v normal
```
Verifica que os mesmos CSVs sintéticos produzem os mesmos percentis, 1% low, contagens por minuto, rejeições e comportamentos de alteração de PID verificados no Python.

### 2. Compilação e empacotamento MSIX (Windows x64)
```powershell
dotnet publish src/GamePerfLab.App/GamePerfLab.App.csproj -c Release -r win-x64 -p:GenerateAppxPackageOnBuild=true -p:PublishTrimmed=false
```
O pacote gerado fica em `src/GamePerfLab.App/AppPackages/GamePerfLab.App_1.0.0.0_x64_Test/GamePerfLab.App_1.0.0.0_x64.msix`. Para instalar no Windows via PowerShell:
```powershell
Add-AppxPackage -Path src/GamePerfLab.App/AppPackages/GamePerfLab.App_1.0.0.0_x64_Test/GamePerfLab.App_1.0.0.0_x64.msix
```
*(Nota: requer Modo de Desenvolvedor ativado no Windows ou certificado de desenvolvimento confiável).*

### 3. Execução direta (Sem exigência de Modo de Desenvolvedor)
```powershell
dotnet build src/GamePerfLab.App/GamePerfLab.App.csproj -c Release -r win-x64 -p:WindowsPackageType=None -p:PublishTrimmed=false
.\src\GamePerfLab.App\bin\Release\net11.0-windows10.0.26100.0\win-x64\GamePerfLab.App.exe
```

## Contrato de entrada

CSV do PresentMon com esquema de métricas v1 (a CLI disponibiliza `--v1_metrics`). São obrigatórias as colunas `Application`, `ProcessID`, `SwapChainAddress` e `MsBetweenPresents`. Forneça um único fluxo de apresentação por arquivo. Uma linha inicial com zero ou NA é aceita como aquecimento. Outros intervalos devem ser positivos e finitos. Capturas misturadas ou em outro esquema retornam erro explícito.

Os intervalos representam o tempo entre chamadas Present da aplicação (CPU/API). Não representam quadros exibidos pelo compositor do Windows nem métricas de codificação ou composição do OBS Studio.

A ferramenta informa percentis por posto mais próximo. O 1% low é 1000 dividido pela média do 1% de intervalos mais lentos, arredondando a quantidade para cima. A taxa de intervalos acima do limiar usa a duração acumulada em minutos. O resultado conta intervalos individuais. Uma única travada perceptível pode ocupar vários intervalos.

Documentação do formato: https://github.com/GameTechDev/PresentMon/blob/main/README-ConsoleApplication.md

## Desenvolvimento e publicação

As regras da IDE ficam em `AGENTS.md`. O fluxo de CI em `.github/workflows/test.yml` roda os testes no Windows em pushes e pull requests. Dados pessoais de testes ficam em `captures/` e saídas em `reports/`, ambos ignorados pelo Git.

Para conectar esta pasta a um repositório vazio criado no GitHub, use `git remote add origin URL_DO_REPOSITORIO` e `git push -u origin main` depois de revisar os arquivos e escolher a licença. MIT é a proposta inicial para o código próprio. O nome e a licença definitivos ficam com o mantenedor.

## Próximas entregas

O cruzamento das duas pesquisas e os critérios de conclusão estão em [Decisões de projeto](docs/decisoes.md).

1. [Concluído] Comparar duas capturas compatíveis no CLI Python e aplicação desktop C# WinUI 3. Mostrar A, B e a diferença de P99, 1% low e intervalos lentos por minuto.
2. Adicionar inventário de aplicativos com psutil, agrupando processos identificados e registrando CPU, memória e I/O. Começar com seleção manual e coleta em leitura.
3. Consultar as estatísticas do OBS via WebSocket e comparar renderização e saída em um perfil de stream.
4. Somente depois, aplicar uma ação de sessão selecionada e validar sua reversão após término normal e falha. O adaptador de Deadlock deve respeitar resolução, escala e HUD escolhidos.
