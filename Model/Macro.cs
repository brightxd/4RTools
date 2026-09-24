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
        // When true and this step is on CD, the chain skips it and advances to the next step
        // rather than resetting to step 0. Use for skills that should fire when available
        // but must not block the chain when cooling down (e.g., a damage-amplifier buff).
        public bool optional { get; set; } = false;

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
        [JsonIgnore] public DateTime skill2SentAt { get; set; } = DateTime.MinValue;
        // Per-step last-sent timestamps. Preserved across chain resets for CD tracking.
        [JsonIgnore] public DateTime[] stepLastSentAt = new DateTime[7];
        // Per-step "when did we arrive here in the current chain run" timestamps.
        // Reset in ResetChainState() so stale values from previous runs never trigger spurious timeouts.
        [JsonIgnore] public DateTime[] stepAttemptedAt = new DateTime[7];
        // How long (ms) to wait at a step before treating it as failed and resetting to step 0.
        [JsonIgnore] public int stepTimeoutMs { get; set; } = 300;
        // Window (ms) after skill 2 during which skills 3/4 are sent (server combo state duration).
        [JsonIgnore] public int comboWindowMs { get; set; } = 3000;

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
            stepAttemptedAt = new DateTime[7];
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

        private int MacroExecutionThread(Client roClient)
        {
            foreach (ChainConfig chainConfig in this.chainConfigs)
            {
                if (chainConfig.trigger == Key.None) continue;

                bool triggerHeld = Keyboard.IsKeyDown(chainConfig.trigger);

                if (!triggerHeld)
                {
                    chainConfig.ResetChainState();
                    continue;
                }

                Dictionary<string, MacroKey> macro = chainConfig.macroEntries;
                int step = chainConfig.currentChainStep;

                // Step timeout: if we've been stuck at this step too long, the skill probably
                // failed server-side. Reset to step 0 and try again.
                // Uses stepAttemptedAt (reset per chain run) — NOT stepLastSentAt (which is
                // never reset and would trigger spurious timeouts on re-press after release).
                if (chainConfig.stepAttemptedAt[step] == DateTime.MinValue)
                    chainConfig.stepAttemptedAt[step] = DateTime.Now;

                bool stepTimedOut = (DateTime.Now - chainConfig.stepAttemptedAt[step]).TotalMilliseconds > chainConfig.stepTimeoutMs;

                if (stepTimedOut)
                {
                    chainConfig.ResetChainState();
                    step = 0;
                }

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
                    {
                        int skippedNext = step + 1;
                        chainConfig.currentChainStep = skippedNext;
                        chainConfig.stepAttemptedAt[skippedNext] = DateTime.Now;
                        continue;
                    }
                    chainConfig.ResetChainState();
                    continue;
                }

                // Per-step local cooldown: skill was sent too recently.
                // Optional steps skip to the next step; mandatory steps reset chain to step 0.
                if (macroKey.cooldownMs > 0)
                {
                    DateTime lastSent = chainConfig.stepLastSentAt[step];
                    if (lastSent != DateTime.MinValue
                        && (DateTime.Now - lastSent).TotalMilliseconds < macroKey.cooldownMs)
                    {
                        if (macroKey.optional)
                        {
                            int skippedNext = step + 1;
                            chainConfig.currentChainStep = skippedNext;
                            chainConfig.stepAttemptedAt[skippedNext] = DateTime.Now;
                        }
                        else
                        {
                            chainConfig.ResetChainState();
                        }
                        continue;
                    }
                }

                // Skills 3+ (step >= 2) require skill 2 to have been sent within the combo window.
                // Guards skill 4 from being sent outside combo-ready state.
                if (step >= 2)
                {
                    bool withinComboWindow = chainConfig.skill2SentAt != DateTime.MinValue
                        && (DateTime.Now - chainConfig.skill2SentAt).TotalMilliseconds <= chainConfig.comboWindowMs;

                    if (!withinComboWindow)
                    {
                        chainConfig.ResetChainState();
                        continue;
                    }
                }

                SendMacroKey(roClient, macroKey, chainConfig);
                chainConfig.stepLastSentAt[step] = DateTime.Now;

                if (step == 1)
                    chainConfig.skill2SentAt = DateTime.Now;

                int nextStep = step + 1;
                bool chainComplete = !macro.ContainsKey("in" + (nextStep + 1) + "mac" + chainConfig.id)
                    || macro["in" + (nextStep + 1) + "mac" + chainConfig.id].key == Key.None;

                if (chainComplete)
                {
                    chainConfig.ResetChainState();
                }
                else
                {
                    chainConfig.currentChainStep = nextStep;
                    chainConfig.stepAttemptedAt[nextStep] = DateTime.Now;
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
