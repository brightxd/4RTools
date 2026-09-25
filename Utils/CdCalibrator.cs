using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace _4RTools.Utils
{
    public class SkillCdEntry
    {
        public long     address       { get; set; }
        public DateTime calibratedAt  { get; set; }
    }

    // Automated differential scanner that maps skillId → memory address of the CD float.
    // Call TryAttach when a game process is selected, then CalibrateAsync right after casting
    // a skill whose base CD is known. Confirmed addresses survive app restarts via SkillCdMap.json.
    public static class CdCalibrator
    {
        private static readonly string MapFile = "SkillCdMap.json";

        private static Dictionary<string, SkillCdEntry> _map    = new Dictionary<string, SkillCdEntry>();
        private static readonly object                   _lock   = new object();
        private static readonly SkillMemoryScanner       _scanner = new SkillMemoryScanner();

        // ── Lifecycle ────────────────────────────────────────────────────────────

        public static void Load()
        {
            if (!File.Exists(MapFile)) return;
            try
            {
                var loaded = JsonConvert.DeserializeObject<Dictionary<string, SkillCdEntry>>(
                    File.ReadAllText(MapFile));
                if (loaded != null)
                    lock (_lock) { _map = loaded; }
            }
            catch { }
        }

        public static void Save()
        {
            try
            {
                lock (_lock)
                    File.WriteAllText(MapFile, JsonConvert.SerializeObject(_map, Formatting.Indented));
            }
            catch { }
        }

        public static bool TryAttach(int pid) => _scanner.Attach(pid);
        public static void Detach()           => _scanner.Detach();

        // ── Address lookup ───────────────────────────────────────────────────────

        public static IntPtr GetAddress(string skillId)
        {
            if (string.IsNullOrEmpty(skillId)) return IntPtr.Zero;
            lock (_lock)
                return _map.TryGetValue(skillId, out var e) ? (IntPtr)e.address : IntPtr.Zero;
        }

        public static float ReadCd(string skillId)
        {
            IntPtr addr = GetAddress(skillId);
            return addr == IntPtr.Zero ? 0f : _scanner.ReadFloat(addr);
        }

        // ── Calibration ──────────────────────────────────────────────────────────
        //
        // Call this immediately after the tool fires the skill.
        // expectedCdSeconds: the skill's base CD (from your config / wiki).
        // Returns the confirmed address, or IntPtr.Zero if calibration failed.
        //
        // Timeline:
        //   T=0        scan for floats ≈ expectedCd          (snapshot 0)
        //   T=40%·CD   scan for floats ≈ 60% of expectedCd  (narrow snapshot 1)
        //   T=98%·CD   scan for floats ≈ 0                   (confirm + persist)
        public static async Task<IntPtr> CalibrateAsync(
            string skillId, float expectedCdSeconds,
            CancellationToken ct = default(CancellationToken))
        {
            const float tol = 0.20f; // ±20 % tolerance on expected value

            // Phase 1 — full scan right after cast
            var snap0 = await Task.Run(
                () => _scanner.ScanFloatRange(
                    expectedCdSeconds * (1f - tol),
                    expectedCdSeconds * (1f + tol)),
                ct).ConfigureAwait(false);

            if (snap0.Count == 0 || ct.IsCancellationRequested) return IntPtr.Zero;

            // Wait ~40 % of CD
            await Task.Delay((int)(expectedCdSeconds * 400f), ct).ConfigureAwait(false);
            if (ct.IsCancellationRequested) return IntPtr.Zero;

            // Phase 2 — narrow: must have decreased to ~60 % of original
            var snap1 = _scanner.FilterDecreased(snap0,
                expectedCdSeconds * 0.40f,
                expectedCdSeconds * 0.80f);

            if (snap1.Count == 0 || ct.IsCancellationRequested) return IntPtr.Zero;

            // Wait until CD is nearly expired (another ~58 % of CD)
            await Task.Delay((int)(expectedCdSeconds * 580f), ct).ConfigureAwait(false);
            if (ct.IsCancellationRequested) return IntPtr.Zero;

            // Phase 3 — confirm: value must now be near 0
            var snap2 = _scanner.FilterDecreased(snap1, 0f, expectedCdSeconds * 0.15f);

            if (snap2.Count == 0) return IntPtr.Zero;

            // If multiple survivors, pick the one closest to 0 (most likely the real timer)
            IntPtr best    = IntPtr.Zero;
            float  bestVal = float.MaxValue;
            foreach (var kvp in snap2)
                if (kvp.Value < bestVal) { bestVal = kvp.Value; best = kvp.Key; }

            if (best == IntPtr.Zero) return IntPtr.Zero;

            lock (_lock)
                _map[skillId] = new SkillCdEntry { address = best.ToInt64(), calibratedAt = DateTime.UtcNow };

            Save();
            return best;
        }

        // Removes a previously calibrated entry (e.g. after a game client update invalidates addresses).
        public static void Invalidate(string skillId)
        {
            lock (_lock) _map.Remove(skillId);
            Save();
        }
    }
}
