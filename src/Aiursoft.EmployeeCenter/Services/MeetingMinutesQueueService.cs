using Aiursoft.Canon.TaskQueue;
using Aiursoft.EmployeeCenter.Configuration;
using Aiursoft.Scanner.Abstractions;
using Microsoft.Extensions.Options;

namespace Aiursoft.EmployeeCenter.Services;

public class MeetingMinutesQueueService(ServiceTaskQueue taskQueue, IOptions<AppSettings> appSettings) : ISingletonDependency
{
    private const string QueueName = "meeting-minutes";
    private readonly Lock _queueLock = new();
    private readonly HashSet<string> _activeTasks = [];

    public int MaxRetryCount { get; } = appSettings.Value.Agent.MeetingMinutesMaxRetryCount;

    public bool QueueIfNotActive(int audioId, int transcriptRevision)
    {
        var taskName = GetTaskName(audioId, transcriptRevision);
        lock (_queueLock)
        {
            if (_activeTasks.Contains(taskName) || IsQueued(taskName)) return false;

            taskQueue.QueueWithDependency<MeetingMinutesService>(
                queueName: QueueName,
                taskName: taskName,
                task: service => RunQueuedAsync(
                    taskName,
                    () => service.RegenerateAsync(audioId, transcriptRevision)));
            return true;
        }
    }

    public async Task<bool> ExecuteIfNotActiveAsync(
        int audioId,
        int transcriptRevision,
        Func<Task> task)
    {
        var taskName = GetTaskName(audioId, transcriptRevision);
        lock (_queueLock)
        {
            if (_activeTasks.Contains(taskName) || IsQueued(taskName)) return false;
            _activeTasks.Add(taskName);
        }

        try
        {
            await task();
            return true;
        }
        finally
        {
            lock (_queueLock)
            {
                _activeTasks.Remove(taskName);
            }
        }
    }

    private async Task RunQueuedAsync(string taskName, Func<Task> task)
    {
        lock (_queueLock)
        {
            if (!_activeTasks.Add(taskName)) return;
        }

        try
        {
            await task();
        }
        finally
        {
            lock (_queueLock)
            {
                _activeTasks.Remove(taskName);
            }
        }
    }

    private bool IsQueued(string taskName)
    {
        return taskQueue.GetPendingTasks()
            .Concat(taskQueue.GetProcessingTasks())
            .Any(task => task.QueueName == QueueName && task.TaskName == taskName);
    }

    private static string GetTaskName(int audioId, int transcriptRevision)
    {
        return $"Generate meeting minutes for audio {audioId} revision {transcriptRevision}";
    }
}
