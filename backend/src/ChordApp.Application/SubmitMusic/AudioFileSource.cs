namespace ChordApp.Application.SubmitMusic
{
    public sealed record AudioFileSource(
        Stream Content,
        string FileName,
        long Length,
        string Model = "lv-chordia");
}
