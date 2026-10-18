namespace BuildingCopilot.Application.Models;

public sealed record KnowledgeDocument(
    Guid Id,
    string Source,
    string Text);

public sealed record KnowledgePoint(
    KnowledgeDocument Document,
    float[] Vector);

public sealed record KnowledgeMatch(
    KnowledgeDocument Document,
    double Score);