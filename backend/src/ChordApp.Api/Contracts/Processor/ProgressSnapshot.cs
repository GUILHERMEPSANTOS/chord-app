namespace ChordApp.Api.Contracts.Processor; 
/// <summary>Resposta recebida da rota de progresso do processador.</summary>
public sealed record ProgressSnapshot(int Percent, string Stage);
