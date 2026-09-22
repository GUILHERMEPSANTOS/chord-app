# ChordApp

MVP local para reconhecer acordes maiores e menores em MP3/WAV e acompanhar o resultado em um player. API ASP.NET Core, processador Python, Next.js e PostgreSQL.

## Executar

Requisitos: Docker Desktop com Docker Compose e pelo menos 4 GB livres para o processador.

```sh
docker compose up --build
```

Abra http://localhost:3000. Na primeira construção, PyTorch e os pesos do modelo podem tornar o download demorado. A API fica em http://localhost:5000/health e o processador em sua rede interna do Compose.

## Contrato

- `POST /api/musics`: formulário `file` com MP3/WAV, até 30 MB e duração entre 1 e 900 segundos. Retorna `202` e o `id`.
- `GET /api/musics/{id}`: estado `Pending`, `Processing`, `Completed` ou `Failed`, tom estimado e segmentos.
- `GET /api/musics`: análises salvas.
- `PUT /api/musics/{id}/chords/{chordId}`: corpo `{ "chord": "Am" }` para correção.

Os segmentos têm `startTime`, `endTime`, `chord` e `confidence`. `confidence` fica `null` porque o modelo escolhido não fornece probabilidade calibrada de acerto por segmento. `N` representa ausência de acorde.

## Dados e limitações

O áudio é mantido só até a análise terminar. A API salva nome, duração, tom, acordes e correções no PostgreSQL. Para reproduzir uma análise antiga, selecione novamente o arquivo local. A estimativa de tom usa perfis cromáticos e pode errar em músicas com modulações.

Este MVP é local e não inclui contas de usuário. Não publique a API na Internet sem autenticação, autorização por música, limite de requisições e armazenamento temporário durável. O worker é único por instância; múltiplas réplicas exigem fila com reserva atômica de tarefas. Em reinício durante processamento, o áudio temporário pode ser perdido e a tarefa precisará ser reenviada.

O processador usa `lv-chordia`, que empacota pesos de um trabalho de pesquisa. A licença MIT do repositório não é prova suficiente de direitos comerciais dos pesos e dos dados originais. Confirme esses direitos antes de uso comercial. Veja [o projeto](https://github.com/openmirlab/lv-chordia) e [a pesquisa original](https://github.com/music-x-lab/ISMIR2019-Large-Vocabulary-Chord-Recognition).

## Testes

```sh
dotnet test backend/tests/ChordApp.UnitTests/ChordApp.UnitTests.csproj
npm --prefix web run build
```

Uma verificação real do modelo requer áudio de teste legalmente utilizável e Docker Compose em funcionamento.
