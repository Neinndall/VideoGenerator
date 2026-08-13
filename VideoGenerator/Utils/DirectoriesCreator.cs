using System;
using System.IO;

namespace VideoGenerator.Utils
{
    /// <summary>
    /// Centralizes safe, on-demand directory creation for the application.
    /// It intentionally does not prepare the full folder tree during startup.
    /// </summary>
    public static class DirectoriesCreator
    {
        public static void CreateDirectory(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path))
                    Directory.CreateDirectory(path);
            }
            catch
            {
                // The owning operation reports its own actionable failure.
            }
        }

        public static void CreateParentDirectory(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return;
            CreateDirectory(Path.GetDirectoryName(filePath));
        }
    }
}
