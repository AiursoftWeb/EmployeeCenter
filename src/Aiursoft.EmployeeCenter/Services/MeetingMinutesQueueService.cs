using Aiursoft.Canon.TaskQueue;
using Aiursoft.EmployeeCenter.Configuration;
using Aiursoft.Scanner.Abstractions;
using Microsoft.Extensions.Options;

namespace Aiursoft.EmployeeCenter.Services;

public class MeetingMinutesQueueService(ServiceTaskQueue taskQueue, IOptions<AppSettings> appSettings) : ISingletonDependency
{
    private const string QueueName = "meeting-minutes";
    private readonly Lock _queueLock = new();

    public int MaxRetryCount { get; } = appSettings.Value.Agent.MeetingMinutesMaxRetryCount;

    public bool QueueIfNotActive(int audioId, int transcriptRevision)
    {
        var taskName = $"Regenerate meeting minutes for audio {audioId} revision {transcriptRevision}";
        lock (_queueLock)
        {
            var active = taskQueue.GetPendingTasks()
                .Concat(taskQueue.GetProcessingTasks())
                .Any(task => task.QueueName == QueueName && task.TaskName == taskName);
            if (active) return false;

            taskQueue.QueueWithDependency<MeetingMinutesService>(
                queueName: QueueName,
                taskName: taskName,
                task: service => service.RegenerateAsync(audioId, transcriptRevision));
            return true;
        }
    }
}
