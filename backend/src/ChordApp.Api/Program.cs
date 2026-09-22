using ChordApp.Api;
using ChordApp.Api.Contracts.Processor;
using ChordApp.Api.Contracts.Requests;
using ChordApp.Api.Contracts.Responses;
using ChordApp.Api.Extensions;
using ChordApp.Application.SubmitMusic;
using ChordApp.Domain;
using ChordApp.Domain.Rules;
using ChordApp.Infrastructure;
using ChordApp.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<MusicDb>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Database") ?? "Host=localhost;Database=chordapp;Username=chordapp;Password=chordapp"));

builder.Services.AddHttpClient("processor", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ProcessorUrl"] ?? "http://localhost:8000");
    client.Timeout = Timeout.InfiniteTimeSpan;
});

builder.Services.AddHostedService<AnalysisWorker>();
builder.Services.AddScoped<IMusicRepository, MusicRepository>();


builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(builder.Configuration["WebOrigin"] ?? "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()));

Directory.CreateDirectory(TempFiles.Root);

var app = builder.Build();
app.UseCors();
await app.InitializeMusicDatabaseAsync();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/musics", async (MusicDb db) =>
{
    var items = await db.Musics.AsNoTracking().OrderByDescending(m => m.CreatedAt)
        .Select(m => new { m.Id, m.FileName, m.DurationSeconds, m.Key, m.Status, m.CreatedAt }).ToListAsync();
    return items.Select(m => new { m.Id, m.FileName, m.DurationSeconds, m.Key, Status = m.Status.ToString(), m.CreatedAt });
});

app.MapGet("/api/musics/{id:guid}", async (Guid id, MusicDb db, IHttpClientFactory clients, CancellationToken ct) =>
{
    var m = await db.Musics.AsNoTracking().Include(x => x.Chords).FirstOrDefaultAsync(x => x.Id == id);
    if (m is null) return Results.NotFound();
    var percent = m.Status == AnalysisStatus.Completed ? 100 : 0;
    string? stage = null;
    if (m.Status == AnalysisStatus.Processing)
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
    return Results.Ok(new MusicResponse(m.Id, m.FileName, m.SourceUrl, m.DurationSeconds, m.Key, m.Status.ToString(), m.Error, percent, stage, m.Chords.OrderBy(c => c.StartTime).Select(c => new ChordResponse(c.Id, c.StartTime, c.EndTime, c.Chord, c.Confidence, c.Corrected)).ToList()));
});

app.MapPost("/api/musics/youtube", async (YouTubeRequest body, IMusicSubmission<YouTubeSource> submissionService, CancellationToken ct) =>
{
    var source = new YouTubeSource(body.Url);
    var result = await submissionService.SubmitAsync(source, ct);

    if (result is null)
        return Results.BadRequest(new { error = "URL de vídeo do YouTube inválida." });

    return Results.Accepted($"/api/musics/{result.MusicId}", new { id = result.MusicId, status = "Pending" });
});

app.MapPost("/api/musics", async (HttpRequest request, IMusicSubmission<AudioFileSource> submissionService, CancellationToken ct) =>
{    
    if (!request.HasFormContentType)
        return Results.BadRequest(new { error = "Envie multipart/form-data." });

    IFormFile? file;
    try
    {
        file = (await request.ReadFormAsync(ct)).Files.GetFile("file");
    }
    catch (InvalidDataException)
    {
        return Results.BadRequest(new { error = "Formulário inválido." });
    }

    if (file is null)
        return Results.BadRequest(new { error = "Arquivo não enviado." });
    
    await using var stream = file.OpenReadStream();
    
    var source = new AudioFileSource(stream, file.FileName, file.Length);    
    var result = await submissionService.SubmitAsync(source, ct);


    if (result is null)
    {
        return Results.BadRequest(new { error = "Arquivo inválido, fora do tamanho permitido, formato não suportado ou duração incorreta." });
    }

    return Results.Accepted($"/api/musics/{result.MusicId}", new { id = result.MusicId, status = "Pending" });
}).DisableAntiforgery();

app.MapPut("/api/musics/{id:guid}/chords/{chordId:guid}", async (Guid id, Guid chordId, CorrectChord body, MusicDb db) =>
{
    var chord = await db.Chords.FirstOrDefaultAsync(c => c.Id == chordId && c.MusicId == id);
    if (chord is null) return Results.NotFound();
    if (!ChordRules.IsAllowed(body.Chord)) return Results.BadRequest(new { error = "Acorde inválido." });
    chord.Chord = body.Chord; chord.Corrected = true; chord.Confidence = null;
    await db.SaveChangesAsync();
    return Results.Ok(new ChordResponse(chord.Id, chord.StartTime, chord.EndTime, chord.Chord, chord.Confidence, chord.Corrected));
});
app.Run();
public partial class Program { }
