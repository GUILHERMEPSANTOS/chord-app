using ChordApp.Domain;
using Microsoft.EntityFrameworkCore;

namespace ChordApp.Infrastructure
{
    public sealed class MusicDb(DbContextOptions<MusicDb> options) : DbContext(options)
    {
        public DbSet<Music> Musics => Set<Music>();
        public DbSet<ChordSegment> Chords => Set<ChordSegment>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder
                .Entity<Music>()
                .HasMany(music => music.Chords)
                .WithOne()
                .HasForeignKey(chords => chords.MusicId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder
                .Entity<Music>()
                .Property(music => music.Status)
                .HasConversion<string>();

            modelBuilder.Entity<ChordSegment>().HasIndex(chord => new { chord.MusicId, chord.Model, chord.StartTime });
        }
    }
}
