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

using CommonBehaviors.Actions;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion

// TODO: Need 'bind at inn' variant
// TODO: Need 'buy from merchant' variant
// TODO: Need 'gossip' variant

namespace Honorbuddy.QuestBehaviorCore
{
	public partial class UtilityBehaviorSeq
	{
		public class Interact : Sequence
		{
			public Interact(ProvideWoWObjectDelegate selectedTargetDelegate,
							Action<object> actionOnSuccessfulItemUseDelegate = null)
			{
				Contract.Requires(selectedTargetDelegate != null, context => "selectedTargetDelegate != null");

				ActionOnSuccessfulInteractDelegate = actionOnSuccessfulItemUseDelegate ?? (context => { /*NoOp*/ });
				SelectedTargetDelegate = selectedTargetDelegate;

				Children = CreateChildren();
			}


			// BT contruction-time properties...
			private Action<object> ActionOnSuccessfulInteractDelegate { get; set; }
			private ProvideWoWObjectDelegate SelectedTargetDelegate { get; set; }

			// BT visit-time properties...
			private bool IsInterrupted { get; set; }
			private string CachedName_SelectedTarget { get; set; }

			// Convenience properties...


			private List<Composite> CreateChildren()
			{
				return new List<Composite>()
				{
					// Interact with the mob...
					new Action(context =>
					{
						// Viable target?
						// NB: Since target may go invalid immediately upon interacting with it,
						// we cache its name for use in subsequent log entries.
						var selectedTarget = SelectedTargetDelegate(context);
						if (!Query.IsViable(selectedTarget))
						{
							QBCLog.Warning("Target is not viable!");
							return RunStatus.Failure;                        
						}
						CachedName_SelectedTarget = selectedTarget.SafeName;

						// Need to be facing target...
						// NB: Not all items require this, but many do.
						Utility.Target(selectedTarget, true);
						
						// Notify user of intent...
						QBCLog.DeveloperInfo("Interacting with '{0}'", CachedName_SelectedTarget);

						// Set up 'interrupted use' detection, and interact...
						// MAINTAINER'S NOTE: Once these handlers are installed, make sure all possible exit paths from the outer
						// Sequence unhook these handlers.  I.e., if you plan on returning RunStatus.Failure, be sure to call
						// UtilityBehaviorSeq_UseItemOn_HandlersUnhook() first.
						InterruptDetection_Hook();
						IsInterrupted = false;
						selectedTarget.Interact();

						// NB: The target may not be valid after this point...
						// Some targets will go 'invalid' immediately afer interacting with them.
						// Most of the time this happens, the target is immediately and invisibly replaced with
						// an identical looking target with a different script.
						// We must assume our target is no longer available for use after this point.
						return RunStatus.Success;   // fall through
					}),
					new WaitContinue(Delay.AfterInteraction, context => false, new ActionAlwaysSucceed()),

					// Wait for any casting to complete...
					// NB: Some interactions or item usages take time, and the WoWclient models this as spellcasting.
					// NB: We can't test for IsCasting or IsChanneling--we must instead look for a valid spell being cast.
					//      There are some quests that require actions where the WoWclient returns 'true' for IsCasting,
					//      but there is no valid spell being cast.  We want the behavior to move on immediately in these
					//      conditions.  An example of such an interaction is removing 'tangler' vines in the Tillers
					//      daily quest area.
					new WaitContinue(TimeSpan.FromSeconds(15),
						context => (Me.CastingSpell == null) && (Me.ChanneledSpell == null),
						new ActionAlwaysSucceed()),

					// Were we interrupted in item use?
					new Action(context => { InterruptDectection_Unhook(); }),
					new DecoratorContinue(context => IsInterrupted,
						new Sequence(
							new Action(context => { QBCLog.DeveloperInfo("Interaction with {0} interrupted.", CachedName_SelectedTarget); }),
							// Give whatever issue encountered a chance to settle...
							// NB: Wait, not WaitContinue--we want the Sequence to fail when delay completes.
							new Wait(TimeSpan.FromMilliseconds(1500), context => false, new ActionAlwaysFail())
						)),
					new Action(context =>
					{
						QBCLog.DeveloperInfo("Interact with '{0}' succeeded.", CachedName_SelectedTarget);
						ActionOnSuccessfulInteractDelegate(context);
					})
				};
			}


			private void HandleInterrupted(object sender, LuaEventArgs args)
			{
				var unitId = args.Args[0].ToString();

				if (unitId == "player")
				{
					// If it was a channeled spell, and still casting

					var spellName = args.Args[1].ToString();
					//var rank = args.Args[2].ToString();
					//var lineId = args.Args[3].ToString();
					var spellId = args.Args[4].ToString();

					QBCLog.DeveloperInfo("\"{0}\"({1}) interrupted via {2} Event.",
						spellName, spellId, args.EventName);
					IsInterrupted = true;
				}
			}


			private void InterruptDetection_Hook()
			{
				Lua.Events.AttachEvent("UNIT_SPELLCAST_FAILED", HandleInterrupted);
				Lua.Events.AttachEvent("UNIT_SPELLCAST_INTERRUPTED", HandleInterrupted);
			}


			private void InterruptDectection_Unhook()
			{
				Lua.Events.DetachEvent("UNIT_SPELLCAST_FAILED", HandleInterrupted);
				Lua.Events.DetachEvent("UNIT_SPELLCAST_INTERRUPTED", HandleInterrupted);
			}
		}
	}
}