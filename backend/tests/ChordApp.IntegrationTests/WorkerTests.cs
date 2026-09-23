using ChordApp.Domain;
using ChordApp.Infrastructure;
using ChordApp.Worker.Contracts;
using ChordApp.Worker.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class WorkerTests
{
    [Fact]
    public async Task MapperSavesIndependentModelsAndSelectedModel()
    {
        await using var db = CreateDb();
        var music = new Music
        {
            DurationSeconds = 10,
            SelectedModel = RecognitionModels.BtcIsmir19
        };
        db.Musics.Add(music);
        var result = new ProcessResult(
            "C",
            10,
            [],
            new Dictionary<string, List<ProcessChord>>
            {
                [RecognitionModels.LvChordia] = [new(0, 5, "C", null)],
                [RecognitionModels.BtcIsmir19] = [new(0, 5, "G7", null)]
            },
            null);

        new AnalysisResultMapper().Apply(db, music, result);
        await db.SaveChangesAsync();

        Assert.Equal(AnalysisStatus.Completed, music.Status);
        Assert.Equal(RecognitionModels.BtcIsmir19, music.SelectedModel);
        Assert.Equal(2, await db.Chords.CountAsync());
    }

    [Fact]
    public async Task RestartRecoveryFailsOnlyInterruptedJobs()
    {
        await using var db = CreateDb();
        var interrupted = new Music { Status = AnalysisStatus.Processing };
        var pending = new Music { Status = AnalysisStatus.Pending };
        db.Musics.AddRange(interrupted, pending);
        await db.SaveChangesAsync();

        await new InterruptedJobRecovery(db).RecoverAsync(CancellationToken.None);

        Assert.Equal(AnalysisStatus.Failed, interrupted.Status);
        Assert.Equal(AnalysisStatus.Pending, pending.Status);
    }

    [Fact]
    public async Task MissingUploadFailsWithoutCallingProcessor()
    {
        await using var db = CreateDb();
        var music = new Music { Status = AnalysisStatus.Pending };
        db.Musics.Add(music);
        await db.SaveChangesAsync();
        var client = new NeverCalledProcessorClient();
        var runner = new AnalysisJobRunner(
            db,
            client,
            new AnalysisResultMapper(),
            NullLogger<AnalysisJobRunner>.Instance);

        Assert.True(await runner.TryRunNextAsync(CancellationToken.None));
        Assert.Equal(AnalysisStatus.Failed, music.Status);
        Assert.False(client.Called);
    }

    [Fact]
    public async Task YouTubeJobPersistsAnalysisFromProcessor()
    {
        await using var db = CreateDb();
        var music = new Music
        {
            SourceUrl = "https://www.youtube.com/watch?v=abcdefghijk",
            Status = AnalysisStatus.Pending
        };
        db.Musics.Add(music);
        await db.SaveChangesAsync();
        var client = new StubProcessorClient(new ProcessResult(
            "C", 4, [new ProcessChord(0, 4, "C", null)], null, null));
        var runner = new AnalysisJobRunner(
            db, client, new AnalysisResultMapper(), NullLogger<AnalysisJobRunner>.Instance);

        Assert.True(await runner.TryRunNextAsync(CancellationToken.None));

        Assert.Equal(AnalysisStatus.Completed, music.Status);
        Assert.Equal("C", music.Key);
        Assert.True(client.Called);
        Assert.Single(await db.Chords.ToListAsync());
    }

    [Fact]
    public async Task ProcessorRejectionPersistsFailureReason()
    {
        await using var db = CreateDb();
        var music = new Music
        {
            SourceUrl = "https://www.youtube.com/watch?v=abcdefghijk",
            Status = AnalysisStatus.Pending
        };
        db.Musics.Add(music);
        await db.SaveChangesAsync();
        var client = new StubProcessorClient(new ProcessorRejectedException("Áudio inválido."));
        var runner = new AnalysisJobRunner(
            db, client, new AnalysisResultMapper(), NullLogger<AnalysisJobRunner>.Instance);

        Assert.True(await runner.TryRunNextAsync(CancellationToken.None));

        Assert.Equal(AnalysisStatus.Failed, music.Status);
        Assert.Equal("Áudio inválido.", music.Error);
        Assert.Empty(await db.Chords.ToListAsync());
    }

    private static MusicDb CreateDb() =>
        new(new DbContextOptionsBuilder<MusicDb>()
            .UseInMemoryDatabase("worker-" + Guid.NewGuid())
            .Options);

    private sealed class NeverCalledProcessorClient : IProcessorClient
    {
        public bool Called { get; private set; }

        public Task<ProcessResult> AnalyzeAsync(
            Music music,
            string? audioPath,
            CancellationToken cancellationToken)
        {
            Called = true;
            throw new InvalidOperationException("O processador não deveria ser chamado.");
        }
    }

    private sealed class StubProcessorClient : IProcessorClient
    {
        private readonly ProcessResult? _result;
        private readonly Exception? _exception;
        public bool Called { get; private set; }

        public StubProcessorClient(ProcessResult result) => _result = result;
        public StubProcessorClient(Exception exception) => _exception = exception;

        public Task<ProcessResult> AnalyzeAsync(
            Music music,
            string? audioPath,
            CancellationToken cancellationToken)
        {
            Called = true;
            if (_exception is not null) throw _exception;
            return Task.FromResult(_result!);
        }
    }
}
