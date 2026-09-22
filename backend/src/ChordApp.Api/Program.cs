using ChordApp.Domain;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<MusicDb>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Database") ?? "Host=localhost;Database=chordapp;Username=chordapp;Password=chordapp"));
builder.Services.AddHttpClient("processor", c => { c.BaseAddress = new Uri(builder.Configuration["ProcessorUrl"] ?? "http://localhost:8000"); c.Timeout = Timeout.InfiniteTimeSpan; });
builder.Services.AddHostedService<AnalysisWorker>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(builder.Configuration["WebOrigin"] ?? "http://localhost:3000").AllowAnyHeader().AllowAnyMethod()));
var app = builder.Build();
app.UseCors();
Directory.CreateDirectory(TempFiles.Root);
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MusicDb>();
    await db.Database.EnsureCreatedAsync();
    foreach (var interrupted in await db.Musics.Where(m => m.Status == AnalysisStatus.Processing).ToListAsync())
    {
        interrupted.Status = AnalysisStatus.Failed;
        interrupted.Error = "Processamento interrompido; envie o áudio novamente.";
        TempFiles.Delete(interrupted.Id);
    }
    await db.SaveChangesAsync();
}
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
app.MapPost("/api/musics/youtube", async (YouTubeRequest body, MusicDb db, CancellationToken ct) =>
{
    var canonical = YouTubeUrls.Canonical(body.Url);
    if (canonical is null) return Results.BadRequest(new { error = "URL de vídeo do YouTube inválida." });
    var id = Guid.NewGuid();
    var music = new Music { Id = id, FileName = $"YouTube {canonical.Split('=')[1]}", SourceUrl = canonical };
    db.Musics.Add(music);
    await db.SaveChangesAsync(ct);
    return Results.Accepted($"/api/musics/{id}", new { id, status = "Pending" });
});
app.MapPost("/api/musics", async (HttpRequest request, MusicDb db, CancellationToken ct) =>
{
    if (!request.HasFormContentType) return Results.BadRequest(new { error = "Envie multipart/form-data." });
    IFormFile? file;
    try { file = (await request.ReadFormAsync(ct)).Files.GetFile("file"); }
    catch (InvalidDataException) { return Results.BadRequest(new { error = "Formulário inválido." }); }
    if (file is null || file.Length == 0 || file.Length > 30 * 1024 * 1024) return Results.BadRequest(new { error = "Arquivo vazio ou maior que 30 MB." });
    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (ext is not (".mp3" or ".wav")) return Results.BadRequest(new { error = "Apenas MP3 ou WAV." });
    var id = Guid.NewGuid();
    var path = TempFiles.PathFor(id, ext);
    try
    {
        await using (var output = File.Create(path)) await file.CopyToAsync(output, ct);
        if (!await AudioValidation.HasSignatureAsync(path, ext, ct)) return Results.BadRequest(new { error = "Conteúdo não corresponde ao formato." });
        var duration = await AudioValidation.DurationAsync(path, ct);
        if (duration is null || duration < 1 || duration > 900) return Results.BadRequest(new { error = "Áudio inválido ou duração fora de 1 a 900 segundos." });
        db.Musics.Add(new Music { Id = id, FileName = Path.GetFileName(file.FileName), DurationSeconds = duration.Value });
        await db.SaveChangesAsync(ct);
        return Results.Accepted($"/api/musics/{id}", new { id, status = "Pending" });
    }
    finally { if (!await db.Musics.AnyAsync(m => m.Id == id, CancellationToken.None)) TempFiles.Delete(id); }
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
