using System;
using System.Reflection;

namespace VideoGenerator.Utils
{
    public static class AssemblyVersion
    {
        public static string Version { get; }

        static AssemblyVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version
                ?? throw new InvalidOperationException("The application assembly has no version.");
            Version = FormatDisplayVersion(version);
        }

        public static string FormatDisplayVersion(System.Version version)
        {
            ArgumentNullException.ThrowIfNull(version);
            // The fourth component is a revision: hide it only when it is zero.
            // A non-zero revision remains visible (e.g. 2.0.0.1).
            string formatted = version.Revision > 0
                ? version.ToString(4)
                : version.ToString(version.Build >= 0 ? 3 : 2);
            return $"v{formatted}";
        }
    }
}
