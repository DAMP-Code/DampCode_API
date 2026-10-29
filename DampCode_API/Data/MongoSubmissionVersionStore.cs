using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace DampCode_API.Data;

// Guarda o código de cada versão na coleção "SubmissionVersions" do MongoDB.
public sealed class MongoSubmissionVersionStore(MongoDbService mongoDbService) : ISubmissionVersionStore
{
    private readonly IMongoCollection<SubmissionCodeDocument> _versions =
        mongoDbService.Database!.GetCollection<SubmissionCodeDocument>("SubmissionVersions");

    public async Task<string> InsertAsync(Guid submissionId, string code, CancellationToken cancellationToken)
    {
        var document = new SubmissionCodeDocument { SubmissionId = submissionId.ToString(), Code = code, CreatedAt = DateTime.UtcNow };
        await _versions.InsertOneAsync(document, cancellationToken: cancellationToken);
        return document.Id!;
    }

    public async Task<string?> GetCodeAsync(string documentId, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(documentId, out _)) return null;
        var document = await _versions.Find(item => item.Id == documentId).FirstOrDefaultAsync(cancellationToken);
        return document?.Code;
    }

    public Task DeleteAsync(string documentId, CancellationToken cancellationToken) =>
        ObjectId.TryParse(documentId, out _)
            ? _versions.DeleteOneAsync(item => item.Id == documentId, cancellationToken)
            : Task.CompletedTask;
}

public sealed class SubmissionCodeDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("submissionId")]
    public required string SubmissionId { get; set; }

    [BsonElement("code")]
    public required string Code { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
}
