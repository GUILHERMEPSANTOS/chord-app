# Decisões técnicas do MVP

## Viabilidade e escolha do modelo

É viável analisar músicas completas localmente, mas acordes de gravações com voz, bateria, baixo e muitos instrumentos são estimativas. A precisão varia por gênero, mixagem e qualidade do áudio. O tempo de processamento em CPU depende da duração e da máquina; medir antes de prometer latência.

| Opção | Vantagens | Limitações para este MVP |
| --- | --- | --- |
| [lv-chordia](https://github.com/openmirlab/lv-chordia) | Modelo pronto para áudio polifônico, execução Python local, saída temporal; licença MIT no código. | Dependências de ML grandes, inferência em CPU potencialmente lenta, confiança por segmento indisponível, direitos comerciais dos pesos precisam de confirmação. |
| [BTC / ISMIR 2019](https://github.com/jayg996/BTC-ISMIR19) | Pesquisa focada em acordes de música completa e saídas temporais. | Integração e manutenção do código de pesquisa exigem mais trabalho; conferir licença dos pesos separadamente. |
| [Essentia](https://essentia.upf.edu/) | Biblioteca madura de áudio, execução local e recursos de acordes. | Licença AGPL ou comercial conforme o uso; método clássico tende a ser uma referência, não o modelo neural escolhido. |
| [madmom](https://github.com/CPJKU/madmom) | Ferramentas consolidadas de MIR e processamento temporal. | Manutenção e compatibilidade com Python moderno exigem verificação; integração menos direta para o escopo de 24 classes. |

Escolha: `lv-chordia` para o primeiro protótipo, simplificando as classes em 12 maiores, 12 menores e `N`. O tom é estimado separadamente por cromagrama do `librosa`; não é uma probabilidade calibrada. Esta escolha deve ser reavaliada com um conjunto de teste próprio. Para produto comercial, confirmar licença dos pesos e datasets antes do lançamento.

## Fluxo e responsabilidades

1. Next.js envia MP3/WAV à API ou uma URL canônica do YouTube.
2. A API valida extensão, assinatura e duração do arquivo, ou host e ID da URL. Persiste um trabalho `Pending` no PostgreSQL.
3. O worker da API marca `Processing` e chama o FastAPI. Arquivos locais e extrações de YouTube ficam em diretórios temporários.
4. FFmpeg converte para WAV mono a 22,05 kHz. `lv-chordia` e BTC-ISMIR19 produzem segmentos independentes; `librosa` estima o tom.
5. A API valida e grava os segmentos com a identificação do modelo. Um erro no BTC não apaga o resultado do `lv-chordia`. Os arquivos temporários são removidos.
6. A interface consulta o estado periodicamente, permite escolher qual resultado salvo exibir e sincroniza seus segmentos com o player. Correções são gravadas por segmento e preservadas ao alternar.

## Etapas de implementação

1. Contratos e domínio: estado da análise, 24 acordes, `N`, intervalos e validação.
2. API e banco: upload, consultas, correção, PostgreSQL.
3. Worker e processador: FFmpeg, modelo pronto, estimativa do tom.
4. Interface: upload, consulta de estado, player, destaque e edição.
5. YouTube: validação estrita da URL, extração temporária e player incorporado.
6. Testes de regras e endpoints; teste integrado com áudio de referência.

## Medição de qualidade

Monte um conjunto legalmente utilizável e anotado, incluindo músicas completas de estilos diferentes. Compare cada instante da previsão com o acorde anotado, contando `N` e intervalos ambíguos. Métrica principal: *weighted chord symbol recall* (tempo com acorde correto dividido pelo tempo anotado). Relate também acerto de raiz, acerto maior/menor, erro médio das mudanças de acorde, acerto do tom e latência por minuto de áudio em CPU. Separe resultados por estilo e por músicas fora da distribuição de teste. Como referência inicial de aceitação, **estimativa a validar**, busque pelo menos 70% de recall nas 24 classes; não é garantia de desempenho do modelo.

## Custos e riscos

Execução local dispensa cobrança de API de IA, mas exige espaço para imagens Docker, modelo e arquivos temporários, além de CPU e memória. Estimativa inicial: reservar ao menos 4 GB de RAM para o processador; medir no hardware alvo. Repositório privado GitHub pode caber no plano gratuito sujeito aos limites vigentes. O áudio de arquivo não é salvo após a análise. O resultado salvo não permite ouvir novamente sem selecionar o arquivo. O YouTube pode impedir extração ou alterar seu funcionamento; respeite direitos autorais e os termos da plataforma. Não use esta configuração sem autenticação em um serviço público.

## Estrutura

```text
backend/src/ChordApp.Domain/       entidades e regras
backend/src/ChordApp.Api/          HTTP, persistência e worker
backend/tests/                     testes unitários e integração
processor/app/                     FastAPI, FFmpeg e modelo
web/app/                           página Next.js
web/components/                    player YouTube
compose.yaml                        ambiente local
```
