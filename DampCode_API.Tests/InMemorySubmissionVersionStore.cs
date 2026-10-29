using System.Collections.Concurrent;
using DampCode_API.Data;
using MongoDB.Bson;

namespace DampCode_API.Tests;

// Substitui o MongoDB nos testes: mesmo contrato, guardado em memória.
internal sealed class InMemorySubmissionVersionStore : ISubmissionVersionStore
{
    public ConcurrentDictionary<string, (Guid SubmissionId, string Code)> Documents { get; } = new();

    public Task<string> InsertAsync(Guid submissionId, string code, CancellationToken cancellationToken)
    {
        var id = ObjectId.GenerateNewId().ToString();
        Documents[id] = (submissionId, code);
        return Task.FromResult(id);
    }

    public Task<string?> GetCodeAsync(string documentId, CancellationToken cancellationToken) =>
        Task.FromResult(Documents.TryGetValue(documentId, out var document) ? document.Code : null);

    public Task DeleteAsync(string documentId, CancellationToken cancellationToken)
    {
        Documents.TryRemove(documentId, out _);
        return Task.CompletedTask;
    }
}
