using HTX586CONTRACT.Application.Admins.CompanyProfiles;
using Microsoft.AspNetCore.Components.Forms;
using HTX586CONTRACT.Application.Common;

namespace HTX586CONTRACT.Application.Abstractions;

public interface ICompanyProfileService
{
    Task<IReadOnlyList<CompanyProfileListItemDto>> GetListAsync(CompanyProfileFilter filter, CancellationToken cancellationToken = default);
    Task<CompanyProfileDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CompanyProfileOptionDto>> GetActiveOptionsAsync(CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateCompanyProfileRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateCompanyProfileRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, string deletedByUserId, CancellationToken cancellationToken = default);
}
