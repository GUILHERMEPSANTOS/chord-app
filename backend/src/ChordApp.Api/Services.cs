using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using ChordApp.Domain;
using Microsoft.EntityFrameworkCore;

public sealed record CorrectChord(string Chord);
public sealed record ChordResponse(Guid Id, double StartTime, double EndTime, string Chord, double? Confidence, bool Corrected);
public sealed record MusicResponse(Guid Id, string FileName, double DurationSeconds, string? Key, string Status, string? Error, List<ChordResponse> Chords);
public sealed record ProcessResult(string? Key, List<ProcessChord> Chords);
public sealed record ProcessChord(double StartTime, double EndTime, string Chord, double? Confidence);

public sealed class MusicDb(DbContextOptions<MusicDb> options) : DbContext(options)
{
    public DbSet<Music> Musics => Set<Music>();
    public DbSet<ChordSegment> Chords => Set<ChordSegment>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Music>().HasMany(m => m.Chords).WithOne().HasForeignKey(c => c.MusicId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Music>().Property(m => m.Status).HasConversion<string>();
    }
}

public static class TempFiles
{
    public static string Root => Environment.GetEnvironmentVariable("AUDIO_TEMP_DIR") ?? Path.Combine(Path.GetTempPath(), "chord-app");
    public static string PathFor(Guid id, string ext) => Path.Combine(Root, id + ext);
    public static string? Find(Guid id) => new[] { PathFor(id, ".mp3"), PathFor(id, ".wav") }.FirstOrDefault(File.Exists);
    public static void Delete(Guid id) { foreach (var ext in new[] { ".mp3", ".wav" }) { var path = PathFor(id, ext); if (File.Exists(path)) File.Delete(path); } }
}

public static class AudioValidation
{
    public static async Task<bool> HasSignatureAsync(string path, string ext, CancellationToken ct)
    {
        var bytes = new byte[12];
        await using var stream = File.OpenRead(path);
        if (await stream.ReadAsync(bytes, ct) < 12) return false;
        return ext == ".wav" ? bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WAVE"u8)
            : bytes.AsSpan(0, 3).SequenceEqual("ID3"u8) || (bytes[0] == 0xFF && (bytes[1] & 0xE0) == 0xE0);
    }
    public static async Task<double?> DurationAsync(string path, CancellationToken ct)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo("ffprobe") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false } };
        foreach (var arg in new[] { "-v", "error", "-show_entries", "format=duration", "-of", "default=noprint_wrappers=1:nokey=1", path }) process.StartInfo.ArgumentList.Add(arg);
        try
        {
            process.Start();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            var output = await process.StandardOutput.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            return process.ExitCode == 0 && double.TryParse(output.Trim(), System.Globalization.CultureInfo.InvariantCulture, out var duration) && double.IsFinite(duration) ? duration : null;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or OperationCanceledException) { if (!process.HasExited) process.Kill(); return null; }
    }
}

public sealed class AnalysisWorker(IServiceScopeFactory scopes, IHttpClientFactory clients, ILogger<AnalysisWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<MusicDb>();
            var music = await db.Musics.FirstOrDefaultAsync(m => m.Status == AnalysisStatus.Pending, stoppingToken);
            if (music is null) { await Task.Delay(1500, stoppingToken); continue; }
            var path = TempFiles.Find(music.Id);
            if (path is null) { music.Status = AnalysisStatus.Failed; music.Error = "Arquivo temporário indisponível."; await db.SaveChangesAsync(stoppingToken); continue; }
            music.Status = AnalysisStatus.Processing;
            await db.SaveChangesAsync(stoppingToken);
            try
            {
                using var form = new MultipartFormDataContent();
                await using var input = File.OpenRead(path);
                using var content = new StreamContent(input);
                form.Add(content, "file", Path.GetFileName(path));
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                timeout.CancelAfter(TimeSpan.FromMinutes(10));
                var response = await clients.CreateClient("processor").PostAsync("/analyze", form, timeout.Token);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<ProcessResult>(new JsonSerializerOptions(JsonSerializerDefaults.Web), timeout.Token) ?? throw new InvalidDataException("Resposta vazia.");
                foreach (var c in result.Chords)
                {
                    ChordRules.Validate(c.StartTime, c.EndTime, music.DurationSeconds, c.Chord);
                    music.Chords.Add(new ChordSegment { StartTime = c.StartTime, EndTime = c.EndTime, Chord = c.Chord, Confidence = c.Confidence });
                }
                music.Key = result.Key;
                music.Status = AnalysisStatus.Completed;
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
