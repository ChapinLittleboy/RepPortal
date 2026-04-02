using System.Linq.Expressions;
using Hangfire;

namespace RepPortal.Services;

public interface IRecurringJobScheduler
{
    void AddOrUpdate<T>(
        string recurringJobId,
        string queue,
        Expression<Func<T, Task>> methodCall,
        string cronExpression,
        RecurringJobOptions options);

    void RemoveIfExists(string recurringJobId);
}

public sealed class HangfireRecurringJobScheduler : IRecurringJobScheduler
{
    public void AddOrUpdate<T>(
        string recurringJobId,
        string queue,
        Expression<Func<T, Task>> methodCall,
        string cronExpression,
        RecurringJobOptions options)
    {
        RecurringJob.AddOrUpdate(
            recurringJobId: recurringJobId,
            queue: queue,
            methodCall: methodCall,
            cronExpression: cronExpression,
            options: options);
    }

    public void RemoveIfExists(string recurringJobId)
        => RecurringJob.RemoveIfExists(recurringJobId);
}
