using System.Net;
using System.Net.Http.Json;
using ChordApp.Domain;
using ChordApp.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

public sealed class EndpointsTests : IClassFixture<TestFactory>
{
    private readonly HttpClient _client;
    private readonly TestFactory _factory;
    public EndpointsTests(TestFactory factory) { _factory = factory; _client = factory.CreateClient(); }

    [Fact]
    public async Task HealthReturnsOk() => Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/health")).StatusCode);

    [Fact]
    public async Task MissingMusicReturnsNotFound() => Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/musics/{Guid.NewGuid()}")).StatusCode);

    [Fact]
    public async Task UploadRejectsUnsupportedExtension()
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent([1, 2, 3]), "file", "track.txt");
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsync("/api/musics", form)).StatusCode);
    }

    [Fact]
    public async Task YouTubeRejectsUntrustedHost()
    {
        var response = await _client.PostAsJsonAsync("/api/musics/youtube", new { url = "https://youtube.com.evil.example/watch?v=abcdefghijk" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task YouTubeCreatesPendingJob()
    {
        var response = await _client.PostAsJsonAsync("/api/musics/youtube", new { url = "https://youtu.be/abcdefghijk" });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CreatedJob>();
        Assert.NotNull(created);
        var details = await _client.GetAsync($"/api/musics/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        var saved = await details.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(0, saved.GetProperty("progressPercent").GetInt32());
        var list = await _client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/musics");
        Assert.Equal("Pending", list[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task CorrectChordAcceptsSeventhAndPersistsIt()
    {
        var music = new Music { DurationSeconds = 10, Status = AnalysisStatus.Completed };
        var segment = new ChordSegment { MusicId = music.Id, StartTime = 0, EndTime = 5, Chord = "C" };
        music.Chords.Add(segment);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MusicDb>();
            db.Musics.Add(music);
            await db.SaveChangesAsync();
        }

        var response = await _client.PutAsJsonAsync($"/api/musics/{music.Id}/chords/{segment.Id}", new { chord = "Cmaj7" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("Cmaj7", saved.GetProperty("chord").GetString());
        Assert.True(saved.GetProperty("corrected").GetBoolean());
        var unavailable = await _client.PutAsJsonAsync($"/api/musics/{music.Id}/model", new { model = RecognitionModels.BtcIsmir19 });
        Assert.Equal(HttpStatusCode.BadRequest, unavailable.StatusCode);
    }

    [Fact]
    public async Task SwitchingModelPreservesSeparateCorrections()
    {
        var music = new Music { DurationSeconds = 10, Status = AnalysisStatus.Completed };
        var lv = new ChordSegment { MusicId = music.Id, Model = RecognitionModels.LvChordia, StartTime = 0, EndTime = 5, Chord = "C" };
        var btc = new ChordSegment { MusicId = music.Id, Model = RecognitionModels.BtcIsmir19, StartTime = 0, EndTime = 5, Chord = "G" };
        music.Chords.AddRange([lv, btc]);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MusicDb>();
            db.Musics.Add(music);
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/musics/{music.Id}/chords/{lv.Id}", new { chord = "Cmaj7" })).StatusCode);
        var switched = await _client.PutAsJsonAsync($"/api/musics/{music.Id}/model", new { model = RecognitionModels.BtcIsmir19 });
        Assert.Equal(HttpStatusCode.OK, switched.StatusCode);
        var btcView = await switched.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(RecognitionModels.BtcIsmir19, btcView.GetProperty("selectedModel").GetString());
        Assert.Equal("G", btcView.GetProperty("chords")[0].GetProperty("chord").GetString());

        var back = await _client.PutAsJsonAsync($"/api/musics/{music.Id}/model", new { model = RecognitionModels.LvChordia });
        var lvView = await back.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("Cmaj7", lvView.GetProperty("chords")[0].GetProperty("chord").GetString());
        Assert.True(lvView.GetProperty("chords")[0].GetProperty("corrected").GetBoolean());
    }

    private sealed record CreatedJob(Guid Id, string Status);
}

public sealed class TestFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = "test-" + Guid.NewGuid();
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<MusicDb>>();
            foreach (var item in services.Where(s => s.ServiceType.IsGenericType && s.ServiceType.GetGenericTypeDefinition().Name.StartsWith("IDbContextOptionsConfiguration")).ToArray()) services.Remove(item);
            services.AddDbContext<MusicDb>(o => o.UseInMemoryDatabase(_databaseName));
            services.RemoveAll<IHostedService>();
        });
    }
}
