# Validação desta etapa

- `docker compose build processor`: concluiu com `lv-chordia==1.1.0`, `mir_eval==0.8.2`, `psutil==7.0.0` e `PyYAML==6.0.2`.
- `docker compose run --rm --no-deps processor python -m unittest discover -s tests -v`: 8 testes passaram. Os testes de intervalos usam dados artificiais apenas para verificar o cálculo e a leitura de arquivos; não medem precisão do modelo.
- BTC-ISMIR19: pesos públicos `test/btc_model_large_voca.pt` e 10 segundos do `test/example.mp3` foram executados em CPU no processador. O adaptador gerou intervalos `.lab` com rótulos do vocabulário amplo, incluindo sétimas. Isso confirma o caminho de inferência, não a qualidade das previsões.
- Na preparação inicial, ainda não havia pares de áudio e anotações disponíveis localmente; a medição exploratória abaixo foi feita posteriormente. Ainda não há músicas completas anotadas.

## Medição exploratória com GuitarSet (2026-09-22)

Fonte: [GuitarSet 1.1.0, Zenodo 3371780](https://zenodo.org/records/3371780). Usei a gravação por microfone `03_Rock2-142-D_comp_mic.wav` e a anotação **performed chord** de `03_Rock2-142-D_comp.jams` convertida para `.lab`, sem alterar os rótulos. São 27,04 s de guitarra tocando acompanhamento, com 14 segmentos de referência. O par foi baixado e mantido apenas em `benchmark/private/`; áudio e anotação não foram incluídos no Git. Executei os dois detectores no mesmo áudio normalizado (mono, 22.050 Hz), em CPU, com o comando documentado em `benchmark/README.md`.

| Detector | Maior/menor* | Sétimas* | Meio diminuto (recall) | Transições (F1, ±0,5 s) | Inferência | Pico RSS** |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| lv-chordia 1.1.0 | 96,10% | 96,10% | 0% de 3,38 s | 96,30% | 97,35 s | 1.235 MB |
| BTC-ISMIR19 | 92,14% | 92,14% | 8,18% de 3,38 s | 80,00% | 7,50 s | 1.281 MB |

\* Acerto ponderado pela duração elegível: 21,97 s. `mir_eval.chord.majmin` e `sevenths` excluem parte dos acordes fora de seus vocabulários. Ambos erraram boa parte dos trechos `E:hdim7`; o BTC acertou apenas 0,28 s. **Pico RSS é uma amostra de processo e filhos, dependente desta máquina. O relatório privado registra hashes SHA-256 do áudio, `.lab` e pesos BTC, ambiente, tempos e previsões. BTC: commit `2682317be668032e6e4b269ded36adaa2ad57df0`, checkpoint SHA-256 `1673d23f8f9a55ae7f9e8b80a51da616debb22675b8d8b67ea6ce0ef37b0ab51`.

Também avaliei `03_Rock2-142-D_solo_mic.wav` com a anotação correspondente. Ambos produziram somente `N` (0% nas métricas elegíveis); a gravação solo tem notas isoladas e é um teste de domínio diferente da transcrição de acompanhamento. A medição está em `benchmark/results/report-solo.json` (ignorado pelo Git).

**Limite:** esses trechos não são músicas completas com voz, bateria e baixo. Uma única execução curta também não estima variação entre gêneros, gravações e acordes raros. Portanto, os resultados não justificam trocar o detector de produção nem combinar modelos automaticamente. Ainda é necessário um conjunto de músicas completas com áudio e `.lab` alinhados à mesma versão da gravação.

Próximo requisito para decidir sobre a produção: adicionar pares de músicas completas e anotação ao manifesto privado, executar os dois detectores no mesmo conjunto e comparar as métricas por duração, transições, tempo e memória. A produção continua com `lv-chordia` até essa revisão.
