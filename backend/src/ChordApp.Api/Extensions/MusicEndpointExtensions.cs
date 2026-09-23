using ChordApp.Api.Contracts.Requests;
using ChordApp.Application.CorrectChord;
using ChordApp.Application.QueryMusic;
using ChordApp.Application.SubmitMusic;

namespace ChordApp.Api.Extensions;

/// <summary>Mapeia as rotas HTTP de músicas para os casos de uso da aplicação.</summary>
public static class MusicEndpointExtensions
{
    public static WebApplication MapMusicEndpoints(this WebApplication app)
    {
        app.MapGet("/api/musics", (ListMusics query, CancellationToken cancellationToken) =>
            query.ExecuteAsync(cancellationToken));

        app.MapGet("/api/musics/{id:guid}", async (
            Guid id,
            GetMusicDetails query,
            CancellationToken cancellationToken) =>
        {
            var music = await query.ExecuteAsync(id, cancellationToken);
            return music is null ? Results.NotFound() : Results.Ok(music);
        });

        app.MapPost("/api/musics/youtube", async (
            YouTubeRequest body,
            IMusicSubmission<YouTubeSource> submission,
            CancellationToken cancellationToken) =>
        {
            var result = await submission.SubmitAsync(new YouTubeSource(body.Url, body.Model), cancellationToken);
            return ToHttpResult(result, "URL de vídeo do YouTube inválida.");
        });

        app.MapPost("/api/musics", async (
            HttpRequest request,
            IMusicSubmission<AudioFileSource> submission,
            CancellationToken cancellationToken) =>
        {
            if (!request.HasFormContentType)
            {
                return Results.BadRequest(new { error = "Envie multipart/form-data." });
            }

            IFormFile? file;
            string? model;
            try
            {
                var form = await request.ReadFormAsync(cancellationToken);
                file = form.Files.GetFile("file");
                model = form["model"].FirstOrDefault();
            }
            catch (InvalidDataException)
            {
                return Results.BadRequest(new { error = "Formulário inválido." });
            }

            if (file is null)
            {
                return Results.BadRequest(new { error = "Arquivo vazio ou maior que 30 MB." });
            }

            await using var content = file.OpenReadStream();
            var source = new AudioFileSource(content, file.FileName, file.Length, model ?? "lv-chordia");
            var result = await submission.SubmitAsync(source, cancellationToken);

            return ToHttpResult(
                result,
                "Arquivo inválido, fora do tamanho permitido, formato não suportado ou duração incorreta.");
        }).DisableAntiforgery();

        app.MapPut("/api/musics/{id:guid}/model", async (
            Guid id,
            SelectModelRequest body,
            SelectMusicModel selection,
            GetMusicDetails query,
            CancellationToken cancellationToken) =>
        {
            var status = await selection.ExecuteAsync(id, body.Model, cancellationToken);
            if (status == SelectMusicModelStatus.NotFound) return Results.NotFound();
            if (status == SelectMusicModelStatus.Unavailable)
                return Results.BadRequest(new { error = "Modelo indisponível para esta música." });
            return Results.Ok(await query.ExecuteAsync(id, cancellationToken));
        });

        app.MapPut("/api/musics/{id:guid}/chords/{chordId:guid}", async (
            Guid id,
            Guid chordId,
            CorrectChord body,
            CorrectMusicChord correction,
            CancellationToken cancellationToken) =>
        {
            var result = await correction.ExecuteAsync(id, chordId, body.Chord, cancellationToken);

            return result.Status switch
            {
                CorrectChordStatus.NotFound => Results.NotFound(),
                CorrectChordStatus.InvalidChord => Results.BadRequest(new { error = "Acorde inválido." }),
                _ => Results.Ok(result.Chord)
            };
        });

        return app;
    }

    private static IResult ToHttpResult(SubmissionResult? result, string error)
    {
        return result is null
            ? Results.BadRequest(new { error })
            : Results.Accepted(
                $"/api/musics/{result.MusicId}",
                new { id = result.MusicId, status = "Pending" });
    }
}
