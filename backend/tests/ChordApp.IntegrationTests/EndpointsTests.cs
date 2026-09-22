using System.Net;
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
}

public sealed class TestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<MusicDb>>();
            foreach (var item in services.Where(s => s.ServiceType.IsGenericType && s.ServiceType.GetGenericTypeDefinition().Name.StartsWith("IDbContextOptionsConfiguration")).ToArray()) services.Remove(item);
            services.AddDbContext<MusicDb>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
            services.RemoveAll<IHostedService>();
        });
    }
}
