using Aiursoft.Canon.TaskQueue;
using Aiursoft.Scanner.Abstractions;

namespace Aiursoft.EmployeeCenter.Services;

public class MeetingMinutesQueueService(ServiceTaskQueue taskQueue) : ISingletonDependency
{
    private const string QueueName = "meeting-minutes";
    private readonly Lock _queueLock = new();

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
