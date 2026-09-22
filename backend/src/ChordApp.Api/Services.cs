using System.Net.Http.Json;
using System.Text.Json;
using ChordApp.Api.Contracts.Processor;
using ChordApp.Domain;
using ChordApp.Domain.Rules;
using ChordApp.Infrastructure;
using Microsoft.EntityFrameworkCore;


namespace ChordApp.Api;

public sealed class AnalysisWorker(IServiceScopeFactory scopes, IHttpClientFactory clients, ILogger<AnalysisWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<MusicDb>();

            var music = await db.Musics.FirstOrDefaultAsync(m => m.Status == AnalysisStatus.Pending, stoppingToken);

            if (music is null)
            {
                await Task.Delay(1500, stoppingToken); continue;
            }

            var path = TempFiles.Find(music.Id);

            if (path is null && music.SourceUrl is null)
            {
                music.Status = AnalysisStatus.Failed; music.Error = "Arquivo temporário indisponível."; await db.SaveChangesAsync(stoppingToken);
                continue;
            }

            music.Status = AnalysisStatus.Processing;
            await db.SaveChangesAsync(stoppingToken);

            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                timeout.CancelAfter(TimeSpan.FromHours(6));
                HttpResponseMessage response;
                if (music.SourceUrl is not null)
                    response = await clients.CreateClient("processor").PostAsJsonAsync("/analyze-youtube", new
                    {
                        url = music.SourceUrl,
                        jobId = music.Id.ToString()
                    }, timeout.Token);
                else
                {
                    using var form = new MultipartFormDataContent();
                    await using var input = File.OpenRead(path!);
                    using var content = new StreamContent(input);
                    form.Add(content, "file", Path.GetFileName(path!));
                    form.Add(new StringContent(music.Id.ToString()), "job_id");
                    response = await clients.CreateClient("processor").PostAsync("/analyze", form, timeout.Token);
                }
                using (response)
                {
                    response.EnsureSuccessStatusCode();
                    var result = await response.Content.ReadFromJsonAsync<ProcessResult>(new JsonSerializerOptions(JsonSerializerDefaults.Web), timeout.Token) ?? throw new InvalidDataException("Resposta vazia.");
                    if (result.DurationSeconds is double measured) music.DurationSeconds = measured;
                    var segments = new List<ChordSegment>();
                    foreach (var c in result.Chords)
                    {
                        if (!double.IsFinite(c.StartTime) || !double.IsFinite(c.EndTime)) throw new InvalidDataException("Tempo inválido no resultado.");
                        var start = Math.Max(0, c.StartTime);
                        var end = Math.Min(music.DurationSeconds, c.EndTime);
                        if (end <= start) continue;
                        ChordRules.Validate(start, end, music.DurationSeconds, c.Chord);
                        segments.Add(new ChordSegment { MusicId = music.Id, StartTime = start, EndTime = end, Chord = c.Chord, Confidence = c.Confidence });
                    }
                    db.Chords.AddRange(segments);
                    music.Key = result.Key;
                    music.Status = AnalysisStatus.Completed;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Falha ao analisar música {MusicId}", music.Id);
                music.Status = AnalysisStatus.Failed;
                music.Error = "Falha no processamento. Consulte os logs.";
            }
            finally
            {
                await db.SaveChangesAsync(CancellationToken.None);
                TempFiles.Delete(music.Id);
            }
        }
    }
}
