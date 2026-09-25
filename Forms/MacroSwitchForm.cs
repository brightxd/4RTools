using System;
using System.Windows.Forms;
using System.Windows.Input;
using System.Collections.Generic;
using _4RTools.Model;
using _4RTools.Utils;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Xml.Linq;

namespace _4RTools.Forms
{
    public partial class MacroSwitchForm : Form, IObserver
    {
        public static int TOTAL_MACRO_LANES = 5;
        public MacroSwitchForm(Subject subject)
        {
            subject.Attach(this);
            InitializeComponent();
            configureMacroLanes();
            addCooldownControls();
            addCastControls();
            addOptionalControls();
            addWNextControls();
            addLoopBackControls();
            addConditionControls();
            addMemoryCdControls();
            anchorGroups();
        }

        public void Update(ISubject subject)
        {
            switch ((subject as Subject).Message.code)
            {
                case MessageCode.PROFILE_CHANGED:
                    updateUi();
                    break;
                case MessageCode.TURN_ON:
                    ProfileSingleton.GetCurrent().MacroSwitch.Start();
                    break;
                case MessageCode.TURN_OFF:
                    ProfileSingleton.GetCurrent().MacroSwitch.Stop();
                    break;
            }
        }

        private void UpdatePanelData(int id)
        {
            try
            {
                GroupBox group = (GroupBox)this.Controls.Find("chainGroup" + id, true)[0];
                ChainConfig chainConfig = new ChainConfig(ProfileSingleton.GetCurrent().MacroSwitch.chainConfigs[id - 1]);

                // Set each slot directly without firing events to avoid transient None values
                // in macroEntries while the macro thread may be running.
                for (int slot = 1; slot <= 7; slot++)
                {
                    string cbName = "in" + slot + "mac" + id;
                    bool hasEntry = chainConfig.macroEntries.ContainsKey(cbName);

                    Control[] tb = group.Controls.Find(cbName, true);
                    if (tb.Length > 0)
                    {
                        TextBox textBox = (TextBox)tb[0];
                        textBox.TextChanged -= this.onTextChange;
                        textBox.Text = hasEntry ? chainConfig.macroEntries[cbName].key.ToString() : Key.None.ToString();
                        textBox.TextChanged += this.onTextChange;
                    }

                    Control[] d = group.Controls.Find($"{cbName}delay", true);
                    if (d.Length > 0)
                    {
                        NumericUpDown delayInput = (NumericUpDown)d[0];
                        delayInput.ValueChanged -= this.onDelayChange;
                        delayInput.Value = hasEntry ? chainConfig.macroEntries[cbName].delay : 0;
                        delayInput.ValueChanged += this.onDelayChange;
                    }

                    Control[] c = group.Controls.Find($"{cbName}click", true);
                    if (c.Length > 0)
                    {
                        CheckBox checkInput = (CheckBox)c[0];
                        checkInput.CheckedChanged -= this.onCheckClickChange;
                        checkInput.Checked = hasEntry && chainConfig.macroEntries[cbName].hasClick;
                        checkInput.CheckedChanged += this.onCheckClickChange;
                    }

                    Control[] cd = group.Controls.Find($"{cbName}cooldown", true);
                    if (cd.Length > 0)
                    {
                        NumericUpDown cdInput = (NumericUpDown)cd[0];
                        cdInput.ValueChanged -= this.onCooldownChange;
                        cdInput.Value = hasEntry ? chainConfig.macroEntries[cbName].cooldownMs : 0;
                        cdInput.ValueChanged += this.onCooldownChange;
                    }

                    Control[] cast = group.Controls.Find($"{cbName}cast", true);
                    if (cast.Length > 0)
                    {
                        NumericUpDown castInput = (NumericUpDown)cast[0];
                        castInput.ValueChanged -= this.onCastChange;
                        castInput.Value = hasEntry ? chainConfig.macroEntries[cbName].castMs : 0;
                        castInput.ValueChanged += this.onCastChange;
                    }

                    Control[] op = group.Controls.Find($"{cbName}opt", true);
                    if (op.Length > 0)
                    {
                        CheckBox optInput = (CheckBox)op[0];
                        optInput.CheckedChanged -= this.onOptionalChange;
                        optInput.Checked = hasEntry && chainConfig.macroEntries[cbName].optional;
                        optInput.CheckedChanged += this.onOptionalChange;
                    }

                    Control[] wn = group.Controls.Find($"{cbName}wnext", true);
                    if (wn.Length > 0)
                    {
                        CheckBox wnInput = (CheckBox)wn[0];
                        wnInput.CheckedChanged -= this.onWNextChange;
                        wnInput.Checked = hasEntry && chainConfig.macroEntries[cbName].fireOnlyWithNext;
                        wnInput.CheckedChanged += this.onWNextChange;
                    }

                    Control[] wait = group.Controls.Find($"{cbName}waitcd", true);
                    if (wait.Length > 0)
                    {
                        CheckBox waitInput = (CheckBox)wait[0];
                        waitInput.CheckedChanged -= this.onWaitCooldownChange;
                        waitInput.Checked = hasEntry && chainConfig.macroEntries[cbName].waitForCooldown;
                        waitInput.CheckedChanged += this.onWaitCooldownChange;
                    }

                    Control[] conditionStatus = group.Controls.Find($"{cbName}condstatus", true);
                    if (conditionStatus.Length > 0)
                    {
                        NumericUpDown statusInput = (NumericUpDown)conditionStatus[0];
                        statusInput.ValueChanged -= this.onConditionStatusChange;
                        statusInput.Value = hasEntry ? chainConfig.macroEntries[cbName].conditionStatusId : -1;
                        statusInput.ValueChanged += this.onConditionStatusChange;
                    }

                    Control[] conditionPresent = group.Controls.Find($"{cbName}condpresent", true);
                    if (conditionPresent.Length > 0)
                    {
                        CheckBox presentInput = (CheckBox)conditionPresent[0];
                        presentInput.CheckedChanged -= this.onConditionPresentChange;
                        presentInput.Checked = hasEntry && chainConfig.macroEntries[cbName].conditionStatusPresent;
                        presentInput.CheckedChanged += this.onConditionPresentChange;
                    }

                    Control[] postCast = group.Controls.Find($"{cbName}postcast", true);
                    if (postCast.Length > 0)
                    {
                        NumericUpDown postCastInput = (NumericUpDown)postCast[0];
                        postCastInput.ValueChanged -= this.onPostCastChange;
                        postCastInput.Value = hasEntry ? chainConfig.macroEntries[cbName].postCastDelayMs : 0;
                        postCastInput.ValueChanged += this.onPostCastChange;
                    }

                    Control[] skillIdCtrl = group.Controls.Find($"{cbName}skillid", true);
                    if (skillIdCtrl.Length > 0)
                    {
                        TextBox skillIdInput = (TextBox)skillIdCtrl[0];
                        skillIdInput.TextChanged -= this.onSkillIdChange;
                        skillIdInput.Text = hasEntry ? (chainConfig.macroEntries[cbName].skillId ?? "") : "";
                        skillIdInput.TextChanged += this.onSkillIdChange;
                    }
                }

                // Loop-back: 0 = disabled, 1-7 = loop to that slot (1-indexed)
                Control[] lb = group.Controls.Find($"chainLoopFrom{id}", true);
                if (lb.Length > 0)
                {
                    NumericUpDown lbInput = (NumericUpDown)lb[0];
                    lbInput.ValueChanged -= this.onLoopBackChange;
                    lbInput.Value = chainConfig.comboLoopBackStep < 0 ? 0 : chainConfig.comboLoopBackStep + 1;
                    lbInput.ValueChanged += this.onLoopBackChange;
                }
            }
            catch { };
        }

        private void onTextChange(object sender, EventArgs e)
        {
            TextBox textBox = (TextBox)sender;
            int chainID = Int16.Parse(textBox.Parent.Name.Split(new[] { "chainGroup" }, StringSplitOptions.None)[1]);
            GroupBox group = (GroupBox)this.Controls.Find("chainGroup" + chainID, true)[0];
            ChainConfig chainConfig = ProfileSingleton.GetCurrent().MacroSwitch.chainConfigs.Find(config => config.id == chainID);

            Key key = (Key)Enum.Parse(typeof(Key), textBox.Text.ToString());
            NumericUpDown delayInput = (NumericUpDown)group.Controls.Find($"{textBox.Name}delay", true)[0];

            int existingCooldown = chainConfig.macroEntries.ContainsKey(textBox.Name)
                ? chainConfig.macroEntries[textBox.Name].cooldownMs
                : 0;
            bool existingOptional = chainConfig.macroEntries.ContainsKey(textBox.Name)
                && chainConfig.macroEntries[textBox.Name].optional;
            bool existingFireOnlyWithNext = chainConfig.macroEntries.ContainsKey(textBox.Name)
                && chainConfig.macroEntries[textBox.Name].fireOnlyWithNext;
            MacroKey existingMacroKey = chainConfig.macroEntries.ContainsKey(textBox.Name)
                ? chainConfig.macroEntries[textBox.Name]
                : null;
            MacroKey updatedMacroKey = new MacroKey(key, decimal.ToInt16(delayInput.Value));
            if (existingMacroKey != null)
            {
                updatedMacroKey.hasClick = existingMacroKey.hasClick;
                updatedMacroKey.cooldownMs = existingCooldown;
                updatedMacroKey.castMs = existingMacroKey.castMs;
                updatedMacroKey.optional = existingOptional;
                updatedMacroKey.fireOnlyWithNext = existingFireOnlyWithNext;
                updatedMacroKey.waitForCooldown = existingMacroKey.waitForCooldown;
                updatedMacroKey.postCastDelayMs = existingMacroKey.postCastDelayMs;
                updatedMacroKey.conditionStatusId = existingMacroKey.conditionStatusId;
                updatedMacroKey.conditionStatusPresent = existingMacroKey.conditionStatusPresent;
                updatedMacroKey.skillId = existingMacroKey.skillId;
                updatedMacroKey.limboGuardMs = existingMacroKey.limboGuardMs;
            }
            chainConfig.macroEntries[textBox.Name] = updatedMacroKey;

            bool isFirstInput = Regex.IsMatch(textBox.Name, $"in1mac{chainID}");
            if (isFirstInput) { chainConfig.trigger = key; }

            ProfileSingleton.SetConfiguration(ProfileSingleton.GetCurrent().MacroSwitch);
        }


        private void onDelayChange(object sender, EventArgs e)
        {
            NumericUpDown delayInput = (NumericUpDown)sender;
            int chainID = Int16.Parse(delayInput.Parent.Name.Split(new[] { "chainGroup" }, StringSplitOptions.None)[1]);
            ChainConfig chainConfig = ProfileSingleton.GetCurrent().MacroSwitch.chainConfigs.Find(config => config.id == chainID);

            String cbName = delayInput.Name.Split(new[] { "delay" }, StringSplitOptions.None)[0];
            GetOrCreateMacroKey(chainConfig, cbName).delay = decimal.ToInt16(delayInput.Value);

            ProfileSingleton.SetConfiguration(ProfileSingleton.GetCurrent().MacroSwitch);
        }

        private void onCheckClickChange(object sender, EventArgs e)
        {
            CheckBox checkInput = (CheckBox)sender;
            int chainID = Int16.Parse(checkInput.Parent.Name.Split(new[] { "chainGroup" }, StringSplitOptions.None)[1]);
            ChainConfig chainConfig = ProfileSingleton.GetCurrent().MacroSwitch.chainConfigs.Find(config => config.id == chainID);

            String cbName = checkInput.Name.Split(new[] { "click" }, StringSplitOptions.None)[0];
            GetOrCreateMacroKey(chainConfig, cbName).hasClick = checkInput.Checked;
            ProfileSingleton.SetConfiguration(ProfileSingleton.GetCurrent().MacroSwitch);
        }

        private MacroKey GetOrCreateMacroKey(ChainConfig chainConfig, string name)
        {
            if (!chainConfig.macroEntries.ContainsKey(name))
                chainConfig.macroEntries[name] = new MacroKey(System.Windows.Input.Key.None, 0);

            return chainConfig.macroEntries[name];
        }

        private void onCooldownChange(object sender, EventArgs e)
        {
            NumericUpDown cdInput = (NumericUpDown)sender;
            int chainID = Int16.Parse(cdInput.Parent.Name.Split(new[] { "chainGroup" }, StringSplitOptions.None)[1]);
            ChainConfig chainConfig = ProfileSingleton.GetCurrent().MacroSwitch.chainConfigs.Find(config => config.id == chainID);

            String cbName = cdInput.Name.Split(new[] { "cooldown" }, StringSplitOptions.None)[0];
            GetOrCreateMacroKey(chainConfig, cbName).cooldownMs = decimal.ToInt32(cdInput.Value);
            ProfileSingleton.SetConfiguration(ProfileSingleton.GetCurrent().MacroSwitch);
        }

        private void onOptionalChange(object sender, EventArgs e)
        {
            CheckBox optInput = (CheckBox)sender;
            int chainID = Int16.Parse(optInput.Parent.Name.Split(new[] { "chainGroup" }, StringSplitOptions.None)[1]);
            ChainConfig chainConfig = ProfileSingleton.GetCurrent().MacroSwitch.chainConfigs.Find(config => config.id == chainID);

            String cbName = optInput.Name.Split(new[] { "opt" }, StringSplitOptions.None)[0];
            GetOrCreateMacroKey(chainConfig, cbName).optional = optInput.Checked;
            ProfileSingleton.SetConfiguration(ProfileSingleton.GetCurrent().MacroSwitch);
        }

        private void onLoopBackChange(object sender, EventArgs e)
        {
            NumericUpDown lbInput = (NumericUpDown)sender;
            int chainID = Int16.Parse(lbInput.Parent.Name.Split(new[] { "chainGroup" }, StringSplitOptions.None)[1]);
            ChainConfig chainConfig = ProfileSingleton.GetCurrent().MacroSwitch.chainConfigs.Find(config => config.id == chainID);

            int slotValue = (int)lbInput.Value;
            chainConfig.comboLoopBackStep = slotValue == 0 ? -1 : slotValue - 1;
            ProfileSingleton.SetConfiguration(ProfileSingleton.GetCurrent().MacroSwitch);
        }

        private void onWNextChange(object sender, EventArgs e)
        {
            CheckBox wnInput = (CheckBox)sender;
            int chainID = Int16.Parse(wnInput.Parent.Name.Split(new[] { "chainGroup" }, StringSplitOptions.None)[1]);
            ChainConfig chainConfig = ProfileSingleton.GetCurrent().MacroSwitch.chainConfigs.Find(config => config.id == chainID);

            String cbName = wnInput.Name.Split(new[] { "wnext" }, StringSplitOptions.None)[0];
            GetOrCreateMacroKey(chainConfig, cbName).fireOnlyWithNext = wnInput.Checked;
            ProfileSingleton.SetConfiguration(ProfileSingleton.GetCurrent().MacroSwitch);
        }

        private void onCastChange(object sender, EventArgs e)
        {
            NumericUpDown castInput = (NumericUpDown)sender;
            int chainID = Int16.Parse(castInput.Parent.Name.Split(new[] { "chainGroup" }, StringSplitOptions.None)[1]);
            ChainConfig chainConfig = ProfileSingleton.GetCurrent().MacroSwitch.chainConfigs.Find(config => config.id == chainID);

            String cbName = castInput.Name.Split(new[] { "cast" }, StringSplitOptions.None)[0];
            GetOrCreateMacroKey(chainConfig, cbName).castMs = decimal.ToInt32(castInput.Value);
            ProfileSingleton.SetConfiguration(ProfileSingleton.GetCurrent().MacroSwitch);
        }

        private void onWaitCooldownChange(object sender, EventArgs e)
        {
            CheckBox waitInput = (CheckBox)sender;
            int chainID = Int16.Parse(waitInput.Parent.Name.Split(new[] { "chainGroup" }, StringSplitOptions.None)[1]);
            ChainConfig chainConfig = ProfileSingleton.GetCurrent().MacroSwitch.chainConfigs.Find(config => config.id == chainID);
            string cbName = waitInput.Name.Split(new[] { "waitcd" }, StringSplitOptions.None)[0];
            GetOrCreateMacroKey(chainConfig, cbName).waitForCooldown = waitInput.Checked;
            ProfileSingleton.SetConfiguration(ProfileSingleton.GetCurrent().MacroSwitch);
        }

        private void onPostCastChange(object sender, EventArgs e)
        {
            NumericUpDown postCastInput = (NumericUpDown)sender;
            int chainID = Int16.Parse(postCastInput.Parent.Name.Split(new[] { "chainGroup" }, StringSplitOptions.None)[1]);
            ChainConfig chainConfig = ProfileSingleton.GetCurrent().MacroSwitch.chainConfigs.Find(config => config.id == chainID);
            string cbName = postCastInput.Name.Split(new[] { "postcast" }, StringSplitOptions.None)[0];
            GetOrCreateMacroKey(chainConfig, cbName).postCastDelayMs = decimal.ToInt32(postCastInput.Value);
            ProfileSingleton.SetConfiguration(ProfileSingleton.GetCurrent().MacroSwitch);
        }

        private void onConditionStatusChange(object sender, EventArgs e)
        {
            NumericUpDown statusInput = (NumericUpDown)sender;
            int chainID = Int16.Parse(statusInput.Parent.Name.Split(new[] { "chainGroup" }, StringSplitOptions.None)[1]);
            ChainConfig chainConfig = ProfileSingleton.GetCurrent().MacroSwitch.chainConfigs.Find(config => config.id == chainID);
            string cbName = statusInput.Name.Split(new[] { "condstatus" }, StringSplitOptions.None)[0];
            GetOrCreateMacroKey(chainConfig, cbName).conditionStatusId = decimal.ToInt32(statusInput.Value);
            ProfileSingleton.SetConfiguration(ProfileSingleton.GetCurrent().MacroSwitch);
        }

        private void onConditionPresentChange(object sender, EventArgs e)
        {
            CheckBox presentInput = (CheckBox)sender;
            int chainID = Int16.Parse(presentInput.Parent.Name.Split(new[] { "chainGroup" }, StringSplitOptions.None)[1]);
            ChainConfig chainConfig = ProfileSingleton.GetCurrent().MacroSwitch.chainConfigs.Find(config => config.id == chainID);
            string cbName = presentInput.Name.Split(new[] { "condpresent" }, StringSplitOptions.None)[0];
            GetOrCreateMacroKey(chainConfig, cbName).conditionStatusPresent = presentInput.Checked;
            ProfileSingleton.SetConfiguration(ProfileSingleton.GetCurrent().MacroSwitch);
        }

        private void addCastControls()
        {
            const int CAST_ROW_Y = 117;
            const int EXPAND = 20;
            const int GAP = 4;
            int[] slotX = { 66, 135, 204, 273, 342, 411, 480 };

            int y = 12;
            for (int i = 1; i <= TOTAL_MACRO_LANES; i++)
            {
                GroupBox group = (GroupBox)this.Controls.Find("chainGroup" + i, true)[0];

                group.Location = new System.Drawing.Point(group.Location.X, y);
                group.Size = new System.Drawing.Size(group.Width, group.Height + EXPAND);

                Label castLabel = new Label();
                castLabel.Name = "labelCast" + i;
                castLabel.Text = "Cast(ms):";
                castLabel.AutoSize = true;
                castLabel.Location = new System.Drawing.Point(4, CAST_ROW_Y + 2);
                group.Controls.Add(castLabel);

                for (int slot = 1; slot <= 7; slot++)
                {
                    NumericUpDown castInput = new NumericUpDown();
                    castInput.Name = "in" + slot + "mac" + i + "cast";
                    castInput.Location = new System.Drawing.Point(slotX[slot - 1], CAST_ROW_Y);
                    castInput.Size = new System.Drawing.Size(47, 20);
                    castInput.Maximum = 5000;
                    castInput.TabIndex = 800 + (i - 1) * 7 + slot;
                    castInput.ValueChanged += new System.EventHandler(this.onCastChange);
                    group.Controls.Add(castInput);
                }

                y += group.Height + GAP;
            }
        }

        private void addOptionalControls()
        {
            const int OPT_ROW_Y = 139;
            const int EXPAND = 20;
            const int GAP = 4;
            int[] slotX = { 66, 135, 204, 273, 342, 411, 480 };

            int y = 12;
            for (int i = 1; i <= TOTAL_MACRO_LANES; i++)
            {
                GroupBox group = (GroupBox)this.Controls.Find("chainGroup" + i, true)[0];

                group.Location = new System.Drawing.Point(group.Location.X, y);
                group.Size = new System.Drawing.Size(group.Width, group.Height + EXPAND);

                Label optLabel = new Label();
                optLabel.Name = "labelOpt" + i;
                optLabel.Text = "Opt:";
                optLabel.AutoSize = true;
                optLabel.Location = new System.Drawing.Point(4, OPT_ROW_Y + 2);
                group.Controls.Add(optLabel);

                for (int slot = 1; slot <= 7; slot++)
                {
                    CheckBox optInput = new CheckBox();
                    optInput.Name = "in" + slot + "mac" + i + "opt";
                    optInput.Text = "";
                    optInput.Location = new System.Drawing.Point(slotX[slot - 1] + 4, OPT_ROW_Y);
                    optInput.Size = new System.Drawing.Size(40, 17);
                    optInput.TabIndex = 500 + (i - 1) * 7 + slot;
                    optInput.CheckedChanged += new System.EventHandler(this.onOptionalChange);
                    group.Controls.Add(optInput);
                }

                y += group.Height + GAP;
            }
        }

        private void addCooldownControls()
        {
            const int CD_ROW_Y = 95;
            const int EXPAND = 20;
            const int GAP = 4;
            int[] slotX = { 66, 135, 204, 273, 342, 411, 480 };

            int y = 12;
            for (int i = 1; i <= TOTAL_MACRO_LANES; i++)
            {
                GroupBox group = (GroupBox)this.Controls.Find("chainGroup" + i, true)[0];

                group.Location = new System.Drawing.Point(group.Location.X, y);
                group.Size = new System.Drawing.Size(group.Width, group.Height + EXPAND);

                Label cdLabel = new Label();
                cdLabel.Name = "labelCd" + i;
                cdLabel.Text = "CD(ms):";
                cdLabel.AutoSize = true;
                cdLabel.Location = new System.Drawing.Point(4, CD_ROW_Y + 2);
                group.Controls.Add(cdLabel);

                for (int slot = 1; slot <= 7; slot++)
                {
                    NumericUpDown cdInput = new NumericUpDown();
                    cdInput.Name = "in" + slot + "mac" + i + "cooldown";
                    cdInput.Location = new System.Drawing.Point(slotX[slot - 1], CD_ROW_Y);
                    cdInput.Size = new System.Drawing.Size(47, 20);
                    cdInput.Maximum = 30000;
                    cdInput.TabIndex = 400 + (i - 1) * 7 + slot;
                    cdInput.ValueChanged += new System.EventHandler(this.onCooldownChange);
                    group.Controls.Add(cdInput);
                }

                y += group.Height + GAP;
            }
        }

        private void addWNextControls()
        {
            const int WNEXT_ROW_Y = 161;
            const int EXPAND = 20;
            const int GAP = 4;
            int[] slotX = { 66, 135, 204, 273, 342, 411, 480 };

            int y = 12;
            for (int i = 1; i <= TOTAL_MACRO_LANES; i++)
            {
                GroupBox group = (GroupBox)this.Controls.Find("chainGroup" + i, true)[0];

                group.Location = new System.Drawing.Point(group.Location.X, y);
                group.Size = new System.Drawing.Size(group.Width, group.Height + EXPAND);

                Label wnLabel = new Label();
                wnLabel.Name = "labelWNext" + i;
                wnLabel.Text = "W/Next:";
                wnLabel.AutoSize = true;
                wnLabel.Location = new System.Drawing.Point(4, WNEXT_ROW_Y + 2);
                group.Controls.Add(wnLabel);

                for (int slot = 1; slot <= 7; slot++)
                {
                    CheckBox wnInput = new CheckBox();
                    wnInput.Name = "in" + slot + "mac" + i + "wnext";
                    wnInput.Text = "";
                    wnInput.Location = new System.Drawing.Point(slotX[slot - 1] + 4, WNEXT_ROW_Y);
                    wnInput.Size = new System.Drawing.Size(40, 17);
                    wnInput.TabIndex = 700 + (i - 1) * 7 + slot;
                    wnInput.CheckedChanged += new System.EventHandler(this.onWNextChange);
                    group.Controls.Add(wnInput);
                }

                y += group.Height + GAP;
            }
        }

        private void addLoopBackControls()
        {
            const int LOOP_ROW_Y = 183;
            const int EXPAND = 20;
            const int GAP = 4;

            int y = 12;
            for (int i = 1; i <= TOTAL_MACRO_LANES; i++)
            {
                GroupBox group = (GroupBox)this.Controls.Find("chainGroup" + i, true)[0];

                group.Location = new System.Drawing.Point(group.Location.X, y);
                group.Size = new System.Drawing.Size(group.Width, group.Height + EXPAND);

                Label loopLabel = new Label();
                loopLabel.Name = "labelLoop" + i;
                loopLabel.Text = "Loop from slot:";
                loopLabel.AutoSize = true;
                loopLabel.Location = new System.Drawing.Point(4, LOOP_ROW_Y + 2);
                group.Controls.Add(loopLabel);

                NumericUpDown lbInput = new NumericUpDown();
                lbInput.Name = "chainLoopFrom" + i;
                lbInput.Location = new System.Drawing.Point(100, LOOP_ROW_Y);
                lbInput.Size = new System.Drawing.Size(47, 20);
                lbInput.Minimum = 0;
                lbInput.Maximum = 7;
                lbInput.TabIndex = 600 + i;
                lbInput.ValueChanged += new System.EventHandler(this.onLoopBackChange);
                group.Controls.Add(lbInput);

                y += group.Height + GAP;
            }
        }

        private void addConditionControls()
        {
            const int WAIT_ROW_Y = 205;
            const int STATUS_ROW_Y = 227;
            const int PRESENT_ROW_Y = 249;
            const int POST_CAST_ROW_Y = 271;
            const int EXPAND = 80;
            const int GAP = 4;
            int[] slotX = { 66, 135, 204, 273, 342, 411, 480 };

            int y = 12;
            for (int i = 1; i <= TOTAL_MACRO_LANES; i++)
            {
                GroupBox group = (GroupBox)this.Controls.Find("chainGroup" + i, true)[0];
                group.Location = new System.Drawing.Point(group.Location.X, y);
                group.Size = new System.Drawing.Size(group.Width, group.Height + EXPAND);

                Label waitLabel = new Label();
                waitLabel.Name = "labelWaitCd" + i;
                waitLabel.Text = "Wait CD:";
                waitLabel.AutoSize = true;
                waitLabel.Location = new System.Drawing.Point(4, WAIT_ROW_Y + 2);
                group.Controls.Add(waitLabel);

                Label statusLabel = new Label();
                statusLabel.Name = "labelCondStatus" + i;
                statusLabel.Text = "Status ID:";
                statusLabel.AutoSize = true;
                statusLabel.Location = new System.Drawing.Point(4, STATUS_ROW_Y + 2);
                group.Controls.Add(statusLabel);

                Label presentLabel = new Label();
                presentLabel.Name = "labelCondPresent" + i;
                presentLabel.Text = "Has status:";
                presentLabel.AutoSize = true;
                presentLabel.Location = new System.Drawing.Point(4, PRESENT_ROW_Y + 2);
                group.Controls.Add(presentLabel);

                Label postCastLabel = new Label();
                postCastLabel.Name = "labelPostCast" + i;
                postCastLabel.Text = "Post(ms):";
                postCastLabel.AutoSize = true;
                postCastLabel.Location = new System.Drawing.Point(4, POST_CAST_ROW_Y + 2);
                group.Controls.Add(postCastLabel);

                for (int slot = 1; slot <= 7; slot++)
                {
                    CheckBox waitInput = new CheckBox();
                    waitInput.Name = "in" + slot + "mac" + i + "waitcd";
                    waitInput.Location = new System.Drawing.Point(slotX[slot - 1] + 4, WAIT_ROW_Y);
                    waitInput.Size = new System.Drawing.Size(40, 17);
                    waitInput.CheckedChanged += new System.EventHandler(this.onWaitCooldownChange);
                    group.Controls.Add(waitInput);

                    NumericUpDown statusInput = new NumericUpDown();
                    statusInput.Name = "in" + slot + "mac" + i + "condstatus";
                    statusInput.Location = new System.Drawing.Point(slotX[slot - 1], STATUS_ROW_Y);
                    statusInput.Size = new System.Drawing.Size(47, 20);
                    statusInput.Minimum = -1;
                    statusInput.Maximum = 5000;
                    statusInput.Value = -1;
                    statusInput.ValueChanged += new System.EventHandler(this.onConditionStatusChange);
                    group.Controls.Add(statusInput);

                    CheckBox presentInput = new CheckBox();
                    presentInput.Name = "in" + slot + "mac" + i + "condpresent";
                    presentInput.Location = new System.Drawing.Point(slotX[slot - 1] + 4, PRESENT_ROW_Y);
                    presentInput.Size = new System.Drawing.Size(40, 17);
                    presentInput.Checked = true;
                    presentInput.CheckedChanged += new System.EventHandler(this.onConditionPresentChange);
                    group.Controls.Add(presentInput);

                    NumericUpDown postCastInput = new NumericUpDown();
                    postCastInput.Name = "in" + slot + "mac" + i + "postcast";
                    postCastInput.Location = new System.Drawing.Point(slotX[slot - 1], POST_CAST_ROW_Y);
                    postCastInput.Size = new System.Drawing.Size(47, 20);
                    postCastInput.Maximum = 5000;
                    postCastInput.ValueChanged += new System.EventHandler(this.onPostCastChange);
                    group.Controls.Add(postCastInput);
                }

                y += group.Height + GAP;
            }
        }

        private void addMemoryCdControls()
        {
            const int SKILL_ROW_Y = 293;
            const int EXPAND     = 22;
            const int GAP        = 4;
            int[] slotX = { 66, 135, 204, 273, 342, 411, 480 };

            int y = 12;
            for (int i = 1; i <= TOTAL_MACRO_LANES; i++)
            {
                GroupBox group = (GroupBox)this.Controls.Find("chainGroup" + i, true)[0];
                group.Location = new System.Drawing.Point(group.Location.X, y);
                group.Size = new System.Drawing.Size(group.Width, group.Height + EXPAND);

                Label lbl = new Label { Name = "labelSkillId" + i, Text = "Skill ID:", AutoSize = true };
                lbl.Location = new System.Drawing.Point(4, SKILL_ROW_Y + 2);
                group.Controls.Add(lbl);

                for (int slot = 1; slot <= 7; slot++)
                {
                    TextBox tb = new TextBox();
                    tb.Name     = "in" + slot + "mac" + i + "skillid";
                    tb.Location = new System.Drawing.Point(slotX[slot - 1], SKILL_ROW_Y);
                    tb.Size     = new System.Drawing.Size(63, 20);
                    tb.TextChanged += new System.EventHandler(this.onSkillIdChange);
                    group.Controls.Add(tb);
                }

                y += group.Height + GAP;
            }
        }

        // Anchor all chain groups to stretch with the form width.
        private void anchorGroups()
        {
            for (int i = 1; i <= TOTAL_MACRO_LANES; i++)
            {
                var found = this.Controls.Find("chainGroup" + i, true);
                if (found.Length > 0)
                    found[0].Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            }
        }

        // Suffixes with no extra X offset (inputs: TextBox / NumericUpDown).
        private static readonly string[] _slotSuffixesDirect  =
            { "", "delay", "cooldown", "cast", "condstatus", "postcast", "skillid" };
        // Suffixes that carry +4 px (CheckBox controls that need a small left indent).
        private static readonly string[] _slotSuffixesOffset4 =
            { "click", "opt", "wnext", "waitcd", "condpresent" };

        // Redistributes the 7 slot columns evenly across the current group width.
        private void RelayoutGroups()
        {
            const int LABEL_COL  = 64;
            const int RIGHT_PAD  = 8;
            const int MIN_STRIDE = 69;

            for (int i = 1; i <= TOTAL_MACRO_LANES; i++)
            {
                var found = this.Controls.Find("chainGroup" + i, true);
                if (found.Length == 0) continue;
                GroupBox group = (GroupBox)found[0];

                int available = group.Width - LABEL_COL - RIGHT_PAD;
                int stride    = Math.Max(MIN_STRIDE, available / 6);

                for (int slot = 1; slot <= 7; slot++)
                {
                    int x = LABEL_COL + (slot - 1) * stride;

                    foreach (string suffix in _slotSuffixesDirect)
                    {
                        var ctrls = group.Controls.Find("in" + slot + "mac" + i + suffix, true);
                        if (ctrls.Length > 0)
                            ctrls[0].Location = new System.Drawing.Point(x, ctrls[0].Location.Y);
                    }
                    foreach (string suffix in _slotSuffixesOffset4)
                    {
                        var ctrls = group.Controls.Find("in" + slot + "mac" + i + suffix, true);
                        if (ctrls.Length > 0)
                            ctrls[0].Location = new System.Drawing.Point(x + 4, ctrls[0].Location.Y);
                    }
                }
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (IsHandleCreated) RelayoutGroups();
        }

        private void onSkillIdChange(object sender, EventArgs e)
        {
            TextBox tb = (TextBox)sender;
            int chainID = Int16.Parse(tb.Parent.Name.Split(new[] { "chainGroup" }, StringSplitOptions.None)[1]);
            string cbName = tb.Name.Split(new[] { "skillid" }, StringSplitOptions.None)[0];
            ChainConfig chainConfig = ProfileSingleton.GetCurrent().MacroSwitch.chainConfigs.Find(c => c.id == chainID);
            if (chainConfig == null) return;
            GetOrCreateMacroKey(chainConfig, cbName).skillId = string.IsNullOrWhiteSpace(tb.Text) ? null : tb.Text.Trim();
            ProfileSingleton.SetConfiguration(ProfileSingleton.GetCurrent().MacroSwitch);
        }

        private void updateUi()
        {
            for (int i = 1; i <= TOTAL_MACRO_LANES; i++)
            {
                UpdatePanelData(i);
            }
        }

        private void configureMacroLanes()
        {
            for (int i = 1; i <= TOTAL_MACRO_LANES; i++)
            {
                initializeLane(i);
            }
        }

        private void initializeLane(int id)
        {
            try
            {
                GroupBox p = (GroupBox)this.Controls.Find("chainGroup" + id, true)[0];
                foreach (Control control in p.Controls)
                {
                    ChainConfig chainConfig = ProfileSingleton.GetCurrent().MacroSwitch.chainConfigs.Find(config => config.id == id);

                    if (chainConfig == null) {
                        chainConfig = new ChainConfig(id, Key.None);
                        ProfileSingleton.GetCurrent().MacroSwitch.chainConfigs.Add(chainConfig);
                    }

                    if (control is TextBox)
                    {
                        TextBox textBox = (TextBox)control;
                        textBox.KeyDown += new System.Windows.Forms.KeyEventHandler(FormUtils.OnKeyDown);
                        textBox.KeyPress += new KeyPressEventHandler(FormUtils.OnKeyPress);
                        textBox.TextChanged += new EventHandler(this.onTextChange);
                    }

                    if (control is NumericUpDown)
                    {
                        NumericUpDown delayInput = (NumericUpDown)control;
                        delayInput.ValueChanged += new System.EventHandler(this.onDelayChange);
                    }


                    if (control is CheckBox)
                    {
                        CheckBox checkInput = (CheckBox)control;
                        checkInput.CheckedChanged += new System.EventHandler(this.onCheckClickChange);
                    }
                }
            }
            catch { }
        }
    }
}
