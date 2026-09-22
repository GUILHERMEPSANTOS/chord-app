# ChordApp

MVP local para reconhecer acordes maiores e menores em MP3/WAV ou vídeos públicos do YouTube e acompanhar o resultado em um player. API ASP.NET Core, processador Python, Next.js e PostgreSQL.

## Executar

Requisitos: Docker Desktop com Docker Compose e pelo menos 4 GB livres para o processador.

```sh
docker compose up --build
```

Abra http://localhost:3000. Na primeira construção, PyTorch e os pesos do modelo podem tornar o download demorado. A API fica em http://localhost:5000/health e o processador em sua rede interna do Compose.
O modelo executa um conjunto de cinco redes em CPU e pode levar vários minutos até para áudios curtos. A latência em músicas completas precisa ser medida no seu computador antes de usar em produção.

## Contrato

- `POST /api/musics`: formulário `file` com MP3/WAV, até 30 MB e duração entre 1 e 900 segundos. Retorna `202` e o `id`.
- `POST /api/musics/youtube`: corpo `{ "url": "https://www.youtube.com/watch?v=..." }` para vídeo público. Retorna `202` e o `id`.
- `GET /api/musics/{id}`: estado `Pending`, `Processing`, `Completed` ou `Failed`, `progressPercent`, `progressStage`, tom estimado e segmentos.
- `GET /api/musics`: análises salvas.
- `PUT /api/musics/{id}/chords/{chordId}`: corpo `{ "chord": "Am" }` para correção.

Os segmentos têm `startTime`, `endTime`, `chord` e `confidence`. `confidence` fica `null` porque o modelo escolhido não fornece probabilidade calibrada de acerto por segmento. `N` representa ausência de acorde.
`progressPercent` mostra marcos do download, normalização e cinco passagens do modelo. É uma porcentagem aproximada das etapas concluídas, não uma previsão do tempo restante. A tela destaca o acorde atual, mostra os próximos acordes e permite corrigir cada segmento na lista.

## Dados e limitações

O áudio é mantido só até a análise terminar. A API salva nome, URL canônica do YouTube quando aplicável, duração, tom, acordes e correções no PostgreSQL. Para reproduzir uma análise antiga de arquivo, selecione novamente o arquivo local. Vídeos são reproduzidos no player incorporado oficial. A estimativa de tom usa perfis cromáticos e pode errar em músicas com modulações.

O processamento de YouTube usa `yt-dlp` localmente para extrair temporariamente o áudio de vídeos públicos; vídeos privados, protegidos, transmissões ao vivo e restrições geográficas podem falhar. Esta função não transfere o áudio para o navegador nem o armazena após a análise. Uso pessoal não garante permissão para baixar conteúdo: verifique os [Termos do YouTube](https://www.youtube.com/t/terms) e os direitos do vídeo antes de usá-la. A disponibilidade da extração pode mudar quando o YouTube alterar seus mecanismos.

Este MVP é local e não inclui contas de usuário. Não publique a API na Internet sem autenticação, autorização por música, limite de requisições e armazenamento temporário durável. O worker é único por instância; múltiplas réplicas exigem fila com reserva atômica de tarefas. Em reinício durante processamento, o áudio temporário pode ser perdido e a tarefa precisará ser reenviada.

O processador usa `lv-chordia`, que empacota pesos de um trabalho de pesquisa. A licença MIT do repositório não é prova suficiente de direitos comerciais dos pesos e dos dados originais. Confirme esses direitos antes de uso comercial. Veja [o projeto](https://github.com/openmirlab/lv-chordia) e [a pesquisa original](https://github.com/music-x-lab/ISMIR2019-Large-Vocabulary-Chord-Recognition).

## Testes

```sh
dotnet test backend/tests/ChordApp.UnitTests/ChordApp.UnitTests.csproj
dotnet test backend/tests/ChordApp.IntegrationTests/ChordApp.IntegrationTests.csproj
npm --prefix web run build
docker compose exec processor python -m unittest discover -s tests -v
```

Uma verificação real do modelo requer áudio de teste legalmente utilizável e Docker Compose em funcionamento.
