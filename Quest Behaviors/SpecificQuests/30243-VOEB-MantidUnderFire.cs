//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.
//

#region Summary and Documentation
#endregion


#region Examples
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Linq;

using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.MantidUnderFire
{
	[CustomBehaviorFileName(@"SpecificQuests\30243-VOEB-MantidUnderFire")]
	public class MantidUnderFire : CustomForcedBehavior
	{
		public MantidUnderFire(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				QuestId = 30243;
			}

			catch (Exception except)
			{
				// Maintenance problems occur for a number of reasons.  The primary two are...
				// * Changes were made to the behavior, and boundary conditions weren't properly tested.
				// * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
				// In any case, we pinpoint the source of the problem area here, and hopefully it
				// can be quickly resolved.
				QBCLog.Exception(except);
				IsAttributeProblem = true;
			}
		}
		public int QuestId { get; set; }
		private bool _isBehaviorDone;
		public int MobIdMantid = 63972;
		private Composite _root;

		public override bool IsDone
		{
			get
			{
				return _isBehaviorDone;
			}
		}
		private LocalPlayer Me { get { return (StyxWoW.Me); } }

		public override void OnStart()
		{
			OnStart_HandleAttributeProblem();
			if (!IsDone)
			{
				this.UpdateGoalText(QuestId);
			}
		}

		public List<WoWUnit> MantidDry
		{
			get
			{
				return
					ObjectManager.GetObjectsOfType<WoWUnit>()
					.Where(u => u.Entry == MobIdMantid && !u.IsDead && u.Distance < 150 && !u.HasAura(125475))
					.OrderBy(u => u.Distance)
					.ToList();
			}
		}
		public List<WoWUnit> MantidWet
		{
			get
			{
				return
					ObjectManager.GetObjectsOfType<WoWUnit>()
					.Where(u => u.Entry == MobIdMantid && !u.IsDead && u.Distance < 150 && u.HasAura(125475))
					.OrderBy(u => u.Distance)
					.ToList();
			}
		}

		public Composite DoneYet
		{
			get
			{
				return new Decorator(ret => Me.IsQuestObjectiveComplete(QuestId, 1),
					new Action(delegate
					{
						Lua.DoString("CastPetAction(6)");
						TreeRoot.StatusText = "Finished!";
						_isBehaviorDone = true;
						return RunStatus.Success;
					}));
			}
		}


		public Composite KillOne
		{
			get
			{
				return new Decorator(r => !Me.IsQuestObjectiveComplete(QuestId, 1),
					new PrioritySelector(
						new Decorator(r => MantidWet.FirstOrDefault() != null && Styx.CommonBot.Bars.ActionBar.Active.Buttons[1].CanUse,
							new Action(r =>
							{
								Styx.CommonBot.Bars.ActionBar.Active.Buttons[1].Use();
								StyxWoW.Sleep(1000);
								var targetWet = MantidWet.FirstOrDefault();
								SpellManager.ClickRemoteLocation(targetWet.Location);
								MantidWet.RemoveAll(m => m.Guid == targetWet.Guid);
								StyxWoW.Sleep(2000);
							})),
						new Decorator(r => MantidDry.FirstOrDefault() != null && Styx.CommonBot.Bars.ActionBar.Active.Buttons[0].CanUse,
							new Action(r =>
							{
								Styx.CommonBot.Bars.ActionBar.Active.Buttons[0].Use();
								StyxWoW.Sleep(500);
								var targetDry = MantidDry.FirstOrDefault();
								SpellManager.ClickRemoteLocation(targetDry.Location);
								MantidDry.RemoveAll(m => m.Guid == targetDry.Guid);
								StyxWoW.Sleep(1000);
							})
						)
					)
				);
			}
		}

		
		protected override Composite CreateBehavior()
		{
			return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, KillOne, new ActionAlwaysSucceed())));
		}
	}
}
