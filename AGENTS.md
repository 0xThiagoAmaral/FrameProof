# Trabalho no repositório

O produto mede o efeito de ajustes na fluidez de jogos Windows e na estabilidade da transmissão. A primeira entrega é um analisador offline em Python 3.12.

- Rode `python -m unittest discover -s tests -v` e o exemplo do README após uma alteração funcional.
- Mantenha nome, unidade, fonte e método de cálculo de cada métrica no relatório. Diferencie intervalos entre chamadas Present, quadros exibidos e métricas do OBS.
- Use dados sintéticos mínimos como fixtures públicas. Mantenha capturas de usuários, caminhos pessoais, backups do jogo e credenciais fora dos commits.
- Colete dados de sessão em modo leitura. Para uma futura ação que modifique o PC, implemente prévia, seleção pelo usuário e restauração verificável do estado anterior.
- Quando incorporar código externo, registre URL, revisão, arquivo, licença e avisos exigidos no mesmo PR. Para invocar uma CLI, documente versão, parâmetros e contrato de saída.
- Termine cada tarefa com o comando executado, o resultado observado e o que ainda depende de hardware real.
