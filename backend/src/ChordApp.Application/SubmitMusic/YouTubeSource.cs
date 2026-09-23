namespace ChordApp.Application.SubmitMusic
{
    /// <summary>Dados da URL e do modelo escolhido para uma análise do YouTube.</summary>
    public sealed record YouTubeSource(string Url, string Model = "lv-chordia");

}
