namespace ChordApp.Domain;

/// <summary>Estados possíveis de uma análise, do recebimento à conclusão ou falha.</summary>
public enum AnalysisStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}
