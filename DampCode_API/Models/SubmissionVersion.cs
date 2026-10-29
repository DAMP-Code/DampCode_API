namespace DampCode_API.Models;

// Uma versão enviada de uma entrega. A linha existir é o que faz a versão existir;
// o código fica no MongoDB, no documento MongoDocumentId.
public sealed class SubmissionVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SubmissionId { get; set; }
    public int Version { get; set; }
    public required string Language { get; set; }
    public string? FileName { get; set; }
    public required string MongoDocumentId { get; set; }
    public Guid SubmittedByAccountId { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public Submission Submission { get; set; } = null!;
    public Account SubmittedByAccount { get; set; } = null!;
}
