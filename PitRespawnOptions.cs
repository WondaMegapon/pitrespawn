using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Menu.Remix.MixedUI;
using UnityEngine;

namespace pitrespawn
{
    internal class PitRespawnOptions : OptionInterface
    {
        // Our settings.
        public static Configurable<bool> PlayerRespawn = new Configurable<bool>(true);
        public static Configurable<bool> CreatureRespawn = new Configurable<bool>(false);

        public static Configurable<bool> ScalingPenalty = new Configurable<bool>(false);
        public static Configurable<bool> SecondChance = new Configurable<bool>(true);
        public static Configurable<int> FallPenalty = new Configurable<int>(1);

        public static Configurable<bool> DropItemsOnRespawn = new Configurable<bool>(true);

        // IT TURNS OUT THIS IS REQUIRED
        // DON'T MAKE MY MISTAKES!
        public PitRespawnOptions()
        {
            // Binding and assigning.
            PlayerRespawn = config.Bind("playerRespawn", true, new ConfigurableInfo("Allows players to respawn when falling into pits.", tags: "Player Respawn"));
            CreatureRespawn = config.Bind("creatureRespawn", false, new ConfigurableInfo("Allows creatures to respawn when falling into pits.", tags: "Creature Respawn"));

            ScalingPenalty = config.Bind("scalingPenalty", false, new ConfigurableInfo("Allow successive deaths in the same cycle to deal additional pips of damage.", tags: "Scaling Penalty"));
            SecondChance = config.Bind("secondChance", true, new ConfigurableInfo("The player will still respawn if they have less food than the penalty.", tags: "Second Chance"));
            FallPenalty = config.Bind("fallPenalty", 1, new ConfigurableInfo("The amount of food pips that will be deducted when a player falls.", new ConfigAcceptableRange<int>(0, 9), tags: "Fall Penalty"));

            DropItemsOnRespawn = config.Bind("dropItemsOnRespawn", true, new ConfigurableInfo("Respawning causes the player to drop their items after respawning.", tags: "Drop Items On Respawn"));
        }

        // Runs on the initailization of the options menu.
        public override void Initialize()
        {
            // Prepping the basic configuration stuff.
            // Initailizing.
            base.Initialize();
            // Creating our basic tab and index (I'm just a naughty lil' gamer stealing all the code she can~).
            Tabs = new OpTab[]
            {
                // No need to be fancy.
                new OpTab(this, "Options")
            };
            InitializeMarginAndPos();

            // The initalization that the original mod did.
            

            // A quicky title. :>
            AddNewLine();
            AddTextLabel("Pit Respawn", bigText: true);
            DrawTextLabels(ref Tabs[0]);

            // And subtitle.
            AddNewLine(0.5f);
            AddTextLabel("Version " + PitRespawn.version, FLabelAlignment.Left);
            AddTextLabel("by " + PitRespawn.author, FLabelAlignment.Right);
            DrawTextLabels(ref Tabs[0]);

            // And getting into the main meat of things. Opening the main settings box.
            AddNewLine();
            AddBox();

            // Dropping our variables on the floor.
            // Respawn entity settings.
            AddCheckBox(PlayerRespawn, (string)PlayerRespawn.info.Tags[0]);
            AddCheckBox(CreatureRespawn, (string)CreatureRespawn.info.Tags[0]);
            DrawCheckBoxes(ref Tabs[0]);
            AddNewLine(0.5f);

            // Fall penalty dragger.
            AddDragger(FallPenalty, (string)FallPenalty.info.Tags[0]);
            DrawDragger(ref Tabs[0]);

            AddNewLine(0.5f);

            // Scaling and second chance boxes.
            AddCheckBox(ScalingPenalty, (string)ScalingPenalty.info.Tags[0]);
            AddCheckBox(SecondChance, (string)SecondChance.info.Tags[0]);
            DrawCheckBoxes(ref Tabs[0]);

            AddNewLine(0.5f);

            // More settings!
            AddCheckBox(DropItemsOnRespawn, (string)DropItemsOnRespawn.info.Tags[0]);
            DrawCheckBoxes(ref Tabs[0]);

            // Closing the settings box.
            DrawBox(ref Tabs[0]);
        }

        //
        // Code stolen from SchuhBaum. :O
        //
        //

        //
        // parameters
        //

        private readonly float spacing = 20f;
        private readonly float fontHeight = 20f;
        private readonly int numberOfCheckboxes = 3;
        private readonly float checkBoxSize = 24f;
        private float CheckBoxWithSpacing => checkBoxSize + 0.25f * spacing;

        //
        // variables
        //

        private Vector2 marginX = new();
        private Vector2 pos = new();

        private readonly List<float> boxEndPositions = new();

        private readonly List<Configurable<bool>> checkBoxConfigurables = new();
        private readonly List<OpLabel> checkBoxesTextLabels = new();

        private readonly List<Configurable<string>> comboBoxConfigurables = new();
        private readonly List<List<ListItem>> comboBoxLists = new();
        private readonly List<bool> comboBoxAllowEmpty = new();
        private readonly List<OpLabel> comboBoxesTextLabels = new();

        private readonly List<Configurable<int>> sliderConfigurables = new();
        private readonly List<string> sliderMainTextLabels = new();
        private readonly List<OpLabel> sliderTextLabelsLeft = new();
        private readonly List<OpLabel> sliderTextLabelsRight = new();

        private readonly List<OpLabel> textLabels = new();

        private readonly List<Configurable<int>> draggerConfigurables = new();
        private readonly List<OpLabel> draggerTextLabels = new();

        //
        // main
        //

        private void InitializeMarginAndPos()
        {
            marginX = new Vector2(50f, 550f);
            pos = new Vector2(50f, 600f);
        }

        private void AddNewLine(float spacingModifier = 1f)
        {
            pos.x = marginX.x; // left margin
            pos.y -= spacingModifier * spacing;
        }

        private void AddBox()
        {
            marginX += new Vector2(spacing, -spacing);
            boxEndPositions.Add(pos.y); // end position > start position
            AddNewLine();
        }

        private void DrawBox(ref OpTab tab)
        {
            marginX += new Vector2(-spacing, spacing);
            AddNewLine();

            float boxWidth = marginX.y - marginX.x;
            int lastIndex = boxEndPositions.Count - 1;

            tab.AddItems(new OpRect(pos, new Vector2(boxWidth, boxEndPositions[lastIndex] - pos.y)));
            boxEndPositions.RemoveAt(lastIndex);
        }

        private void AddCheckBox(Configurable<bool> configurable, string text)
        {
            checkBoxConfigurables.Add(configurable);
            checkBoxesTextLabels.Add(new OpLabel(new Vector2(), new Vector2(), text, FLabelAlignment.Left));
        }

        private void DrawCheckBoxes(ref OpTab tab) // changes pos.y but not pos.x
        {
            if (checkBoxConfigurables.Count != checkBoxesTextLabels.Count) return;

            float width = marginX.y - marginX.x;
            float elementWidth = (width - (numberOfCheckboxes - 1) * 0.5f * spacing) / numberOfCheckboxes;
            pos.y -= checkBoxSize;
            float _posX = pos.x;

            for (int checkBoxIndex = 0; checkBoxIndex < checkBoxConfigurables.Count; ++checkBoxIndex)
            {
                Configurable<bool> configurable = checkBoxConfigurables[checkBoxIndex];
                OpCheckBox checkBox = new(configurable, new Vector2(_posX, pos.y))
                {
                    description = configurable.info?.description ?? ""
                };
                tab.AddItems(checkBox);
                _posX += CheckBoxWithSpacing;

                OpLabel checkBoxLabel = checkBoxesTextLabels[checkBoxIndex];
                checkBoxLabel.SetPos(new Vector2(_posX, pos.y + 2f));
                checkBoxLabel.size = new Vector2(elementWidth - CheckBoxWithSpacing, fontHeight);
                tab.AddItems(checkBoxLabel);

                if (checkBoxIndex < checkBoxConfigurables.Count - 1)
                {
                    if ((checkBoxIndex + 1) % numberOfCheckboxes == 0)
                    {
                        AddNewLine();
                        pos.y -= checkBoxSize;
                        _posX = pos.x;
                    }
                    else
                    {
                        _posX += elementWidth - CheckBoxWithSpacing + 0.5f * spacing;
                    }
                }
            }

            checkBoxConfigurables.Clear();
            checkBoxesTextLabels.Clear();
        }

        private void AddComboBox(Configurable<string> configurable, List<ListItem> list, string text, bool allowEmpty = false)
        {
            OpLabel opLabel = new(new Vector2(), new Vector2(0.0f, fontHeight), text, FLabelAlignment.Center, false);
            comboBoxesTextLabels.Add(opLabel);
            comboBoxConfigurables.Add(configurable);
            comboBoxLists.Add(list);
            comboBoxAllowEmpty.Add(allowEmpty);
        }

        private void DrawComboBoxes(ref OpTab tab)
        {
            if (comboBoxConfigurables.Count != comboBoxesTextLabels.Count) return;
            if (comboBoxConfigurables.Count != comboBoxLists.Count) return;
            if (comboBoxConfigurables.Count != comboBoxAllowEmpty.Count) return;

            float offsetX = (marginX.y - marginX.x) * 0.1f;
            float width = (marginX.y - marginX.x) * 0.4f;

            for (int comboBoxIndex = 0; comboBoxIndex < comboBoxConfigurables.Count; ++comboBoxIndex)
            {
                AddNewLine(1.25f);
                pos.x += offsetX;

                OpLabel opLabel = comboBoxesTextLabels[comboBoxIndex];
                opLabel.SetPos(pos);
                opLabel.size += new Vector2(width, 2f); // size.y is already set
                pos.x += width;

                Configurable<string> configurable = comboBoxConfigurables[comboBoxIndex];
                OpComboBox comboBox = new(configurable, pos, width, comboBoxLists[comboBoxIndex])
                {
                    allowEmpty = comboBoxAllowEmpty[comboBoxIndex],
                    description = configurable.info?.description ?? ""
                };
                tab.AddItems(opLabel, comboBox);

                // don't add a new line on the last element
                if (comboBoxIndex < comboBoxConfigurables.Count - 1)
                {
                    AddNewLine();
                    pos.x = marginX.x;
                }
            }

            comboBoxesTextLabels.Clear();
            comboBoxConfigurables.Clear();
            comboBoxLists.Clear();
            comboBoxAllowEmpty.Clear();
        }

        private void AddSlider(Configurable<int> configurable, string text, string sliderTextLeft = "", string sliderTextRight = "")
        {
            sliderConfigurables.Add(configurable);
            sliderMainTextLabels.Add(text);
            sliderTextLabelsLeft.Add(new OpLabel(new Vector2(), new Vector2(), sliderTextLeft, alignment: FLabelAlignment.Right)); // set pos and size when drawing
            sliderTextLabelsRight.Add(new OpLabel(new Vector2(), new Vector2(), sliderTextRight, alignment: FLabelAlignment.Left));
        }

        private void DrawSliders(ref OpTab tab)
        {
            if (sliderConfigurables.Count != sliderMainTextLabels.Count) return;
            if (sliderConfigurables.Count != sliderTextLabelsLeft.Count) return;
            if (sliderConfigurables.Count != sliderTextLabelsRight.Count) return;

            float width = marginX.y - marginX.x;
            float sliderCenter = marginX.x + 0.5f * width;
            float sliderLabelSizeX = 0.2f * width;
            float sliderSizeX = width - 2f * sliderLabelSizeX - spacing;

            for (int sliderIndex = 0; sliderIndex < sliderConfigurables.Count; ++sliderIndex)
            {
                AddNewLine(2f);

                OpLabel opLabel = sliderTextLabelsLeft[sliderIndex];
                opLabel.SetPos(new Vector2(marginX.x, pos.y + 5f));
                opLabel.size = new Vector2(sliderLabelSizeX, fontHeight);
                tab.AddItems(opLabel);

                Configurable<int> configurable = sliderConfigurables[sliderIndex];
                OpSlider slider = new(configurable, new Vector2(sliderCenter - 0.5f * sliderSizeX, pos.y), (int)sliderSizeX)
                {
                    size = new Vector2(sliderSizeX, fontHeight),
                    description = configurable.info?.description ?? ""
                };
                tab.AddItems(slider);

                opLabel = sliderTextLabelsRight[sliderIndex];
                opLabel.SetPos(new Vector2(sliderCenter + 0.5f * sliderSizeX + 0.5f * spacing, pos.y + 5f));
                opLabel.size = new Vector2(sliderLabelSizeX, fontHeight);
                tab.AddItems(opLabel);

                AddTextLabel(sliderMainTextLabels[sliderIndex]);
                DrawTextLabels(ref tab);

                if (sliderIndex < sliderConfigurables.Count - 1)
                {
                    AddNewLine();
                }
            }

            sliderConfigurables.Clear();
            sliderMainTextLabels.Clear();
            sliderTextLabelsLeft.Clear();
            sliderTextLabelsRight.Clear();
        }

        private void AddTextLabel(string text, FLabelAlignment alignment = FLabelAlignment.Center, bool bigText = false)
        {
            float textHeight = (bigText ? 2f : 1f) * fontHeight;
            if (textLabels.Count == 0)
            {
                pos.y -= textHeight;
            }

            OpLabel textLabel = new(new Vector2(), new Vector2(20f, textHeight), text, alignment, bigText) // minimal size.x = 20f
            {
                autoWrap = true
            };
            textLabels.Add(textLabel);
        }

        private void DrawTextLabels(ref OpTab tab)
        {
            if (textLabels.Count == 0)
            {
                return;
            }

            float width = (marginX.y - marginX.x) / textLabels.Count;
            foreach (OpLabel textLabel in textLabels)
            {
                textLabel.SetPos(pos);
                textLabel.size += new Vector2(width - 20f, 0.0f);
                tab.AddItems(textLabel);
                pos.x += width;
            }

            pos.x = marginX.x;
            textLabels.Clear();
        }

        // I had to spledge this one in. I like draggers. :>
        private void AddDragger(Configurable<int> configurable, string text)
        {
            draggerConfigurables.Add(configurable);
            draggerTextLabels.Add(new OpLabel(new Vector2(), new Vector2(), text, FLabelAlignment.Left));
        }

        private void DrawDragger(ref OpTab tab) // changes pos.y but not pos.x
        {
            if (draggerConfigurables.Count != draggerTextLabels.Count) return;

            float width = marginX.y - marginX.x;
            float elementWidth = (width - (numberOfCheckboxes - 1) * 0.5f * spacing) / numberOfCheckboxes;
            pos.y -= checkBoxSize;
            float _posX = pos.x;

            for (int draggerIndex = 0; draggerIndex < draggerConfigurables.Count; ++draggerIndex)
            {
                Configurable<int> configurable = draggerConfigurables[draggerIndex];
                OpDragger checkBox = new(configurable, new Vector2(_posX, pos.y))
                {
                    description = configurable.info?.description ?? ""
                };
                tab.AddItems(checkBox);
                _posX += CheckBoxWithSpacing;

                OpLabel draggerTextLabel = draggerTextLabels[draggerIndex];
                draggerTextLabel.SetPos(new Vector2(_posX, pos.y + 2f));
                draggerTextLabel.size = new Vector2(elementWidth - CheckBoxWithSpacing, fontHeight);
                tab.AddItems(draggerTextLabel);

                if (draggerIndex < draggerConfigurables.Count - 1)
                {
                    if ((draggerIndex + 1) % numberOfCheckboxes == 0)
                    {
                        AddNewLine();
                        pos.y -= checkBoxSize;
                        _posX = pos.x;
                    }
                    else
                    {
                        _posX += elementWidth - CheckBoxWithSpacing + 0.5f * spacing;
                    }
                }
            }

            draggerConfigurables.Clear();
            draggerTextLabels.Clear();
        }
    }
}
