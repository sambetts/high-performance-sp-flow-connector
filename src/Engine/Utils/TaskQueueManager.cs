using System.Collections.Concurrent;

namespace Engine.Utils;

public class TaskQueueManager
{
    private ConcurrentDictionary<Guid, Task> _cd = new();

    public Guid AddNew(Task task)
    { 
        var guid = Guid.NewGuid();
        if (!_cd.TryAdd(guid, task))
        {
            throw new InvalidOperationException("Couldn't add new task");
        }

        return guid;
    }

    public void Remove(Guid guid)
    {
        if (!_cd.TryRemove(guid, out var task))
        {
            throw new InvalidOperationException("Couldn't remove task");
        }
    }

    public Task GetTask(Guid guid)
    {
        if (!_cd.TryGetValue(guid, out var task))
        {
            throw new InvalidOperationException("Couldn't find task");
        }
        return task;
    }
}
