using System.Collections.Concurrent;

namespace HTX586CONTRACT.Application.Common;

/// <summary>
/// Khóa đồng bộ theo từng hợp đồng dùng chung giữa luồng cập nhật/hoàn thành (ContractService)
/// và luồng ký số (ContractDocumentService), tự động giải phóng bộ nhớ khi không còn luồng chờ.
/// </summary>
public static class ContractWriteGate
{
    private sealed class GateEntry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public int RefCount;
    }

    private static readonly ConcurrentDictionary<Guid, GateEntry> Gates = new();
    private static readonly object SyncRoot = new();

    public static async Task<IDisposable> AcquireAsync(Guid contractId, CancellationToken ct = default)
    {
        GateEntry entry;
        lock (SyncRoot)
        {
            entry = Gates.GetOrAdd(contractId, static _ => new GateEntry());
            entry.RefCount++;
        }

        try
        {
            await entry.Semaphore.WaitAsync(ct);
            return new Releaser(contractId, entry);
        }
        catch
        {
            ReleaseEntry(contractId, entry);
            throw;
        }
    }

    private static void ReleaseEntry(Guid contractId, GateEntry entry)
    {
        lock (SyncRoot)
        {
            entry.RefCount--;
            if (entry.RefCount <= 0)
            {
                Gates.TryRemove(contractId, out _);
            }
        }
    }

    private sealed class Releaser(Guid contractId, GateEntry entry) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            try
            {
                entry.Semaphore.Release();
            }
            finally
            {
                ReleaseEntry(contractId, entry);
            }
        }
    }
}
