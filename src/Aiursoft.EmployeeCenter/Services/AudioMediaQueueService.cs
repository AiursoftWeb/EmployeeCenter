using Aiursoft.Canon.TaskQueue;
using Aiursoft.Scanner.Abstractions;

namespace Aiursoft.EmployeeCenter.Services;

public class AudioMediaQueueService(ServiceTaskQueue taskQueue) : ISingletonDependency
{
    private const string QueueName = "audio-media";
    private readonly Lock _queueLock = new();

    public bool QueueIfNotActive(int audioId)
    {
        var taskName = $"Process uploaded media for audio {audioId}";
        lock (_queueLock)
        {
            var active = taskQueue.GetPendingTasks()
                .Concat(taskQueue.GetProcessingTasks())
                .Any(task => task.QueueName == QueueName && task.TaskName == taskName);
            if (active)
            {
                return false;
            }

            taskQueue.QueueWithDependency<AudioMediaService>(
                queueName: QueueName,
                taskName: taskName,
                task: service => service.ProcessAsync(audioId));
            return true;
        }
    }
}
