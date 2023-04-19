using Engine.Models;

namespace Engine
{
    public class FileMigrationManager
    {
        public FileMigrationManager()
        {
        }

        public async Task StartCopy(StartCopyInfo startCopyInfo)
        {
            if (startCopyInfo is null)
            {
                throw new ArgumentNullException(nameof(startCopyInfo));
            }


        }
    }
}