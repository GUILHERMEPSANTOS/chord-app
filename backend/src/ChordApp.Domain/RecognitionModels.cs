namespace ChordApp.Domain;

/// <summary>Identificadores e validação dos detectores disponíveis.</summary>
public static class RecognitionModels
{
    public const string LvChordia = "lv-chordia";
    public const string BtcIsmir19 = "btc-ismir19";

    public static bool IsAllowed(string? model) => model is LvChordia or BtcIsmir19;
}
