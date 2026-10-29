namespace DampCode_API.Data;

// Código das versões de uma entrega. A lista de versões fica no PostgreSQL (SubmissionVersion);
// aqui fica só o texto, identificado pelo _id do documento.
public interface ISubmissionVersionStore
{
    Task<string> InsertAsync(Guid submissionId, string code, CancellationToken cancellationToken);
    Task<string?> GetCodeAsync(string documentId, CancellationToken cancellationToken);
    Task DeleteAsync(string documentId, CancellationToken cancellationToken);
}
