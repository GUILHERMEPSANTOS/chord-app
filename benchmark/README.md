# Avaliação local de reconhecimento de acordes

Este avaliador compara previsões com **acordes anotados no tempo para a mesma gravação**. Não usa concordância entre detectores nem os testes sintéticos como evidência de precisão. Ainda não há áudio de referência no projeto; por isso não há números de acerto publicados.

## Dados necessários

Crie `benchmark/private/manifest.json` a partir de `benchmark/manifest.example.json`. Coloque os arquivos MP3/WAV e `.lab` no mesmo diretório ou ajuste os caminhos relativos ao manifesto. Cada linha `.lab` deve conter `início fim acorde`, em segundos, usando a notação Harte (`C:maj`, `A:min`, `G:7`, `B:hdim7`, `N`). Use músicas completas, com a versão exata do áudio que foi anotada. Registre a origem e os direitos de cada par. Selecione faixas de estilos e instrumentações diferentes, com exemplos suficientes de sétimas e diminutos. Não inclua áudio ou anotações privadas no Git.

O [McGill Billboard](https://ddmal.ca/research/The_McGill_Billboard_Project_(Chord_Analysis_Dataset)/) oferece anotações CC0, mas não distribui o áudio correspondente. O [GuitarSet](https://zenodo.org/records/3371780) oferece áudio e anotações, porém é formado por trechos de guitarra; pode ser um teste complementar, não a prova principal para músicas completas com voz e bateria.

## Executar o modelo atual

No PowerShell, a partir da raiz do projeto:

```powershell
docker compose build processor
docker compose run --rm -v "${PWD}/benchmark/private:/data:ro" -v "${PWD}/benchmark/results:/results" processor python -m benchmark.run --manifest /data/manifest.json --output /results --detector lv-chordia
```

O relatório fica em `benchmark/results/report.json`; as previsões em `.lab` ficam na subpasta `predictions`. Esses arquivos são ignorados pelo Git. A normalização usa o mesmo FFmpeg do processamento atual: WAV mono a 22.050 Hz. O arquivo normalizado é apagado ao final de cada faixa. O benchmark não grava resultados no PostgreSQL nem altera análises ou correções existentes.

## BTC-ISMIR19 experimental

O [BTC-ISMIR19](https://github.com/jayg996/BTC-ISMIR19) disponibiliza pesos para vocabulário amplo em `test/btc_model_large_voca.pt`, com 170 rótulos incluindo diminutos, meio diminutos e sétimas. O código é MIT; não encontramos uma licença separada para os pesos. É código de pesquisa com dependências antigas, então sua execução em versões atuais de Python e PyTorch precisa ser verificada. O adaptador usa as classes e os pesos originais em um processo Python separado, mas decodifica com a tabela de rótulos do vocabulário amplo. Não há combinação automática das previsões.

Para comparar no mesmo contêiner, baixe o repositório BTC em `benchmark/private/btc-source` e execute:

```powershell
git clone --depth 1 https://github.com/jayg996/BTC-ISMIR19.git benchmark/private/btc-source
docker compose run --rm --no-deps -v "${PWD}/benchmark/private:/data:ro" -v "${PWD}/benchmark/results:/results" processor python -m benchmark.run --manifest /data/manifest.json --output /results --detector lv-chordia --detector btc-ismir19 --btc-checkout /data/btc-source --btc-python /usr/local/bin/python
```

O checkout e os pesos ficam na pasta ignorada pelo Git. O relatório inclui o commit BTC e o hash SHA-256 do peso. Um teste de funcionamento com 10 segundos do áudio de exemplo do próprio BTC concluiu em CPU; isso **não mede acurácia** nem garante que qualquer música inteira caiba no limite de memória. O adaptador corrige apenas incompatibilidades de leitura de YAML, o alias `np.float` removido e o caso de áudio curto, sem alterar os pesos.

## Métricas e limites

- `majmin` e `sevenths`: proporção da duração elegível acertada segundo `mir_eval.chord`, com o denominador em segundos no relatório. A métrica padrão `sevenths` não inclui todas as classes diminutas.
- `diminished`: duração de referência de `dim`, `dim7` e `hdim7` cujo **raiz e tipo exato** foram acertados; também informa precisão por duração predita. Sem exemplos de referência, o recall é `null`.
- `transitions`: precisão, recall e F1 das mudanças de acorde, com pareamento único e tolerância padrão de 0,5 s. O relatório informa a quantidade de transições e o erro temporal médio dos pareamentos.
- `inference_seconds`: tempo por detector. `normalization_seconds` é medido uma vez por faixa; `processing_seconds` soma normalização e inferência. `peak_rss_bytes` é o maior RSS observado para o processo avaliador e seus filhos, amostrado a cada 50 ms. É uma estimativa dependente da máquina.

Os detectores rodam sequencialmente sobre o mesmo WAV normalizado para reduzir disputa por CPU. O relatório registra a ordem de execução; para comparar desempenho, repita a medição com a ordem invertida. Acurácia é agregada por duração elegível; resultados sem suporte para uma classe aparecem como `null`. Faixas ambíguas, rótulos fora do vocabulário e diferenças de masterização precisam de revisão antes de qualquer conclusão. A aplicação pode exibir os resultados salvos de cada modelo separadamente, mas não os combina automaticamente. A medição exploratória de guitarra não comprova melhoria em músicas completas.
