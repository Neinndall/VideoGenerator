using System.Reflection;

namespace VideoGenerator.Utils
{
    public static class AssemblyVersion
    {
        public static string Version { get; }

        static AssemblyVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            Version = $"v{version?.ToString(4) ?? "1.0.0.0"}";
        }
    }
}