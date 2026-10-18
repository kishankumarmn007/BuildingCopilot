namespace BuildingCopilot.Application.Models;

public sealed record RagResponse(
    string Answer,
    IReadOnlyList<RagSource> Sources);

public sealed record RagSource(
    string CitationId,
    string Source,
    string Text,
    double Similarity);