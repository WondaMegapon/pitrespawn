using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Menu.Remix.MixedUI;
using UnityEngine;

namespace pitrespawn
{
    internal class PitRespawnOptions : OptionInterface
    {
        public static Configurable<bool> ScalingPenalty = new Configurable<bool>(false);
        public static Configurable<int> FallPenalty = new Configurable<int>(1);

        public override void Initialize()
        {
            base.Initialize();
        }
    }
}
