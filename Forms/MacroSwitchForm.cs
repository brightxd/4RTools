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
            addOptionalControls();
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

                    Control[] op = group.Controls.Find($"{cbName}opt", true);
                    if (op.Length > 0)
                    {
                        CheckBox optInput = (CheckBox)op[0];
                        optInput.CheckedChanged -= this.onOptionalChange;
                        optInput.Checked = hasEntry && chainConfig.macroEntries[cbName].optional;
                        optInput.CheckedChanged += this.onOptionalChange;
                    }
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
            chainConfig.macroEntries[textBox.Name] = new MacroKey(key, decimal.ToInt16(delayInput.Value));
            chainConfig.macroEntries[textBox.Name].cooldownMs = existingCooldown;
            chainConfig.macroEntries[textBox.Name].optional = existingOptional;

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
            chainConfig.macroEntries[cbName].delay = decimal.ToInt16(delayInput.Value);

            ProfileSingleton.SetConfiguration(ProfileSingleton.GetCurrent().MacroSwitch);
        }

        private void onCheckClickChange(object sender, EventArgs e)
        {
            CheckBox checkInput = (CheckBox)sender;
            int chainID = Int16.Parse(checkInput.Parent.Name.Split(new[] { "chainGroup" }, StringSplitOptions.None)[1]);
            ChainConfig chainConfig = ProfileSingleton.GetCurrent().MacroSwitch.chainConfigs.Find(config => config.id == chainID);

            String cbName = checkInput.Name.Split(new[] { "click" }, StringSplitOptions.None)[0];
            chainConfig.macroEntries[cbName].hasClick = checkInput.Checked;
            ProfileSingleton.SetConfiguration(ProfileSingleton.GetCurrent().MacroSwitch);
        }

        private void onCooldownChange(object sender, EventArgs e)
        {
            NumericUpDown cdInput = (NumericUpDown)sender;
            int chainID = Int16.Parse(cdInput.Parent.Name.Split(new[] { "chainGroup" }, StringSplitOptions.None)[1]);
            ChainConfig chainConfig = ProfileSingleton.GetCurrent().MacroSwitch.chainConfigs.Find(config => config.id == chainID);

            String cbName = cdInput.Name.Split(new[] { "cooldown" }, StringSplitOptions.None)[0];
            if (chainConfig.macroEntries.ContainsKey(cbName))
            {
                chainConfig.macroEntries[cbName].cooldownMs = decimal.ToInt32(cdInput.Value);
                ProfileSingleton.SetConfiguration(ProfileSingleton.GetCurrent().MacroSwitch);
            }
        }

        private void onOptionalChange(object sender, EventArgs e)
        {
            CheckBox optInput = (CheckBox)sender;
            int chainID = Int16.Parse(optInput.Parent.Name.Split(new[] { "chainGroup" }, StringSplitOptions.None)[1]);
            ChainConfig chainConfig = ProfileSingleton.GetCurrent().MacroSwitch.chainConfigs.Find(config => config.id == chainID);

            String cbName = optInput.Name.Split(new[] { "opt" }, StringSplitOptions.None)[0];
            if (chainConfig.macroEntries.ContainsKey(cbName))
            {
                chainConfig.macroEntries[cbName].optional = optInput.Checked;
                ProfileSingleton.SetConfiguration(ProfileSingleton.GetCurrent().MacroSwitch);
            }
        }

        private void addOptionalControls()
        {
            const int OPT_ROW_Y = 117;
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
