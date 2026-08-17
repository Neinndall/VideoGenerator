using System.Reflection;

namespace VideoGenerator.Utils
{
    public static class AssemblyVersion
    {
        private const string FallbackVersion = "1.3.0.1";
        public static string Version { get; }

        static AssemblyVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            Version = $"v{version?.ToString(4) ?? FallbackVersion}";
        }
    }
}
