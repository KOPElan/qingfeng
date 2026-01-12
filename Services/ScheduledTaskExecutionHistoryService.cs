using QingFeng.Models;
using QingFeng.Data;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace QingFeng.Services;

public class ScheduledTaskExecutionHistoryService : IScheduledTaskExecutionHistoryService
{
    private readonly ILogger<ScheduledTaskExecutionHistoryService> _logger;
    private readonly IDbContextFactory<QingFengDbContext> _dbContextFactory;

    public ScheduledTaskExecutionHistoryService(
        ILogger<ScheduledTaskExecutionHistoryService> logger,
        IDbContextFactory<QingFengDbContext> dbContextFactory)
    {
        _logger = logger;
        _dbContextFactory = dbContextFactory;
    }

    public async Task<List<ScheduledTaskExecutionHistory>> GetHistoryByTaskIdAsync(int taskId, int limit = 50)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync();
        return await context.ScheduledTaskExecutionHistories
            .Where(h => h.ScheduledTaskId == taskId)
            .OrderByDescending(h => h.StartTime)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<ScheduledTaskExecutionHistory>> GetRecentHistoryAsync(int limit = 100)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync();
        return await context.ScheduledTaskExecutionHistories
            .OrderByDescending(h => h.StartTime)
            .Take(limit)
            .Include(h => h.ScheduledTask)
            .ToListAsync();
    }

    public async Task<ScheduledTaskExecutionHistory> CreateHistoryAsync(ScheduledTaskExecutionHistory history)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync();
        context.ScheduledTaskExecutionHistories.Add(history);
        await context.SaveChangesAsync();
        return history;
    }

    public async Task UpdateHistoryAsync(ScheduledTaskExecutionHistory history)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync();
        context.ScheduledTaskExecutionHistories.Update(history);
        await context.SaveChangesAsync();
    }

    public async Task<ScheduledTaskExecutionHistory?> GetHistoryAsync(int id)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync();
        return await context.ScheduledTaskExecutionHistories.FindAsync(id);
    }
}
