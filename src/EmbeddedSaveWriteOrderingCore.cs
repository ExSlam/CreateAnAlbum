using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Albummodelite.EmbeddedSaveOrdering
{
    internal static class EmbeddedSaveOrderingConstants
    {
        internal const string StandaloneHarmonyId = "com.cosmo.savewriteorderingfix";
        internal const string ImDataCoreHarmonyId = "com.cosmo.imdatacore";
        internal const string GraduationDetailsHarmonyId = "com.cosmo.graduationdetails";
        internal const int ReadWaitMilliseconds = 30000;
        internal const string LogPrefix = "[AlbumSave/SWO] ";
    }

    internal static class EmbeddedSavePathResolver
    {
        internal static string ResolveWritePath(string dataFileName, bool fullPath)
        {
            if (string.IsNullOrWhiteSpace(dataFileName))
                return string.Empty;

            try
            {
                string path = fullPath
                    ? dataFileName
                    : Path.Combine(Application.persistentDataPath, "data", dataFileName + ".json");
                return Path.GetFullPath(path);
            }
            catch
            {
                return string.Empty;
            }
        }

        internal static string ResolveReadPath(string dataFileName)
        {
            if (string.IsNullOrWhiteSpace(dataFileName))
                return string.Empty;

            try
            {
                string path = Path.Combine(
                    Application.persistentDataPath,
                    "data",
                    dataFileName + ".json");
                path = path.Replace(".json.json", ".json");
                return Path.GetFullPath(path);
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    internal sealed class EmbeddedFrozenSave
    {
        internal string TargetPath = string.Empty;
        internal byte[] Payload = new byte[0];
    }

    internal sealed class EmbeddedSaveQueue
    {
        internal readonly object Sync = new object();
        internal readonly Queue<EmbeddedFrozenSave> Pending =
            new Queue<EmbeddedFrozenSave>();
        internal bool Draining;
    }

    /// <summary>
    /// CAA-local equivalent of the Save Write Ordering Fix behavior needed by this mod:
    /// freeze SavedData on the caller thread and serialize writes FIFO per physical path.
    /// This prevents vanilla worker threads from observing later mutations or finishing out of order.
    /// </summary>
    internal static class EmbeddedSaveOrderingCoordinator
    {
        private static readonly object RegistrySync = new object();
        private static readonly Dictionary<string, EmbeddedSaveQueue> Queues =
            new Dictionary<string, EmbeddedSaveQueue>(GetPathComparer());

        internal static void QueueVanillaSavedDataWrite(
            SaveManager.SavedData dataToSave,
            string dataFileName,
            bool isJson,
            bool fullPath)
        {
            string targetPath = EmbeddedSavePathResolver.ResolveWritePath(dataFileName, fullPath);
            if (string.IsNullOrEmpty(targetPath))
            {
                Debug.LogWarning(
                    EmbeddedSaveOrderingConstants.LogPrefix +
                    "Could not resolve save target; falling back to vanilla DataSaver.");
                DataSaver.saveData(dataToSave, dataFileName, isJson, fullPath);
                return;
            }

            byte[] frozenPayload;
            try
            {
                string text = isJson
                    ? JsonUtility.ToJson(dataToSave, true)
                    : Convert.ToString(dataToSave, CultureInfo.InvariantCulture);
                frozenPayload = Encoding.UTF8.GetBytes(text ?? string.Empty);
            }
            catch (Exception ex)
            {
                // A synchronous freeze is the safety property. If it fails, do not enqueue a
                // delayed reference to the mutable live SavedData graph. Let vanilla attempt the
                // save and keep CAA's checkpoint dirty until physical validation succeeds.
                Debug.LogWarning(
                    EmbeddedSaveOrderingConstants.LogPrefix +
                    "Could not freeze SavedData payload; falling back to vanilla writer: " +
                    ex.Message);
                DataSaver.saveData(dataToSave, dataFileName, isJson, fullPath);
                return;
            }

            EmbeddedSaveQueue queue;
            bool startDrainer = false;
            lock (RegistrySync)
            {
                if (!Queues.TryGetValue(targetPath, out queue))
                {
                    queue = new EmbeddedSaveQueue();
                    Queues.Add(targetPath, queue);
                }

                lock (queue.Sync)
                {
                    queue.Pending.Enqueue(new EmbeddedFrozenSave
                    {
                        TargetPath = targetPath,
                        Payload = frozenPayload
                    });
                    if (!queue.Draining)
                    {
                        queue.Draining = true;
                        startDrainer = true;
                    }
                    Monitor.PulseAll(queue.Sync);
                }
            }

            if (startDrainer)
                StartDrainer(queue);
        }

        internal static SaveManager.SavedData LoadSavedDataAfterPendingWrites(string dataFileName)
        {
            string path = EmbeddedSavePathResolver.ResolveReadPath(dataFileName);
            if (!string.IsNullOrEmpty(path) &&
                !WaitForPath(path, EmbeddedSaveOrderingConstants.ReadWaitMilliseconds))
            {
                Debug.LogWarning(
                    EmbeddedSaveOrderingConstants.LogPrefix +
                    "Timed out waiting for a pending save before loading " + path + ".");
            }

            return DataSaver.loadData<SaveManager.SavedData>(dataFileName);
        }

        internal static bool WaitForPath(string physicalPath, int timeoutMilliseconds)
        {
            if (string.IsNullOrEmpty(physicalPath))
                return true;

            EmbeddedSaveQueue queue;
            lock (RegistrySync)
            {
                if (!Queues.TryGetValue(physicalPath, out queue))
                    return true;
            }

            DateTime deadline = timeoutMilliseconds < 0
                ? DateTime.MaxValue
                : DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);

            lock (queue.Sync)
            {
                while (queue.Draining || queue.Pending.Count > 0)
                {
                    int wait;
                    if (timeoutMilliseconds < 0)
                    {
                        wait = Timeout.Infinite;
                    }
                    else
                    {
                        TimeSpan remaining = deadline - DateTime.UtcNow;
                        if (remaining <= TimeSpan.Zero)
                            return false;
                        wait = Math.Max(1, (int)Math.Min(int.MaxValue, remaining.TotalMilliseconds));
                    }

                    if (!Monitor.Wait(queue.Sync, wait) && timeoutMilliseconds >= 0)
                        return false;
                }
            }
            return true;
        }

        private static void StartDrainer(EmbeddedSaveQueue queue)
        {
            Thread thread = new Thread(new ThreadStart(delegate { Drain(queue); }));
            // Match vanilla's foreground save-thread semantics: process shutdown must not
            // tear down a queued write halfway through.
            thread.IsBackground = false;
            thread.Name = "CAA Save Ordering";
            thread.Start();
        }

        private static void Drain(EmbeddedSaveQueue queue)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            while (true)
            {
                EmbeddedFrozenSave next;
                lock (queue.Sync)
                {
                    if (queue.Pending.Count == 0)
                    {
                        queue.Draining = false;
                        Monitor.PulseAll(queue.Sync);
                        return;
                    }
                    next = queue.Pending.Dequeue();
                }

                try
                {
                    string directory = Path.GetDirectoryName(next.TargetPath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        Directory.CreateDirectory(directory);
                    File.WriteAllBytes(next.TargetPath, next.Payload ?? new byte[0]);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(
                        EmbeddedSaveOrderingConstants.LogPrefix +
                        "Ordered vanilla save write failed for " + next.TargetPath + ": " + ex.Message);
                }
                finally
                {
                    lock (queue.Sync)
                        Monitor.PulseAll(queue.Sync);
                }
            }
        }

        private static StringComparer GetPathComparer()
        {
            return Path.DirectorySeparatorChar == '\\'
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;
        }
    }
}
