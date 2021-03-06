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
using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
	public partial class UtilityBehaviorSeq
	{
		/// <summary>
		/// <para>Uses item defined by WOWITEMDELEGATE on target defined by SELECTEDTARGETDELEGATE.</para>
		/// <para>Notes:<list type="bullet">
		/// <item><description><para>* It is up to the caller to assure that all preconditions have been met for
		/// using the item (i.e., the target is in range, the item is off cooldown, etc).</para></description></item>
		/// <item><description><para> * If item use was successful, BT is provided with RunStatus.Success;
		/// otherwise, RunStatus.Failure is returned (e.g., item is not ready for use,
		/// item use was interrupted by combat, etc).</para></description></item>
		/// <item><description><para>* It is up to the caller to blacklist the target, or select a new target
		/// after successful item use.</para></description></item>
		/// </list></para>
		/// </summary>
		/// <param name="selectedTargetDelegate"> may NOT be null.  The target provided by the delegate should be viable.</param>
		/// <param name="wowItemDelegate"> may NOT be null.  The item provided by the delegate should be viable, and ready for use.</param>
		/// <returns></returns>
		public class CastSpell : Sequence
		{
			public CastSpell(ProvideIntDelegate spellIdDelegate,
							 ProvideWoWObjectDelegate selectedTargetDelegate,
							 Action<object> actionOnSuccessfulSpellCastDelegate = null)
			{
				Contract.Requires(spellIdDelegate != null, context => "spellDelegate != null");
				Contract.Requires(selectedTargetDelegate != null, context => "selectedTargetDelegate != null");

				ActionOnSuccessfulSpellCastDelegate = actionOnSuccessfulSpellCastDelegate ?? (context => { /*NoOp*/ });
				SpellIdDelegate = spellIdDelegate;
				SelectedTargetDelegate = selectedTargetDelegate;

				Children = CreateChildren();
			}


			// BT contruction-time properties...
			private Action<object> ActionOnSuccessfulSpellCastDelegate { get; set; }
			private ProvideIntDelegate SpellIdDelegate { get; set; }
			private ProvideWoWObjectDelegate SelectedTargetDelegate { get; set; }

			// BT visit-time properties...
			private string CachedName_Spell { get; set; }
			private string CachedName_Target { get; set; }
			private bool IsInterrupted { get; set; }


			private List<Composite> CreateChildren()
			{
				return new List<Composite>()
				{
					// Cache & qualify...
					new Action(context =>
					{
						// Viable target?
						// If target is null, then assume 'self'.
						// NB: Since target may go invalid immediately upon casting the spell,
						// we cache its name for use in subsequent log entries.
						var selectedObject = SelectedTargetDelegate(context) ?? Me;
						if (!Query.IsViable(selectedObject))
						{
							QBCLog.Warning("Target is not viable!");
							return RunStatus.Failure;       
						}
						CachedName_Target = selectedObject.SafeName;

						// Target must be a WoWUnit for us to be able to cast a spell on it...
						var selectedTarget = selectedObject as WoWUnit;
						if (!Query.IsViable(selectedTarget))
						{
							QBCLog.Warning("Target {0} is not a WoWUnit--cannot cast spell on it.", CachedName_Target);
							return RunStatus.Failure;
						}

						// Spell known?
						var spellId = SpellIdDelegate(context);
						WoWSpell selectedSpell = WoWSpell.FromId(spellId);
						if (selectedSpell == null)
						{
							QBCLog.Warning("{0} is not known.", Utility.GetSpellNameFromId(spellId));
							return RunStatus.Failure;
						}
						CachedName_Spell = selectedSpell.Name;

						// Need to be facing target...
						// NB: Not all spells require this, but many do.
						Utility.Target(selectedTarget, true);

						// Wait for spell to become ready...
						if (!SpellManager.CanCast(selectedSpell))
						{
							QBCLog.Warning("{0} is not usable, yet.  (cooldown remaining: {1})",
								CachedName_Spell,
								Utility.PrettyTime(selectedSpell.CooldownTimeLeft));
							return RunStatus.Failure;
						}

						// Notify user of intent...
						var message = string.Format("Attempting cast of '{0}' on '{1}'", CachedName_Spell, CachedName_Target);
						message +=
							selectedTarget.IsDead
							? " (dead)"
							: string.Format(" (health: {0:F1})", selectedTarget.HealthPercent);
						QBCLog.DeveloperInfo(message);

						// Set up 'interrupted use' detection, and cast spell...
						// MAINTAINER'S NOTE: Once these handlers are installed, make sure all possible exit paths from the outer
						// Sequence unhook these handlers.  I.e., if you plan on returning RunStatus.Failure, be sure to call
						// UtilityBehaviorSeq_UseItemOn_HandlersUnhook() first.
						InterruptDetection_Hook();
						IsInterrupted = false;
						SpellManager.Cast(selectedSpell, selectedTarget);
						
						// NB: The target or the spell may not be valid after this point...
						// Some targets will go 'invalid' immediately afer interacting with them.
						// Most of the time this happens, the target is immediately and invisibly replaced with
						// an identical looking target with a different script.
						// We must assume our target and spell is no longer available for use after this point.
						return RunStatus.Success;   // fall through
					}),
					new WaitContinue(Delay.AfterItemUse, context => false, new ActionAlwaysSucceed()),

					// If item use requires a second click on the target (e.g., item has a 'ground target' mechanic)...
					new DecoratorContinue(context => StyxWoW.Me.CurrentPendingCursorSpell != null,
						new Sequence(
							new Action(context =>
							{
								// If target is still viable, click it as destination of spell...
								var selectedTarget = SelectedTargetDelegate(context);
								if (Query.IsViable(selectedTarget))
									{ SpellManager.ClickRemoteLocation(selectedTarget.Location); }
								else
									{ Lua.DoString("SpellStopTargeting()"); }
							}),
							new WaitContinue(Delay.LagDuration,
								context => StyxWoW.Me.CurrentPendingCursorSpell == null,
								new ActionAlwaysSucceed()),
							// If we've leftover spell cursor dangling, clear it...
							// NB: This can happen for "use item on location" type activites where you get interrupted
							// (e.g., a walk-in mob).
							new Action(context =>
							{
								if (StyxWoW.Me.CurrentPendingCursorSpell != null)
									{ Lua.DoString("SpellStopTargeting()"); }
							})
						)),
						
					// Wait for any casting to complete...
					// NB: Some interactions or item usages take time, and the WoWclient models this as spellcasting.
					new WaitContinue(TimeSpan.FromSeconds(15),
						context => !(Me.IsCasting || Me.IsChanneling),
						new ActionAlwaysSucceed()),

					// Were we interrupted in spell casting?
					new Action(context => { InterruptDectection_Unhook(); }),
					new DecoratorContinue(context => IsInterrupted,
						new Sequence(
							new Action(context => { QBCLog.Warning("Cast of {0} interrupted.", CachedName_Spell); }),
							// Give whatever issue encountered a chance to settle...
							// NB: Wait, not WaitContinue--we want the Sequence to fail when delay completes.
							new Wait(TimeSpan.FromMilliseconds(1500), context => false, new ActionAlwaysFail())
						)),
					new Action(context =>
					{
						QBCLog.DeveloperInfo("Cast of '{0}' on '{1}' succeeded.", CachedName_Spell, CachedName_Target);
						ActionOnSuccessfulSpellCastDelegate(context);
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