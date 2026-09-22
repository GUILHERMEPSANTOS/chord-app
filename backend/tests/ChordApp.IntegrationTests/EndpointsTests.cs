using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

public sealed class EndpointsTests : IClassFixture<TestFactory>
{
    private readonly HttpClient _client;
    public EndpointsTests(TestFactory factory) => _client = factory.CreateClient();

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
