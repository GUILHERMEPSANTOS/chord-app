# Validação desta etapa

- `docker compose build processor`: concluiu com `lv-chordia==1.1.0`, `mir_eval==0.8.2`, `psutil==7.0.0` e `PyYAML==6.0.2`.
- `docker compose run --rm --no-deps processor python -m unittest discover -s tests -v`: 8 testes passaram. Os testes de intervalos usam dados artificiais apenas para verificar o cálculo e a leitura de arquivos; não medem precisão do modelo.
- BTC-ISMIR19: pesos públicos `test/btc_model_large_voca.pt` e 10 segundos do `test/example.mp3` foram executados em CPU no processador. O adaptador gerou intervalos `.lab` com rótulos do vocabulário amplo, incluindo sétimas. Isso confirma o caminho de inferência, não a qualidade das previsões.
- Não foi possível calcular acerto contra acordes anotados: ainda não há músicas completas com áudio e anotações correspondentes disponíveis localmente. Nenhum valor de precisão foi inventado ou publicado.

Próximo requisito para decidir sobre a produção: adicionar pares de áudio e anotação ao manifesto privado, executar os dois detectores no mesmo conjunto e comparar as métricas por duração, transições, tempo e memória. A produção continua com `lv-chordia` até essa revisão.
