using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

using CommonBehaviors.Actions;

using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;

namespace Honorbuddy.Quest_Behaviors.SpecificQuests.BloodiedSkies
{
	[CustomBehaviorFileName(@"SpecificQuests\30266-VOEB-BloodiedSkies")]
	public class BloodiedSkies : CustomForcedBehavior
	{
		public BloodiedSkies(Dictionary<string, string> args)
			: base(args) 
		{
			try
			{
				QuestId = 30266; // GetAttributeAsQuestId("QuestId", true, null) ?? 0;
			}
			catch
			{
				Logging.Write("Problem parsing a QuestId in behavior: Bloodied Skies");
			}
		}

		public int QuestId { get; set; }
		public static LocalPlayer me = StyxWoW.Me;
		static public bool InVehicle { get { return Lua.GetReturnVal<int>("if IsPossessBarVisible() or UnitInVehicle('player') or not(GetBonusBarOffset()==0) then return 1 else return 0 end", 0) == 1; } }
		public double angle = 0;
		public double CurentAngle = 0;
		
		public List<WoWUnit> Mantid
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWUnit>()
									.Where(u => (u.Entry == 63973 && u.IsAlive && u.Y > 2330 && u.Z >= 410))
									.OrderBy(u => u.Distance).ToList();
			}
		}

		public bool IsQuestComplete()
		{
			var quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);
			return quest == null || quest.IsCompleted;
		}

		private Composite _root;
		protected override Composite CreateBehavior()
		{
			return _root ?? (_root =
				new PrioritySelector(
					new Decorator(ret => IsQuestComplete(),
						new Sequence(
							new Action(ret => TreeRoot.StatusText = "Finished!"),
							new Action(ret => Lua.DoString("CastPetAction({0})", 12)),
							new WaitContinue(120,
							new Action(delegate
							{
								_isDone = true;
								return RunStatus.Success;
							}))
							)),
					new Decorator(ret => !InVehicle,
						new Action(delegate
						{
							_isDone = true;
							return RunStatus.Success;
						})),
					new Action(ret =>
					{
						if (Mantid.Count == 0) return;
						Mantid[0].Target();

						if (me.CurrentTarget != null && me.CurrentTarget.IsAlive)
						{
							using (StyxWoW.Memory.ReleaseFrame(true))
							{
								WoWMovement.ConstantFace(me.CurrentTarget.Guid);
								angle = ((me.CurrentTarget.Z - me.Z)-2) / (me.CurrentTarget.Location.Distance(me.Location));
								CurentAngle = Lua.GetReturnVal<double>("return VehicleAimGetAngle()", 0);
								if (CurentAngle < angle)
								{
									Lua.DoString(string.Format("VehicleAimIncrement(\"{0}\")", (angle - CurentAngle)));
								}
								if (CurentAngle > angle)
								{
									Lua.DoString(string.Format("VehicleAimDecrement(\"{0}\")", (CurentAngle - angle)));
								}
								Lua.DoString("CastPetAction({0})", 1);
								StyxWoW.Sleep(1500);
							}
						}
					}
			)));
		}

		private bool _isDone;
		public override bool IsDone { get { return _isDone; } }
	}
}

