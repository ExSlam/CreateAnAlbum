using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Albummodelite
{
    /// <summary>
    /// Keeps the Top 20 chart lightweight by materializing cover art in four-row batches.
    /// Visible batches stay active, one neighboring batch is prefetched, and old batches are
    /// evicted from a small LRU cache instead of rebuilding all twenty covers at popup open.
    /// </summary>
    internal sealed class AlbumChartCoverVirtualizer : MonoBehaviour
    {
        private const int BatchSize = 4;
        private const int CacheLimit = 12;
        private const float CoverSize = 84f;

        private sealed class CacheEntry
        {
            internal GameObject Root;
            internal int LastUse;
        }

        private ScrollRect scroll;
        private RectTransform content;
        private RectTransform viewport;
        private List<AlbumData> albums;
        private float pitch;
        private int useCounter;
        private int lastPrimaryBatch = -1;
        private float lastScrollY = -1f;
        private int layoutWarmupFrames;
        private readonly Dictionary<int, CacheEntry> cache =
            new Dictionary<int, CacheEntry>();

        internal void Configure(
            ScrollRect targetScroll,
            RectTransform targetContent,
            List<AlbumData> entries,
            float rowPitch)
        {
            scroll = targetScroll;
            content = targetContent;
            viewport = targetScroll != null ? targetScroll.viewport : null;
            albums = entries != null ? new List<AlbumData>(entries) : new List<AlbumData>();
            pitch = Mathf.Max(1f, rowPitch);

            if (scroll != null)
                scroll.onValueChanged.AddListener(OnScroll);

            layoutWarmupFrames = 3;
            Refresh(true);
        }

        private void OnDestroy()
        {
            if (scroll != null)
                scroll.onValueChanged.RemoveListener(OnScroll);
            cache.Clear();
        }

        private void OnScroll(Vector2 ignored)
        {
            Refresh(false);
        }

        private void LateUpdate()
        {
            if (layoutWarmupFrames <= 0)
                return;

            layoutWarmupFrames--;
            Refresh(true);
        }

        private void Refresh(bool force)
        {
            if (scroll == null || content == null || viewport == null || albums == null || albums.Count == 0)
                return;

            float y = Mathf.Max(0f, content.anchoredPosition.y);
            int scrollDirection = lastScrollY < 0f || y >= lastScrollY ? 1 : -1;
            lastScrollY = y;
            int firstVisible = Mathf.Clamp(Mathf.FloorToInt(y / pitch), 0, albums.Count - 1);
            float viewportHeight = Mathf.Max(pitch, viewport.rect.height);
            int visibleRows = Mathf.Max(1, Mathf.CeilToInt(viewportHeight / pitch) + 1);
            int lastVisible = Mathf.Clamp(firstVisible + visibleRows - 1, 0, albums.Count - 1);

            int primaryBatch = firstVisible / BatchSize;
            int lastBatch = lastVisible / BatchSize;
            if (!force && primaryBatch == lastPrimaryBatch &&
                AllVisibleBatchesPresent(primaryBatch, lastBatch))
            {
                ApplyCulling(primaryBatch, lastBatch);
                return;
            }
            lastPrimaryBatch = primaryBatch;

            // Materialize every batch currently intersecting the viewport. Prefetch the next
            // four-row batch in the current scroll direction so crossing a batch boundary does
            // not synchronously construct four cover trees in the user's scroll gesture.
            for (int batch = primaryBatch; batch <= lastBatch; batch++)
                EnsureBatch(batch);

            int totalBatches = Mathf.CeilToInt(albums.Count / (float)BatchSize);
            int prefetch = scrollDirection >= 0
                ? lastBatch + 1
                : primaryBatch - 1;
            if (prefetch >= 0 && prefetch < totalBatches)
                EnsureBatch(prefetch);
            else
                prefetch = -1;

            ApplyCulling(primaryBatch, lastBatch);
            EvictOldEntries(primaryBatch, lastBatch, prefetch);
        }

        private bool AllVisibleBatchesPresent(int firstBatch, int lastBatch)
        {
            for (int batch = firstBatch; batch <= lastBatch; batch++)
            {
                int start = batch * BatchSize;
                int end = Mathf.Min(albums.Count, start + BatchSize);
                for (int index = start; index < end; index++)
                {
                    if (!cache.ContainsKey(index))
                        return false;
                }
            }
            return true;
        }

        private void EnsureBatch(int batch)
        {
            if (batch < 0 || albums == null)
                return;
            int start = batch * BatchSize;
            if (start >= albums.Count)
                return;
            int end = Mathf.Min(albums.Count, start + BatchSize);
            for (int index = start; index < end; index++)
                EnsureCover(index);
        }

        private void TouchBatch(int batch)
        {
            if (batch < 0)
                return;
            int start = batch * BatchSize;
            int end = Mathf.Min(albums.Count, start + BatchSize);
            for (int index = start; index < end; index++)
            {
                CacheEntry entry;
                if (cache.TryGetValue(index, out entry))
                    entry.LastUse = ++useCounter;
            }
        }

        private void EnsureCover(int index)
        {
            CacheEntry existing;
            if (cache.TryGetValue(index, out existing))
            {
                existing.LastUse = ++useCounter;
                return;
            }

            int rank = index + 1;
            Transform row = content.Find("AlbumRank_" + rank);
            Transform slot = row != null ? row.Find("CoverSlot") : null;
            if (slot == null)
                return;

            try
            {
                GameObject root = AlbumCoverRenderer.Build(
                    slot,
                    albums[index],
                    "ChartCover_" + rank,
                    Vector2.zero,
                    CoverSize
                );
                if (root == null)
                    return;
                cache[index] = new CacheEntry
                {
                    Root = root,
                    LastUse = ++useCounter
                };
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[AlbumChart] Cover failed for rank " + rank + ": " + ex.Message
                );
            }
        }

        private void ApplyCulling(int firstVisibleBatch, int lastVisibleBatch)
        {
            foreach (KeyValuePair<int, CacheEntry> pair in cache)
            {
                int batch = pair.Key / BatchSize;
                bool visible = batch >= firstVisibleBatch && batch <= lastVisibleBatch;
                if (pair.Value.Root != null && pair.Value.Root.activeSelf != visible)
                    pair.Value.Root.SetActive(visible);
                if (visible)
                    pair.Value.LastUse = ++useCounter;
            }
        }

        private void EvictOldEntries(int firstVisibleBatch, int lastVisibleBatch, int prefetchedBatch)
        {
            while (cache.Count > CacheLimit)
            {
                int oldestIndex = -1;
                int oldestUse = int.MaxValue;
                foreach (KeyValuePair<int, CacheEntry> pair in cache)
                {
                    int batch = pair.Key / BatchSize;
                    if (batch >= firstVisibleBatch && batch <= lastVisibleBatch)
                        continue;
                    if (batch == prefetchedBatch)
                        continue;
                    if (pair.Value.LastUse < oldestUse)
                    {
                        oldestUse = pair.Value.LastUse;
                        oldestIndex = pair.Key;
                    }
                }

                if (oldestIndex < 0)
                    break;

                CacheEntry entry = cache[oldestIndex];
                cache.Remove(oldestIndex);
                if (entry.Root != null)
                    Destroy(entry.Root);
            }
        }
    }
}
