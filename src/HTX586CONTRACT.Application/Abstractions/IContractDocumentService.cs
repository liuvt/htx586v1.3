using HTX586CONTRACT.Application.Contracts;

namespace HTX586CONTRACT.Application.Abstractions;
public interface IContractDocumentService
{
    Task<string> GeneratePdfAsync(Guid contractId, CancellationToken cancellationToken = default);
    Task<string> GenerateSyncedPdfAsync(Guid contractId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContractPdfVersionDto>> GetSyncedPdfsAsync(Guid contractId, CancellationToken cancellationToken = default);
    Task<string> SaveSignatureAsync(
        Guid contractId,
        string currentUserId,
        string party,
        string signerName,
        string dataUrl,
        CancellationToken cancellationToken = default);
}
