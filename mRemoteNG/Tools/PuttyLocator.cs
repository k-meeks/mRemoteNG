using System;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace mRemoteNG.Tools
{
    /// <summary>
    /// Locates an installed PuTTY (putty.exe) on the system. mRemoteNG no longer
    /// bundles a PuTTY binary, so the user is expected to install the official
    /// build from Simon Tatham. This searches the common install locations and
    /// the registry App Paths / PATH so an existing install is picked up
    /// automatically; if none is found the user is prompted to point at one.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static class PuttyLocator
    {
        public const string PuttyExeName = "putty.exe";

        /// <summary>
        /// Returns the full path to an installed putty.exe, or an empty string
        /// if none could be found. Order: registry App Paths, the standard
        /// Program Files\PuTTY folders, then directories on PATH.
        /// </summary>
        public static string Detect()
        {
            string fromRegistry = DetectFromAppPaths();
            if (!string.IsNullOrEmpty(fromRegistry))
                return fromRegistry;

            foreach (string candidate in StandardInstallCandidates())
            {
                if (SafeFileExists(candidate))
                    return candidate;
            }

            string fromPath = DetectFromEnvironmentPath();
            if (!string.IsNullOrEmpty(fromPath))
                return fromPath;

            return string.Empty;
        }

        /// <summary>
        /// Given a directory the user selected, resolve the putty.exe inside it.
        /// Returns the full path if found, otherwise an empty string.
        /// </summary>
        public static string ResolveFromDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
                return string.Empty;

            try
            {
                // The user may have selected the folder itself or, by habit, the
                // exe; handle both.
                if (File.Exists(directory) &&
                    string.Equals(Path.GetFileName(directory), PuttyExeName, StringComparison.OrdinalIgnoreCase))
                    return directory;

                string candidate = Path.Combine(directory, PuttyExeName);
                if (SafeFileExists(candidate))
                    return candidate;
            }
            catch
            {
                // ignored - treat as not found
            }

            return string.Empty;
        }

        private static string DetectFromAppPaths()
        {
            // HKLM first (system-wide install), then HKCU (per-user install).
            foreach (RegistryKey root in new[] { Registry.LocalMachine, Registry.CurrentUser })
            {
                try
                {
                    using RegistryKey key = root.OpenSubKey(
                        @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + PuttyExeName);
                    if (key?.GetValue(null) is string path)
                    {
                        path = path.Trim('"');
                        if (SafeFileExists(path))
                            return path;
                    }
                }
                catch
                {
                    // ignored - registry not readable, try next source
                }
            }

            return string.Empty;
        }

        private static System.Collections.Generic.IEnumerable<string> StandardInstallCandidates()
        {
            foreach (Environment.SpecialFolder folder in new[]
                     {
                         Environment.SpecialFolder.ProgramFiles,
                         Environment.SpecialFolder.ProgramFilesX86
                     })
            {
                string root = Environment.GetFolderPath(folder);
                if (!string.IsNullOrEmpty(root))
                    yield return Path.Combine(root, "PuTTY", PuttyExeName);
            }
        }

        private static string DetectFromEnvironmentPath()
        {
            string pathVar = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (string dir in pathVar.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(dir))
                    continue;

                try
                {
                    string candidate = Path.Combine(dir.Trim(), PuttyExeName);
                    if (SafeFileExists(candidate))
                        return candidate;
                }
                catch
                {
                    // ignored - malformed PATH entry
                }
            }

            return string.Empty;
        }

        private static bool SafeFileExists(string path)
        {
            try
            {
                return !string.IsNullOrEmpty(path) && File.Exists(path);
            }
            catch
            {
                return false;
            }
        }
    }
}
