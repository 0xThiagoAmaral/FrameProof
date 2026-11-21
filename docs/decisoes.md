# Decisões a partir das duas pesquisas

A planilha de mercado detalha PresentMon, inventário de aplicativos, OBS e experimento A/B. O relatório *Otimizadores de PC para Jogos*, páginas 16 e 17, recomenda catálogo declarativo e reversão, perfis de CPU por sessão, gestão de RAM sob pressão, compatibilidade e diagnóstico antes/depois. Esta síntese define a ordem dos primeiros PRs.

| Entrega | Decisão | Evidência de conclusão |
| --- | --- | --- |
| 1. Diagnóstico offline | Começar por uma CLI em Python 3.12 para comparar a mesma métrica entre capturas. Preservar essas fixtures como referência também para um futuro cliente nativo Windows. | Uma série sintética reproduz os percentis esperados. Uma captura real identifica jogo, métrica e duração. |
| 2. Sessão e streaming | Primeiro observar a atividade por aplicativo e os contadores do OBS. Capturar o mesmo replay com a ferramenta ligada e desligada para medir o custo da própria coleta. | Perfil jogo solo e perfil live exportam resultados separados, com indicação de coleta ausente. |
| 3. Catálogo reversível | Primeira ação de teste em um backend simulado. Para cada ajuste registrar identificador, escopo, pré-condição, estado anterior, alvo e verificação. No backend real, retomar o diário após crash e respeitar uma edição externa subsequente. | Aplicar duas vezes é idempotente. Uma falha parcial pode ser recuperada. O relatório identifica alterações que exigem intervenção. |
| 4. Agente nativo | C#/.NET é o candidato inicial do relatório para um cliente Windows. Rust é alternativa caso medições justifiquem um coletor nativo específico. Decidir após medir CPU, memória e custo de implantação do protótipo. | Decisão registrada com resultados do agente ocioso e em captura. As métricas batem com as fixtures da CLI. |
| 5. Experimentos avançados | CPU Sets e planos de energia entram como testes condicionais. Uma limpeza de standby requer pressão observada, dados de paginação e comparação de frametime. Um limiar fixo de RAM livre sozinho é insuficiente. | Repetir cenário e registrar ganho, resultado inconclusivo ou regressão. Configurações de firmware, proteção e timers ficam fora das ações do primeiro lançamento. |

Para reutilização, estudar o catálogo do WinUtil e os modelos de sessão de ThreadPilot e Winderust citados no relatório. Começar com integrações pequenas e revisões identificadas. O repositório inicial contém código próprio e uma fixture sintética.

Referências públicas: [PresentMon](https://github.com/GameTechDev/PresentMon), [WinUtil](https://github.com/ChrisTitusTech/winutil), [ThreadPilot](https://github.com/PrimeBuild-pc/ThreadPilot), [Winderust](https://github.com/TatshSiow/Winderust).
