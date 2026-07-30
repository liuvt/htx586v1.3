using HTX586CONTRACT.Domain.Enums;

namespace HTX586CONTRACT.Application.Abstractions;

public interface IContractDocumentService
{
    Task<string> SaveSignatureAsync(
        Guid contractId,
        string currentUserId,
        SignatureParty party,
        string dataUrl,
        CancellationToken cancellationToken = default);

    Task<string> CompleteAsync(
        Guid contractId,
        string currentUserId,
        CancellationToken cancellationToken = default);
}
