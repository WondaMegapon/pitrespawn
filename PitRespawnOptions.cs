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
        public static Configurable<bool> ScalingPenalty = new Configurable<bool>(false);
        public static Configurable<int> FallPenalty = new Configurable<int>(1);

        // The UI element storage.
        private UIelement[] PlayerOptions;

        // IT TURNS OUT THIS IS REQUIRED
        // DON'T MAKE MY MISTAKES!
        public PitRespawnOptions()
        {
            // Binding and assigning.
            ScalingPenalty = config.Bind("scalingPenalty", false, (ConfigurableInfo)null);
            FallPenalty = config.Bind("fallPenalty", 1, new ConfigAcceptableRange<int>(0,9));
        }

        // Runs on the initailization of the options menu.
        public override void Initialize()
        {
            // Prepping the basic configuration stuff.
            // Initailizing.
            base.Initialize();
            // Creating our basic tab.
            Tabs = new OpTab[]
            {
                // No need to be fancy.
                new OpTab(this, "Options")
            };

            // Easy storage for the new options.
            PlayerOptions = new UIelement[]
            {
                // Big label. Just because I'm like that :).
                new OpLabel(new Vector2(100f, 530f), new Vector2(400f, 40f), "Pit Respawn Settings", FLabelAlignment.Center, true),

                // Fall Penalty Settings
                new OpRect(new Vector2(30f, 380f), new Vector2(210f, 120f)),

                new OpLabel(new Vector2(40f, 470f), new Vector2(190f, 20f), "Fall Penalty Settings"),
                new OpLabel(new Vector2(40f, 440f), new Vector2(140f, 20f), "Scaling Fall Penalty", FLabelAlignment.Left),
                new OpCheckBox(ScalingPenalty, new Vector2(200f, 435f)) { description = "Allow successive deaths in the same room to deal additional pips of damage." },
                new OpLabel(new Vector2(40f, 400f), new Vector2(140f, 20f), "Fall Penalty", FLabelAlignment.Left),
                new OpDragger(FallPenalty, new Vector2(200f, 395f)) { description = "The amount of food pips that will be deducted when a player falls." },
            };

            // Turning it into actual UI elements.
            this.Tabs[0].AddItems(PlayerOptions);
        }
    }
}
