using System.Net.Http.Json;
using ChordApp.Api.Contracts.Processor;
using ChordApp.Api.Contracts.Requests;
using ChordApp.Api.Contracts.Responses;
using ChordApp.Application.SubmitMusic;
using ChordApp.Domain;
using ChordApp.Domain.Rules;
using ChordApp.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ChordApp.Api.Extensions;

public static class MusicEndpointExtensions
{
    public static WebApplication MapMusicEndpoints(this WebApplication app)
    {
        app.MapGet("/api/musics", async (MusicDb db) =>
        {
            var items = await db.Musics.AsNoTracking().OrderByDescending(m => m.CreatedAt)
                .Select(m => new { m.Id, m.FileName, m.DurationSeconds, m.Key, m.Status, m.CreatedAt }).ToListAsync();
            return items.Select(m => new { m.Id, m.FileName, m.DurationSeconds, m.Key, Status = m.Status.ToString(), m.CreatedAt });
        });

        app.MapGet("/api/musics/{id:guid}", async (Guid id, MusicDb db, IHttpClientFactory clients, CancellationToken ct) =>
        {
            var music = await db.Musics.AsNoTracking().Include(m => m.Chords).FirstOrDefaultAsync(m => m.Id == id, ct);
            if (music is null) return Results.NotFound();

            var percent = music.Status == AnalysisStatus.Completed ? 100 : 0;
            string? stage = null;
            if (music.Status == AnalysisStatus.Processing)
            {
                percent = 2;
                stage = "Preparando processamento";
                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    timeout.CancelAfter(TimeSpan.FromSeconds(2));
                    var snapshot = await clients.CreateClient("processor").GetFromJsonAsync<ProgressSnapshot>($"/progress/{id}", timeout.Token);
                    if (snapshot is not null) { percent = Math.Clamp(snapshot.Percent, 0, 100); stage = snapshot.Stage; }
                }
                catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException) { }
            }

            return Results.Ok(new MusicResponse(music.Id, music.FileName, music.SourceUrl, music.DurationSeconds, music.Key,
                music.Status.ToString(), music.Error, percent, stage,
                music.Chords.OrderBy(c => c.StartTime)
                    .Select(c => new ChordResponse(c.Id, c.StartTime, c.EndTime, c.Chord, c.Confidence, c.Corrected)).ToList()));
        });

        app.MapPost("/api/musics/youtube", async (YouTubeRequest body, IMusicSubmission<YouTubeSource> submission, CancellationToken ct) =>
            ToHttpResult(await submission.SubmitAsync(new YouTubeSource(body.Url), ct), "URL de vídeo do YouTube inválida."));

        app.MapPost("/api/musics", async (HttpRequest request, IMusicSubmission<AudioFileSource> submission, CancellationToken ct) =>
        {
            if (!request.HasFormContentType) return Results.BadRequest(new { error = "Envie multipart/form-data." });
            IFormFile? file;
            try { file = (await request.ReadFormAsync(ct)).Files.GetFile("file"); }
            catch (InvalidDataException) { return Results.BadRequest(new { error = "Formulário inválido." }); }
            if (file is null) return Results.BadRequest(new { error = "Arquivo vazio ou maior que 30 MB." });

            await using var content = file.OpenReadStream();
            return ToHttpResult(await submission.SubmitAsync(new AudioFileSource(content, file.FileName, file.Length), ct),
                "Arquivo inválido, fora do tamanho permitido, formato não suportado ou duração incorreta.");
        }).DisableAntiforgery();

        app.MapPut("/api/musics/{id:guid}/chords/{chordId:guid}", async (Guid id, Guid chordId, CorrectChord body, MusicDb db, CancellationToken ct) =>
        {
            var chord = await db.Chords.FirstOrDefaultAsync(c => c.Id == chordId && c.MusicId == id, ct);
            if (chord is null) return Results.NotFound();
            if (!ChordRules.IsAllowed(body.Chord)) return Results.BadRequest(new { error = "Acorde inválido." });
            chord.Chord = body.Chord;
            chord.Corrected = true;
            chord.Confidence = null;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new ChordResponse(chord.Id, chord.StartTime, chord.EndTime, chord.Chord, chord.Confidence, chord.Corrected));
        });

        return app;
    }

    private static IResult ToHttpResult(SubmissionResult? result, string error) => result is null
        ? Results.BadRequest(new { error })
        : Results.Accepted($"/api/musics/{result.MusicId}", new { id = result.MusicId, status = "Pending" });
}
