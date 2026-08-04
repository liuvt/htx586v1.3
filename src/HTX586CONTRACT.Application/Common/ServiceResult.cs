namespace HTX586CONTRACT.Application.Common;

public sealed class ServiceResult
{
    public bool Succeeded { get; init; }

    public string? Message { get; init; }

    public IReadOnlyList<string> Errors { get; init; }
        = Array.Empty<string>();

    public static ServiceResult Success(
        string? message = null)
    {
        return new ServiceResult
        {
            Succeeded = true,
            Message = message
        };
    }

    public static ServiceResult Failure(
        string error)
    {
        return new ServiceResult
        {
            Succeeded = false,
            Message = error,
            Errors = [error]
        };
    }

    public static ServiceResult Failure(
        IEnumerable<string> errors)
    {
        var values = errors.ToArray();
        return new ServiceResult
        {
            Succeeded = false,
            Message = values.FirstOrDefault(),
            Errors = values
        };
    }
}