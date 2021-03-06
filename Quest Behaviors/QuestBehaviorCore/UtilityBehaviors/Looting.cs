// Originally contributed by Chinajade.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.

#region Usings
using System;
using System.Collections.Generic;
using System.Linq;
using Bots.Grind;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.POI;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
	public partial class UtilityBehaviorPS
	{
		public class Looting : PrioritySelector
		{
			public Looting(ProvideMovementByDelegate movementByDelegate)
			{
				Contract.Requires(movementByDelegate != null, context => "movementByDelegate != null");

				MovementByDelegate = movementByDelegate;

				Children = CreateChildren();
			}


			// BT contruction-time properties...
			private ProvideMovementByDelegate MovementByDelegate { get; set; }

			// BT visit-time properties...
			private WoWObject CachedLootObject { get; set; }

			// Convenience properties...


			private List<Composite> CreateChildren()
			{
				return new List<Composite>()
				{
					new Decorator(context => LevelBot.BehaviorFlags.HasFlag(BehaviorFlags.Loot) && LootTargeting.LootMobs,
						new PrioritySelector(context => CachedLootObject = Utility.LootableObject(),
							new Decorator(context => (CachedLootObject != null) && (CachedLootObject.Distance > CachedLootObject.InteractRange),
								new UtilityBehaviorPS.MoveTo(
									context => CachedLootObject.Location,
									context => CachedLootObject.Name,
									MovementByDelegate)),
							new Decorator(context => CachedLootObject != null,
								new UtilityBehaviorPS.MoveStop())
						)),
					LevelBot.CreateLootBehavior()
				};
			}
		}
	}


	public partial class UtilityBehaviorPS
	{
		public class WarnIfBagsFull : PrioritySelector
		{
			public WarnIfBagsFull()
			{
				Children = CreateChildren();
			}


			// BT contruction-time properties...

			// BT visit-time properties...


			private List<Composite> CreateChildren()
			{
				return new List<Composite>()
				{
					new CompositeThrottle(context => LootTargeting.LootMobs && (Me.FreeBagSlots <= 0),
						TimeSpan.FromSeconds(10),
						new ActionFail(context =>
						{
							QBCLog.Error("Honorbuddy may not be looting because your bags are full.");
						}))
				};
			}
		}
	}

}