using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models;

namespace SafeLearnKids.Web.Services;

// US-18: a sync reads a whole channel or playlist from YouTube and can take many seconds, so it runs in the background.
// The admin's click only marks the source (ContentSources.SyncRequestedAt) and queues it; this worker syncs one source
// at a time, writes the outcome (LastSyncedAt, LastSyncError, LastSyncResult) and clears the mark. Marks left by a
// restart are picked up again at startup, so no request is lost.
public sealed class SourceSyncQueue(IServiceScopeFactory scopes, ILogger<SourceSyncQueue> logger) : BackgroundService
{
    // A mark older than this is treated as stuck (the app stopped mid-sync) and may be queued again.
    public static readonly TimeSpan Stale = TimeSpan.FromMinutes(10);

    private readonly Channel<int> queue = Channel.CreateUnbounded<int>(new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(int sourceId) => queue.Writer.TryWrite(sourceId);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            if (scope.ServiceProvider.GetService<SafeLearnDbContext>() is { } db)
                foreach (var id in await db.ContentSources.AsNoTracking().Where(source => source.SyncRequestedAt != null)
                             .OrderBy(source => source.SyncRequestedAt).Select(source => source.Id).ToListAsync(stoppingToken))
                    Enqueue(id);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogWarning("Could not reload pending source syncs: {Type}", error.GetType().Name);
        }

        await foreach (var id in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await RunAsync(id, stoppingToken);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                // Never let one source stop the worker; the mark is cleared with a readable error.
                logger.LogWarning("Background sync of source {SourceId} failed: {Type}", id, error.GetType().Name);
                await FailAsync(id, stoppingToken);
            }
        }
    }

    private async Task RunAsync(int id, CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        if (scope.ServiceProvider.GetService<SafeLearnDbContext>() is not { } db) return;
        var source = await db.ContentSources.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        // Already done (queued twice) or deleted meanwhile.
        if (source?.SyncRequestedAt is null) return;
        SyncResult result;
        if (source.Platform == "Website" || source.Status is SourceStatus.Rejected or SourceStatus.Suspended)
        {
            // Same rules as the button: websites are recorded only, rejected or suspended sources are not fetched.
            result = new SyncResult(0, 0, 0, source.Platform == "Website"
                ? "Nguồn website chỉ được ghi nhận, không tải video."
                : "Chỉ đồng bộ nguồn đang chờ duyệt hoặc đã duyệt.");
            source.LastSyncedAt = DateTime.UtcNow;
            source.LastSyncError = result.Error;
        }
        else
        {
            result = await scope.ServiceProvider.GetRequiredService<SourceSyncService>().SyncAsync(db, source, cancellationToken);
        }
        source.SyncRequestedAt = null;
        source.LastSyncResult = result.Error is null ? Describe(result) : null;
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Synced source {SourceId}: {Found} found, {Added} new, error: {Failed}", id, result.Found, result.Added, result.Error is not null);
    }

    private async Task FailAsync(int id, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            if (scope.ServiceProvider.GetService<SafeLearnDbContext>() is not { } db) return;
            await db.ContentSources.Where(item => item.Id == id).ExecuteUpdateAsync(set => set
                .SetProperty(item => item.SyncRequestedAt, (DateTime?)null)
                .SetProperty(item => item.LastSyncedAt, DateTime.UtcNow)
                .SetProperty(item => item.LastSyncResult, (string?)null)
                .SetProperty(item => item.LastSyncError, "Đồng bộ gặp lỗi không mong muốn. Hãy thử lại sau."), cancellationToken);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogWarning("Could not record the failed sync of source {SourceId}: {Type}", id, error.GetType().Name);
        }
    }

    public static string Describe(SyncResult result) =>
        $"{result.Found} video, {result.Added} video mới chờ duyệt" + (result.Blocked > 0 ? $", {result.Blocked} video tự chặn do từ khóa" : "");
}
