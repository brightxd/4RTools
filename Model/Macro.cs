using System;
using System.Collections.Generic;
using System.Windows.Input;
using Newtonsoft.Json;
using System.Threading;
using _4RTools.Utils;
using System.Windows.Forms;

namespace _4RTools.Model
{
    public class MacroKey
    {
        public Key key { get; set; }
        public int delay { get; set; } = 50;
        public bool hasClick { get; set; } = false;
        // Local cooldown guard (ms). When > 0 and the skill was sent more recently than
        // this value, the chain resets to step 0 instead of wasting an iteration sending
        // a key the server will reject. Set to match the skill's actual cooldown.
        public int cooldownMs { get; set; } = 0;
        // Cast animation time (ms). stepLastSentAt is set to DateTime.UtcNow + castMs so the
        // CD guard doesn't clear until castMs + cooldownMs after the key press.
        // Set this to the skill's cast animation duration so the macro doesn't retry
        // during the cast window and cause a double-CD wait on server rejection.
        public int castMs { get; set; } = 0;
        // When true and this step is on CD, the chain skips it and advances to the next step
        // rather than resetting to step 0. Use for skills that should fire when available
        // but must not block the chain when cooling down (e.g., a damage-amplifier buff).
        public bool optional { get; set; } = false;
        // When true, this step is skipped (like optional) if the NEXT step is on CD.
        // Use for a buff skill that should only be cast immediately before the buffed skill
        // to avoid wasting the buff window when the main skill is not ready.
        public bool fireOnlyWithNext { get; set; } = false;
        // Keep this step pending while its local cooldown is active. This is useful
        // for the final skill in a chain: earlier setup skills must not be repeated.
        public bool waitForCooldown { get; set; } = false;
        // Optional status gate. -1 disables the gate; otherwise the step requires
        // the status to be present (or absent when conditionStatusPresent is false).
        public int conditionStatusId { get; set; } = -1;
        public bool conditionStatusPresent { get; set; } = true;

        public MacroKey(Key key, int delay)
        {
            this.key = key;
            this.delay = delay;
        }
    }

    public class ChainConfig
    {
        public int id;
        public Key trigger { get; set; }
        public Key daggerKey { get; set; }
        public Key instrumentKey { get; set; }
        public int delay { get; set; } = 50;
        public Dictionary<string, MacroKey> macroEntries { get; set; } = new Dictionary<string, MacroKey>();
        public bool infinityLoop { get; set; } = false;
        public bool infinityLoopOn { get; set; } = false;

        // Runtime state — not persisted in profile
        [JsonIgnore] public int currentChainStep { get; set; } = 0;
        // Per-step last-sent timestamps. Preserved across chain resets for CD tracking.
        [JsonIgnore] public DateTime[] stepLastSentAt = new DateTime[7];
        // When >= 0, chain loops to this step (0-indexed) after the last step fires instead of
        // resetting to step 0. Enables cycling of late combo skills without re-triggering setup.
        public int comboLoopBackStep { get; set; } = -1;

        public ChainConfig() { }
        public ChainConfig(int id)
        {
            this.id = id;
            this.macroEntries = new Dictionary<string, MacroKey>();
        }

        public ChainConfig(ChainConfig macro)
        {
            this.id = macro.id;
            this.delay = macro.delay;
            this.trigger = macro.trigger;
            this.daggerKey = macro.daggerKey;
            this.instrumentKey = macro.instrumentKey;
            this.infinityLoop = macro.infinityLoop;
            this.macroEntries = new Dictionary<string, MacroKey>(macro.macroEntries);
            this.comboLoopBackStep = macro.comboLoopBackStep;
        }
        public ChainConfig(int id, Key trigger)
        {
            this.id = id;
            this.trigger = trigger;
            this.macroEntries = new Dictionary<string, MacroKey>();
        }

        public void ResetChainState()
        {
            currentChainStep = 0;
            // stepLastSentAt preserved — CD tracking must survive chain resets.
        }
    }

    public class Macro : Action
    {
        public static string ACTION_NAME_SONG_MACRO = "SongMacro2.0";
        public static string ACTION_NAME_MACRO_SWITCH = "MacroSwitch2.0";

        public string actionName { get; set; }
        private _4RThread thread;
        public List<ChainConfig> chainConfigs { get; set; } = new List<ChainConfig>();

        public Macro(string macroname, int macroLanes)
        {
            this.actionName = macroname;
            for(int i = 1; i <= macroLanes; i++)
            {
                chainConfigs.Add(new ChainConfig(i, Key.None));
            }
        }

        public void ResetMacro(int macroId)
        {
            try
            {
                chainConfigs[macroId - 1] = new ChainConfig(macroId);
            }
            catch (Exception) { }
            
        }

        public string GetActionName()
        {
            return this.actionName;
        }

        public string GetConfiguration()
        {
            return JsonConvert.SerializeObject(this);
        }

        private void SendMacroKey(Client roClient, MacroKey macroKey, ChainConfig chainConfig)
        {
            if (chainConfig.instrumentKey != Key.None)
            {
                Keys instrumentKey = (Keys)Enum.Parse(typeof(Keys), chainConfig.instrumentKey.ToString());
                Interop.PostMessage(roClient.process.MainWindowHandle, Constants.WM_KEYDOWN_MSG_ID, instrumentKey, 0);
                Thread.Sleep(30);
            }

            Keys thisk = (Keys)Enum.Parse(typeof(Keys), macroKey.key.ToString());
            Thread.Sleep(macroKey.delay);
            Interop.PostMessage(roClient.process.MainWindowHandle, Constants.WM_KEYDOWN_MSG_ID, thisk, 0);

            if (macroKey.hasClick)
            {
                Interop.PostMessage(roClient.process.MainWindowHandle, Constants.WM_LBUTTONDOWN, 0, 0);
                Thread.Sleep(1);
                Interop.PostMessage(roClient.process.MainWindowHandle, Constants.WM_LBUTTONUP, 0, 0);
            }

            if (chainConfig.daggerKey != Key.None)
            {
                Keys daggerKey = (Keys)Enum.Parse(typeof(Keys), chainConfig.daggerKey.ToString());
                Interop.PostMessage(roClient.process.MainWindowHandle, Constants.WM_KEYDOWN_MSG_ID, daggerKey, 0);
                Thread.Sleep(30);
            }
        }

        private static bool IsStepOnCooldown(ChainConfig chainConfig, int step, MacroKey macroKey, DateTime now)
        {
            if (macroKey.cooldownMs <= 0 || step < 0 || step >= chainConfig.stepLastSentAt.Length)
                return false;

            DateTime lastSent = chainConfig.stepLastSentAt[step];
            return lastSent != DateTime.MinValue
                && (now - lastSent).TotalMilliseconds < macroKey.cooldownMs;
        }

        private static bool IsConditionSatisfied(
            MacroKey macroKey,
            HashSet<uint> activeStatusCodes,
            bool statusSnapshotAvailable)
        {
            if (macroKey.conditionStatusId < 0)
                return true;

            if (!statusSnapshotAvailable)
                return false;

            bool statusPresent = activeStatusCodes.Contains((uint)macroKey.conditionStatusId);
            return statusPresent == macroKey.conditionStatusPresent;
        }

        private static bool IsStepReady(
            ChainConfig chainConfig,
            int step,
            MacroKey macroKey,
            DateTime now,
            HashSet<uint> activeStatusCodes,
            bool statusSnapshotAvailable)
        {
            return macroKey.key != Key.None
                && !IsStepOnCooldown(chainConfig, step, macroKey, now)
                && IsConditionSatisfied(macroKey, activeStatusCodes, statusSnapshotAvailable);
        }

        private bool HasStatusConditions()
        {
            foreach (ChainConfig chainConfig in this.chainConfigs)
            {
                foreach (MacroKey macroKey in chainConfig.macroEntries.Values)
                {
                    if (macroKey.conditionStatusId >= 0)
                        return true;
                }
            }

            return false;
        }

        // Set to true and rebuild to write fire/skip events to %TEMP%\4rtools_trace.txt
        public static bool TraceEnabled = false;
        private static readonly string TraceFile =
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "4rtools_trace.txt");
        private static void Trace(string msg)
        {
            if (!TraceEnabled) return;
            try { System.IO.File.AppendAllText(TraceFile, $"{DateTime.UtcNow:HH:mm:ss.fff}  {msg}\n"); }
            catch { }
        }

        private int MacroExecutionThread(Client roClient)
        {
            HashSet<uint> activeStatusCodes = new HashSet<uint>();
            bool statusSnapshotAvailable = !HasStatusConditions()
                || roClient.TryGetActiveStatusCodes(out activeStatusCodes);

            foreach (ChainConfig chainConfig in this.chainConfigs)
            {
                if (chainConfig.trigger == Key.None) continue;

                if (!Keyboard.IsKeyDown(chainConfig.trigger))
                {
                    chainConfig.ResetChainState();
                    continue;
                }

                Dictionary<string, MacroKey> macro = chainConfig.macroEntries;
                int step = chainConfig.currentChainStep;

                string keyName = "in" + (step + 1) + "mac" + chainConfig.id;
                if (!macro.ContainsKey(keyName))
                {
                    chainConfig.ResetChainState();
                    continue;
                }

                MacroKey macroKey = macro[keyName];

                if (macroKey.key == Key.None)
                {
                    if (macroKey.optional)
                        chainConfig.currentChainStep = step + 1;
                    else
                        chainConfig.ResetChainState();
                    continue;
                }

                DateTime now = DateTime.UtcNow;

                // Per-step local cooldown guard.
                // waitForCooldown=true → hold this step (do not replay setup steps)
                // optional=true        → skip to next step
                // otherwise            → reset chain to step 0
                if (IsStepOnCooldown(chainConfig, step, macroKey, now))
                {
                    if (macroKey.waitForCooldown)
                    {
                        continue;
                    }

                    if (macroKey.optional)
                    {
                        chainConfig.currentChainStep = step + 1;
                        continue;
                    }

                    chainConfig.ResetChainState();
                    continue;
                }

                if (!IsConditionSatisfied(macroKey, activeStatusCodes, statusSnapshotAvailable))
                {
                    // A configured condition is a hard gate by default. If the
                    // step is optional, it may be skipped without firing.
                    if (macroKey.optional)
                    {
                        chainConfig.currentChainStep = step + 1;
                    }
                    continue;
                }

                // fireOnlyWithNext: skip when the next step is not ready, so a
                // setup skill is never spent without its immediately following skill.
                if (macroKey.fireOnlyWithNext)
                {
                    int nextIdx = step + 1;
                    string nextKeyName = "in" + (nextIdx + 1) + "mac" + chainConfig.id;
                    if (macro.ContainsKey(nextKeyName))
                    {
                        MacroKey nextKey = macro[nextKeyName];
                        if (!IsStepReady(
                            chainConfig,
                            nextIdx,
                            nextKey,
                            now,
                            activeStatusCodes,
                            statusSnapshotAvailable))
                        {
                            double nextCdRemaining = nextKey.cooldownMs > 0
                                ? nextKey.cooldownMs - (now - chainConfig.stepLastSentAt[nextIdx]).TotalMilliseconds
                                : 0;
                            Trace($"chain={chainConfig.id} step={step} key={macroKey.key} WNEXT_SKIP nextKey={nextKey.key} cdRemaining={nextCdRemaining:F0}ms");
                            chainConfig.currentChainStep = nextIdx;
                            continue;
                        }
                    }
                }

                SendMacroKey(roClient, macroKey, chainConfig);
                Trace($"chain={chainConfig.id} step={step} key={macroKey.key} FIRE");
                // Set stepLastSentAt to Now + castMs so the CD guard clears only after
                // castMs + cooldownMs from key press — matching when the game's CD actually starts.
                chainConfig.stepLastSentAt[step] = DateTime.UtcNow.AddMilliseconds(macroKey.castMs);

                int nextStep = step + 1;
                bool chainComplete = !macro.ContainsKey("in" + (nextStep + 1) + "mac" + chainConfig.id)
                    || macro["in" + (nextStep + 1) + "mac" + chainConfig.id].key == Key.None;

                if (chainComplete)
                {
                    if (chainConfig.comboLoopBackStep >= 0)
                        chainConfig.currentChainStep = chainConfig.comboLoopBackStep;
                    else
                        chainConfig.ResetChainState();
                }
                else
                {
                    chainConfig.currentChainStep = nextStep;
                }
            }
            Thread.Sleep(15);
            return 0;
        }

        public void Start()
        {
            Stop();
            Client roClient = ClientSingleton.GetClient();
            if (roClient != null)
            {
                this.thread = new _4RThread((_) => MacroExecutionThread(roClient));
                _4RThread.Start(this.thread);
            }
        }

        public void Stop()
        {
            _4RThread.Stop(this.thread);
        }
    }
}
