using Aiursoft.Canon.TaskQueue;
using Aiursoft.Scanner.Abstractions;

namespace Aiursoft.EmployeeCenter.Services;

public class MeetingMinutesQueueService(ServiceTaskQueue taskQueue) : ISingletonDependency
{
    private const string QueueName = "meeting-minutes";
    private readonly Lock _queueLock = new();
    private readonly HashSet<MeetingMinutesTaskKey> _activeTasks = [];
    private readonly HashSet<MeetingMinutesTaskKey> _deferredRetries = [];

    public bool QueueRetry(int audioId, int transcriptRevision, DateTime transcriptCreateTime)
    {
        var taskKey = new MeetingMinutesTaskKey(audioId, transcriptRevision, transcriptCreateTime);
        lock (_queueLock)
        {
            if (_activeTasks.Contains(taskKey)) return _deferredRetries.Add(taskKey);
            if (IsQueued(taskKey)) return false;

            QueueRetry(taskKey);
            return true;
        }
    }

    public async Task<bool> ExecuteIfNotActiveAsync(
        int audioId,
        int transcriptRevision,
        DateTime transcriptCreateTime,
        Func<Task> task)
    {
        var taskKey = new MeetingMinutesTaskKey(audioId, transcriptRevision, transcriptCreateTime);
        lock (_queueLock)
        {
            if (_activeTasks.Contains(taskKey) || IsQueued(taskKey)) return false;
            _activeTasks.Add(taskKey);
        }

        try
        {
            await task();
            return true;
        }
        finally
        {
            CompleteTask(taskKey);
        }
    }

    private async Task RunQueuedAsync(MeetingMinutesTaskKey taskKey, Func<Task> task)
    {
        lock (_queueLock)
        {
            if (!_activeTasks.Add(taskKey))
            {
                _deferredRetries.Add(taskKey);
                return;
            }
        }

        try
        {
            await task();
        }
        finally
        {
            CompleteTask(taskKey);
        }
    }

    private void CompleteTask(MeetingMinutesTaskKey taskKey)
    {
        lock (_queueLock)
        {
            _activeTasks.Remove(taskKey);
            if (_deferredRetries.Remove(taskKey)) QueueRetry(taskKey);
        }
    }

    private void QueueRetry(MeetingMinutesTaskKey taskKey)
    {
        taskQueue.QueueWithDependency<MeetingMinutesService>(
            queueName: QueueName,
            taskName: GetTaskName(taskKey),
            task: service => RunQueuedAsync(
                taskKey,
                () => service.RegenerateAsync(
                    taskKey.AudioId,
                    taskKey.TranscriptRevision,
                    taskKey.TranscriptCreateTime)));
    }

    private bool IsQueued(MeetingMinutesTaskKey taskKey)
    {
        var taskName = GetTaskName(taskKey);
        return taskQueue.GetPendingTasks()
            .Concat(taskQueue.GetProcessingTasks())
            .Any(task => task.QueueName == QueueName && task.TaskName == taskName);
    }

    private static string GetTaskName(MeetingMinutesTaskKey taskKey)
    {
        return $"Generate meeting minutes for audio {taskKey.AudioId} revision {taskKey.TranscriptRevision} " +
               $"instance {taskKey.TranscriptCreateTime.Ticks}";
    }

    private readonly record struct MeetingMinutesTaskKey(
        int AudioId,
        int TranscriptRevision,
        DateTime TranscriptCreateTime);
}
