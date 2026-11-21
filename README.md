# Game Perf Lab

Protótipo Windows para investigar travadas em jogos e medir o efeito de uma alteração por vez. Nome de trabalho. O primeiro comando analisa uma captura já gravada. Usa a biblioteca padrão do Python 3.12 e roda sem privilégios de administrador.

## Primeira execução

Abra esta pasta como workspace no Antigravity. No terminal da raiz, execute:

```powershell
python -m unittest discover -s tests -v
python -m gameperf examples/synthetic.csv
```

O exemplo de dez intervalos é sintético. O esperado é P50 de 4 ms, P99 de 20 ms e 1% low de 50 FPS. Ele testa a conta. Medir o PC exige captura real.

## Contrato de entrada

CSV do PresentMon com esquema de métricas v1 (a CLI disponibiliza `--v1_metrics`). São obrigatórias as colunas `Application`, `ProcessID`, `SwapChainAddress` e `MsBetweenPresents`. Forneça um único fluxo de apresentação. Uma linha inicial com zero ou NA é aceita como aquecimento. Outros intervalos devem ser positivos e finitos. Capturas misturadas ou em outro esquema retornam erro explícito. Os intervalos representam o tempo entre chamadas Present da aplicação.

A ferramenta informa percentis por posto mais próximo. O 1% low é 1000 dividido pela média do 1% de intervalos mais lentos, arredondando a quantidade para cima. A taxa de intervalos acima do limiar usa a duração acumulada em minutos. O resultado conta intervalos individuais. Uma única travada perceptível pode ocupar vários intervalos.

Documentação do formato: https://github.com/GameTechDev/PresentMon/blob/main/README-ConsoleApplication.md

## Desenvolvimento e publicação

As regras da IDE ficam em `AGENTS.md`. O fluxo de CI em `.github/workflows/test.yml` roda os testes no Windows em pushes e pull requests. Dados pessoais de testes ficam em `captures/` e saídas em `reports/`, ambos ignorados pelo Git.

Para conectar esta pasta a um repositório vazio criado no GitHub, use `git remote add origin URL_DO_REPOSITORIO` e `git push -u origin main` depois de revisar os arquivos e escolher a licença. MIT é a proposta inicial para o código próprio. O nome e a licença definitivos ficam com o mantenedor.

## Próximas entregas

1. Comparar duas capturas compatíveis. Mostrar A, B e a diferença de P99, 1% low e intervalos lentos por minuto. Uma comparação isolada é descritiva. Para concluir sobre ganho, repetir a mesma cena e medir a variação.
2. Adicionar inventário de aplicativos com psutil, agrupando processos identificados e registrando CPU, memória e I/O. Começar com seleção manual e coleta em leitura.
3. Consultar as estatísticas do OBS via WebSocket e comparar renderização e saída em um perfil de stream.
4. Somente depois, aplicar uma ação de sessão selecionada e validar sua reversão após término normal e falha. O adaptador de Deadlock deve respeitar resolução, escala e HUD escolhidos.

## Primeira tarefa para o Antigravity

Leia AGENTS.md e rode a suíte atual. Implemente o comando de comparação A/B preservando o uso com um arquivo. Reutilize as métricas existentes, exiba a direção da diferença e rejeite jogos ou métricas incompatíveis. PIDs e endereços de swap chain podem mudar entre capturas. Acrescente um caso sintético com melhora de P99 e outro com entradas incompatíveis. Entregue a saída do terminal e os testes.
