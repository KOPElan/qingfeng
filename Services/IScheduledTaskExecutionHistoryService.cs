using QingFeng.Models;

namespace QingFeng.Services;

public interface IScheduledTaskExecutionHistoryService
{
    Task<List<ScheduledTaskExecutionHistory>> GetHistoryByTaskIdAsync(int taskId, int limit = 50);
    Task<List<ScheduledTaskExecutionHistory>> GetRecentHistoryAsync(int limit = 100);
    Task<ScheduledTaskExecutionHistory> CreateHistoryAsync(ScheduledTaskExecutionHistory history);
    Task UpdateHistoryAsync(ScheduledTaskExecutionHistory history);
    Task<ScheduledTaskExecutionHistory?> GetHistoryAsync(int id);
}
