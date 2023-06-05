// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.Win32;
using osu.Desktop.Security;
using osu.Framework.Platform;
using osu.Game;
using osu.Desktop.Updater;
using osu.Framework;
using osu.Framework.Logging;
using osu.Game.Updater;
using osu.Desktop.Windows;
using osu.Framework.Threading;
using osu.Game.IO;
using osu.Game.IPC;
using osu.Game.Utils;
using SDL2;

namespace osu.Desktop
{
    internal partial class OsuGameDesktop : OsuGame
    {
        private OsuSchemeLinkIPCChannel? osuSchemeLinkIPCChannel;
        private ArchiveImportIPCChannel? archiveImportIPCChannel;

        public OsuGameDesktop(string[]? args = null)
            : base(args)
        {
        }

        public override StableStorage? GetStorageForStableInstall()
        {
            try
            {
                if (Host is DesktopGameHost desktopHost)
                {
                    string? stablePath = getStableInstallPath();
                    if (!string.IsNullOrEmpty(stablePath))
                        return new StableStorage(stablePath, desktopHost);
                }
            }
            catch (Exception)
            {
                Logger.Log("Could not find a stable install", LoggingTarget.Runtime, LogLevel.Important);
            }

            return null;
        }

        private string? getStableInstallPath()
        {
            static bool checkExists(string p) => Directory.Exists(Path.Combine(p, "Songs")) || File.Exists(Path.Combine(p, "osu!.cfg"));

            string? stableInstallPath;

            if (OperatingSystem.IsWindows())
            {
                try
                {
                    stableInstallPath = getStableInstallPathFromRegistry();

                    if (!string.IsNullOrEmpty(stableInstallPath) && checkExists(stableInstallPath))
                        return stableInstallPath;
                }
                catch
                {
                }
            }

            stableInstallPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"osu!");
            if (checkExists(stableInstallPath))
                return stableInstallPath;

            stableInstallPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".osu");
            if (checkExists(stableInstallPath))
                return stableInstallPath;

            return null;
        }

        [SupportedOSPlatform("windows")]
        private string? getStableInstallPathFromRegistry()
        {
            using (RegistryKey? key = Registry.ClassesRoot.OpenSubKey("osu"))
                return key?.OpenSubKey(@"shell\open\command")?.GetValue(string.Empty)?.ToString()?.Split('"')[1].Replace("osu!.exe", "");
        }

        protected override UpdateManager CreateUpdateManager()
        {
            string? packageManaged = Environment.GetEnvironmentVariable("OSU_EXTERNAL_UPDATE_PROVIDER");

            if (!string.IsNullOrEmpty(packageManaged))
                return new NoActionUpdateManager();

            switch (RuntimeInfo.OS)
            {
                case RuntimeInfo.Platform.Windows:
                    Debug.Assert(OperatingSystem.IsWindows());

                    return new SquirrelUpdateManager();

                default:
                    return new SimpleUpdateManager();
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            LoadComponentAsync(new DiscordRichPresence(), Add);

            if (RuntimeInfo.OS == RuntimeInfo.Platform.Windows)
                LoadComponentAsync(new GameplayWinKeyBlocker(), Add);

            LoadComponentAsync(new ElevatedPrivilegesChecker(), Add);

            osuSchemeLinkIPCChannel = new OsuSchemeLinkIPCChannel(Host, this);
            archiveImportIPCChannel = new ArchiveImportIPCChannel(Host, this);
        }

        public override void SetHost(GameHost host)
        {
            base.SetHost(host);

            var desktopWindow = (SDL2DesktopWindow)host.Window;

            var iconStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(GetType(), "lazer.ico");
            if (iconStream != null)
                desktopWindow.SetIconFromStream(iconStream);

            desktopWindow.CursorState |= CursorState.Hidden;
            desktopWindow.Title = Name;
            desktopWindow.DragDrop += f =>
            {
                // on macOS, URL associations are handled via SDL_DROPFILE events.
                if (f.StartsWith(OSU_PROTOCOL, StringComparison.Ordinal))
                {
                    HandleLink(f);
                    return;
                }

                fileDrop(new[] { f });
            };
        }

        protected override BatteryInfo CreateBatteryInfo() => new SDL2BatteryInfo();

        private readonly List<string> importableFiles = new List<string>();
        private ScheduledDelegate? importSchedule;

        private void fileDrop(string[] filePaths)
        {
            lock (importableFiles)
            {
                importableFiles.AddRange(filePaths);

                Logger.Log($"Adding {filePaths.Length} files for import");

                // File drag drop operations can potentially trigger hundreds or thousands of these calls on some platforms.
                // In order to avoid spawning multiple import tasks for a single drop operation, debounce a touch.
                importSchedule?.Cancel();
                importSchedule = Scheduler.AddDelayed(handlePendingImports, 100);
            }
        }

        private void handlePendingImports()
        {
            lock (importableFiles)
            {
                Logger.Log($"Handling batch import of {importableFiles.Count} files");

                string[] paths = importableFiles.ToArray();
                importableFiles.Clear();

                Task.Factory.StartNew(() => Import(paths), TaskCreationOptions.LongRunning);
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            osuSchemeLinkIPCChannel?.Dispose();
            archiveImportIPCChannel?.Dispose();
        }

        private class SDL2BatteryInfo : BatteryInfo
        {
            public override double? ChargeLevel
            {
                get
                {
                    SDL.SDL_GetPowerInfo(out _, out int percentage);

                    if (percentage == -1)
                        return null;

                    return percentage / 100.0;
                }
            }

            public override bool OnBattery => SDL.SDL_GetPowerInfo(out _, out _) == SDL.SDL_PowerState.SDL_POWERSTATE_ON_BATTERY;
        }
    }
}
