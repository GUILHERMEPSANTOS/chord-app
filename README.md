# ChordApp

MVP local para reconhecer acordes maiores, menores, sétimas e diminutos em MP3/WAV ou vídeos públicos do YouTube e acompanhar o resultado em um player. API ASP.NET Core, processador Python, Next.js e PostgreSQL.

## Executar

Requisitos: Docker Desktop com Docker Compose e pelo menos 4 GB livres para o processador.

```sh
docker compose up --build
```

Abra http://localhost:3000. Na primeira construção, PyTorch e os pesos do modelo podem tornar o download demorado. A API fica em http://localhost:5000/health e o processador em sua rede interna do Compose.
Cada novo áudio é analisado pelo `lv-chordia` (cinco redes) e pelo BTC-ISMIR19 em CPU. O processamento pode levar vários minutos. O BTC é experimental; suas previsões não são combinadas automaticamente com as do `lv-chordia`.

### Função de cada serviço

| Serviço | Responsabilidade |
| --- | --- |
| `web` | Mostra o player, o progresso e os acordes; envia pedidos à API. |
| `api` | Valida upload e URL, cria o job e atende consultas e correções. Não executa análises em segundo plano. |
| `worker` | Busca jobs pendentes no PostgreSQL, chama o processador e salva resultados ou falhas. |
| `processor` | Extrai e normaliza áudio com yt-dlp/FFmpeg, executa os detectores e devolve tom e acordes. Veja o [guia do processador](processor/README.md). |
| `db` | PostgreSQL que persiste músicas, resultados e correções. |
| `postgres_data` | Volume Docker dos dados do banco. O áudio temporário não fica nesse volume. |
| `audio_temp` | Volume compartilhado entre API e Worker para MP3/WAV até o fim da análise. O Worker remove o arquivo ao concluir ou falhar. |

O middleware da API registra erros inesperados nas requisições e retorna HTTP 500 com um `traceId`, sem enviar detalhes internos ao navegador. Falhas do processamento em segundo plano são registradas pelo worker e aparecem no estado da análise.

### Serviços da API e da aplicação

| Classe | Função |
| --- | --- |
| `AudioFileSourceUpload` | Valida e recebe o arquivo, cria a análise pendente e mantém o áudio temporário até o processamento. |
| `MusicSubmissionYoutube` | Valida a URL do YouTube e cria a análise pendente. |
| `AnalysisWorker` | Mantém o ciclo de busca no projeto `ChordApp.Worker` e recupera jobs interrompidos após reinício. |
| `AnalysisJobRunner` | Reserva um job, coordena a análise, grava o estado e remove o arquivo temporário. |
| `IProcessorClient` / `ProcessorClient` | Separa a comunicação HTTP com o Python do ciclo de jobs. |
| `AnalysisResultMapper` | Valida e transforma a resposta Python em acordes persistidos por modelo. |
| `InterruptedJobRecovery` | Marca jobs que estavam processando como falhos quando o Worker reinicia. |
| `ProcessorProgressReader` | Consulta o progresso em memória do processador para a tela. |
| `ListMusics` | Lista as análises salvas. |
| `GetMusicDetails` | Monta os detalhes da música com estado, progresso e acordes do modelo selecionado. |
| `SelectMusicModel` | Alterna o modelo exibido quando há resultados salvos para ele. |
| `CorrectMusicChord` | Aplica e persiste a correção manual de um acorde. |

As rotas HTTP estão em `MusicEndpointExtensions`, e `GlobalExceptionMiddleware` cuida dos erros inesperados dessas rotas.
As classes e contratos .NET têm resumos XML no próprio código. O projeto `ChordApp.Worker` está incluído em `backend/ChordApp.slnx`.

### Fluxo do job

1. A API valida a entrada e cria uma música com estado `Pending`; uploads ficam em `audio_temp`.
2. O Worker encontra o job e muda seu estado para `Processing`.
3. `ProcessorClient` envia o arquivo ou a URL ao Python. `AnalysisResultMapper` valida os segmentos e separa os resultados de cada modelo.
4. O Worker salva `Completed` ou `Failed` e remove o upload temporário. Ao reiniciar, ele marca jobs que estavam em `Processing` como interrompidos.

Execute apenas uma réplica do Worker nesta versão: a busca de jobs ainda não implementa reserva atômica entre múltiplas instâncias.

## Contrato

- `POST /api/musics`: formulário `file` com MP3/WAV, até 30 MB e duração entre 1 e 900 segundos; `model` opcional (`lv-chordia` ou `btc-ismir19`) escolhe a visualização inicial. Retorna `202` e o `id`.
- `POST /api/musics/youtube`: corpo `{ "url": "https://www.youtube.com/watch?v=...", "model": "lv-chordia" }` para vídeo público. Retorna `202` e o `id`.
- `GET /api/musics/{id}`: estado, progresso, tom, `selectedModel`, `availableModels`, `btcError` e segmentos do modelo selecionado.
- `PUT /api/musics/{id}/model`: corpo `{ "model": "btc-ismir19" }` para alternar entre resultados já salvos. Retorna `400` se esse modelo não tem resultado para a música.
- `GET /api/musics`: análises salvas.
- `PUT /api/musics/{id}/chords/{chordId}`: corpo `{ "chord": "Am7" }` para correção.

Os segmentos têm `startTime`, `endTime`, `chord` e `confidence`. `confidence` fica `null` porque o modelo escolhido não fornece probabilidade calibrada de acerto por segmento. `N` representa ausência de acorde.
Novas análises usam o vocabulário `submission` do `lv-chordia` e os pesos de vocabulário amplo do BTC. A tela aceita tríades maiores e menores; sétima dominante (`C7`), maior (`Cmaj7`) e menor (`Cm7`); diminuto (`Cdim`), diminuto com sétima (`Cdim7`) e meio diminuto (`Cm7b5`). Outros tipos detectados são reduzidos a maior/menor quando a qualidade permite, ou a `N`. As análises antigas permanecem atribuídas ao `lv-chordia`. As correções são independentes por modelo. Se o BTC falhar, o resultado do `lv-chordia` continua disponível e a tela informa que o BTC não concluiu.
`progressPercent` mostra marcos do download, normalização e execução dos dois modelos. É uma porcentagem aproximada das etapas concluídas, não uma previsão do tempo restante.

## Dados e limitações

O áudio é mantido só até a análise terminar. A API salva nome, URL canônica do YouTube quando aplicável, duração, tom, acordes separados por modelo e correções no PostgreSQL. Para reproduzir uma análise antiga de arquivo, selecione novamente o arquivo local. Vídeos são reproduzidos no player incorporado oficial. A estimativa de tom usa perfis cromáticos e pode errar em músicas com modulações.

O processamento de YouTube usa `yt-dlp` localmente para extrair temporariamente o áudio de vídeos públicos; vídeos privados, protegidos, transmissões ao vivo e restrições geográficas podem falhar. Esta função não transfere o áudio para o navegador nem o armazena após a análise. Uso pessoal não garante permissão para baixar conteúdo: verifique os [Termos do YouTube](https://www.youtube.com/t/terms) e os direitos do vídeo antes de usá-la. A disponibilidade da extração pode mudar quando o YouTube alterar seus mecanismos.

Se o YouTube responder com HTTP 429 ou pedir confirmação de que você não é um robô, a extração falha antes de qualquer modelo ser executado. A tela passa a mostrar essa causa. Para uso local, você pode colocar um arquivo de cookies no formato Netscape em `secrets/youtube-cookies.txt`, copiar `compose.cookies.example.yaml` para `compose.cookies.yaml` e reiniciar com `docker compose -f compose.yaml -f compose.cookies.yaml up -d --build processor api`. Ambos os caminhos são ignorados pelo Git e o arquivo é montado somente para leitura. Consulte [as instruções oficiais do yt-dlp sobre cookies](https://github.com/yt-dlp/yt-dlp/wiki/FAQ#how-do-i-pass-cookies-to-yt-dlp); cookies são credenciais da sua sessão e podem expirar. Sem cookies, você também pode enviar um MP3/WAV obtido legitimamente. Esta configuração não garante acesso a vídeos restritos nem contorna todos os bloqueios do YouTube.

Este MVP é local e não inclui contas de usuário. Não publique a API na Internet sem autenticação, autorização por música, limite de requisições e armazenamento temporário durável. O worker é único por instância; múltiplas réplicas exigem fila com reserva atômica de tarefas. Em reinício durante processamento, o áudio temporário pode ser perdido e a tarefa precisará ser reenviada.

O processador usa `lv-chordia` e o [BTC-ISMIR19](https://github.com/jayg996/BTC-ISMIR19). O Docker fixa o commit BTC e verifica o SHA-256 do checkpoint. O código de ambos é MIT, mas isso não confirma direitos comerciais separados para os pesos e dados de treinamento. Confirme esses direitos antes de uso comercial. Veja também [a pesquisa original do lv-chordia](https://github.com/music-x-lab/ISMIR2019-Large-Vocabulary-Chord-Recognition).

## Testes

Para medir reconhecimento com músicas completas e acordes de referência, consulte [a avaliação local](benchmark/README.md). O avaliador não altera os acordes salvos nem combina automaticamente detectores. Há uma medição exploratória com trecho de guitarra em [VALIDATION.md](benchmark/VALIDATION.md); ainda faltam músicas completas anotadas.

```sh
dotnet test backend/tests/ChordApp.UnitTests/ChordApp.UnitTests.csproj
dotnet test backend/tests/ChordApp.IntegrationTests/ChordApp.IntegrationTests.csproj
npm --prefix web run build
docker compose exec processor python -m unittest discover -s tests -v
```

Uma verificação real do modelo requer áudio de teste legalmente utilizável e Docker Compose em funcionamento.
