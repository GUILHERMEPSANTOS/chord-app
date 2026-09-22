namespace ChordApp.Api;

public static class TempFiles
{
    public static string Root => Environment.GetEnvironmentVariable("AUDIO_TEMP_DIR") ?? Path.Combine(Path.GetTempPath(), "chord-app");
    public static string PathFor(Guid id, string ext) => Path.Combine(Root, id + ext);
    public static string? Find(Guid id) => new[] { PathFor(id, ".mp3"), PathFor(id, ".wav") }.FirstOrDefault(File.Exists);
    public static void Delete(Guid id) { foreach (var ext in new[] { ".mp3", ".wav" }) { var path = PathFor(id, ext); if (File.Exists(path)) File.Delete(path); } }
}
