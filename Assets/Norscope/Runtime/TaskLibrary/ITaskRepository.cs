using System.Collections.Generic;

namespace Norscope.TaskLibrary
{
    public interface ITaskRepository
    {
        IReadOnlyList<TaskData> GetAll();
    }
}
