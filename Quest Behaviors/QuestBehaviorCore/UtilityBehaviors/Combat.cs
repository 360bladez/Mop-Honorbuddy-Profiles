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

using Bots.Grind;

using System;
using System.Collections.Generic;
using System.Linq;

using Styx;
using Styx.CommonBot;
using Styx.CommonBot.POI;
using Styx.CommonBot.Routines;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
	public partial class UtilityBehaviorPS
	{
		/*
		/// <summary>
		/// This behavior quits attacking the mob, once the mob is targeting us.
		/// </summary>
		// 24Feb2013-08:11UTC chinajade
		public class GetMobsAttention : PrioritySelector
		{
			public GetMobsAttention(ProvideWoWUnitDelegate selectedTargetDelegate)
			{
				Contract.Requires(selectedTargetDelegate != null, context => "selectedTargetDelegate != null");

				SelectedTargetDelegate = selectedTargetDelegate;

				Children = CreateChildren();
			}


			// BT contruction-time properties...
			private ProvideWoWUnitDelegate SelectedTargetDelegate { get; set; }

			// BT visit-time properties...
			private WoWUnit SelectedMob { get; set; }

			// Convenience properties...


			private List<Composite> CreateChildren()
			{
				return new List<Composite>()
				{
					new ActionFail(context => { SelectedMob = SelectedTargetDelegate(context); }),
					new Decorator(context => Query.IsViableForFighting(SelectedMob) && !SelectedMob.IsTargetingMeOrPet,
						new PrioritySelector(
							new CompositeThrottle(TimeSpan.FromSeconds(3),
								new ActionFail(context => { TreeRoot.StatusText = string.Format("Getting attention of {0}", SelectedMob.Name); })),
							new UtilityBehaviorPS.SpankMob(SelectedTargetDelegate)))
				};
			}
		}
		 */
	}


	public partial class UtilityBehaviorPS
	{
		// TODO: This behavior needs to be retired when we refactor Combat_Main actions.
		public class HealAndRest : PrioritySelector
		{
			public HealAndRest()
			{
				Children = CreateChildren();
			}


			// BT contruction-time properties...

			// BT visit-time properties...

			// Convenience properties...


			private List<Composite> CreateChildren()
			{
				return new List<Composite>()
				{
					// The NeedHeal and NeedCombatBuffs are part of legacy custom class support
					// and pair with the Heal and CombatBuff virtual methods.  If a legacy custom class is loaded,
					// HonorBuddy automatically wraps calls to Heal and CustomBuffs it in a Decorator checking those for you.
					// So, no need to duplicate that work here.
					// Don't call CR when Rest is disabled in LevelBot.
					new Decorator(context => LevelBot.BehaviorFlags.HasFlag(BehaviorFlags.Rest) && !Me.Combat,
						new PrioritySelector(
							new Decorator(context => RoutineManager.Current.HealBehavior != null,
								RoutineManager.Current.HealBehavior),
							new Decorator(context => RoutineManager.Current.NeedHeal,
								new Action(context => RoutineManager.Current.Heal())),
							new Decorator(context => RoutineManager.Current.RestBehavior != null,
								RoutineManager.Current.RestBehavior),
							new Decorator(context => RoutineManager.Current.NeedRest,
								new Action(context => RoutineManager.Current.Rest()))
						))
				};
			}
		}
	}


	public partial class UtilityBehaviorPS
	{
		// 16May2013-04:52UTC chinajade
		public class MiniCombatRoutine : PrioritySelector
		{
			/// <summary>
			/// This sub-behavior was meant to be used mostly for 'vehicle' encounters.  Normal combat routines
			/// do not work well (or at all) in these situations.  Examples include:<list type="bullet">
			/// <item><description><para> * The 'poles' for "The Lesson of Dry Fur" (http://wowhead.com/quest=29661)
			/// </para></description></item>
			/// <item><description><para> * The 'Great White Plainshawk' for "A Lesson in Bravery" (http://wowhead.com/quest=29918)
			/// </para></description></item>
			/// </list>
			/// </summary>
			public MiniCombatRoutine()
			{
				Children = CreateChildren();
			}


			// BT contruction-time properties...

			// BT visit-time properties...
			WoWUnit SelectedTarget;

			// Convenience properties...


			private List<Composite> CreateChildren()
			{return new List<Composite>()
				{
					new Decorator(context =>
						{
							SelectedTarget = Me.CurrentTarget;

							if (Query.IsViableForFighting(SelectedTarget))
							{
								AutoAttackOn();
								return true;
							}

							AutoAttackOff();
							return false;
						},
						new PrioritySelector(
							// Make certain we are facing the target...
							// NB: Since this behavior is frequently employed while we're "on vehicles"
							// "facing" doesn't always work.  If we are unable to 'face' successfully,
							// we don't want that to stop us from trying our special attaks.  Thus,
							// the ActionFail().
							new Decorator(context => !WoWMovement.ActiveMover.IsSafelyFacing(SelectedTarget, 30),
								new ActionFail(context => { Me.SetFacing(SelectedTarget.Location); })),

							// Class-specific (specialty) attacks...
							new Switch<WoWClass>(context => Me.Class,
								new Action(context =>   // default case
								{
									QBCLog.MaintenanceError("Class({0}) is unhandled", Me.Class);
									TreeRoot.Stop();
								}),

								new SwitchArgument<WoWClass>(WoWClass.DeathKnight,
									new PrioritySelector(
										TryCast(49998)      // Death Strike: http://wowhead.com/spell=49998
									)),

								new SwitchArgument<WoWClass>(WoWClass.Druid,
									new PrioritySelector(
										TryCast(5176, context => !Me.HasAura(768)),      // Wrath: http://wowhead.com/spell=5176
										TryCast(768, context => !Me.HasAura(768)),       // Cat Form: http://wowhead.com/spell=768
										TryCast(1822),      // Rake: http://wowhead.com/spell=1822
										TryCast(22568),     // Ferocious Bite: http://wowhead.com/spell=22568
										TryCast(33917)      // Mangle: http://wowhead.com/spell=33917
									)),

								new SwitchArgument<WoWClass>(WoWClass.Hunter,
									new PrioritySelector(
										TryCast(3044),      // Arcane Shot: http://wowhead.com/spell=3044
										TryCast(56641)      // Steady Shot: http://wowhead.com/spell=56641
									)),

								new SwitchArgument<WoWClass>(WoWClass.Mage,
									new PrioritySelector(
										TryCast(44614),     // Frostfire Bolt: http://wowhead.com/spell=44614
										TryCast(126201),    // Frostbolt: http://wowhead.com/spell=126201
										TryCast(2136)       // Fire Blast: http://wowhead.com/spell=2136
									)),

								new SwitchArgument<WoWClass>(WoWClass.Monk,
									new PrioritySelector(
										TryCast(100780),    // Jab: http://wowhead.com/spell=100780
										TryCast(100787)     // Tiger Palm: http://wowhead.com/spell=100787
									)),

								new SwitchArgument<WoWClass>(WoWClass.Paladin,
									new PrioritySelector(
										TryCast(35395),     // Crusader Strike: http://wowhead.com/spell=35395
										TryCast(20271)      // Judgment: http://wowhead.com/spell=20271
									)),

								new SwitchArgument<WoWClass>(WoWClass.Priest,
									new PrioritySelector(
										TryCast(589, context => !Me.CurrentTarget.HasAura(589)),      // Shadow Word: Pain: http://wowhead.com/spell=589
										TryCast(15407),     // Mind Flay: http://wowhead.com/spell=15407
										TryCast(585)        // Smite: http://wowhead.com/spell=585
									)),

								new SwitchArgument<WoWClass>(WoWClass.Rogue,
									new PrioritySelector(
										TryCast(2098),      // Eviscerate: http://wowhead.com/spell=2098
										TryCast(1752)       // Sinster Strike: http://wowhead.com/spell=1752
									)),

								new SwitchArgument<WoWClass>(WoWClass.Shaman,
									new PrioritySelector(
										TryCast(17364),     // Stormstrike: http://wowhead.com/spell=17364
										TryCast(403),       // Lightning Bolt: http://wowhead.com/spell=403
										TryCast(73899)      // Primal Strike: http://wowhead.com/spell=73899
									)),

								new SwitchArgument<WoWClass>(WoWClass.Warlock,
									new PrioritySelector(
										TryCast(686)        // Shadow Bolt: http://wowhead.com/spell=686
									)),

								new SwitchArgument<WoWClass>(WoWClass.Warrior,
									new PrioritySelector(
										TryCast(78),        // Heroic Strike: http://wowhead.com/spell=78
										TryCast(34428),     // Victory Rush: http://wowhead.com/spell=34428
										TryCast(23922),     // Shield Slam: http://wowhead.com/spell=23922
										TryCast(20243)      // Devastate: http://wowhead.com/spell=20243
									))
							)))
				};
			}


			private void AutoAttackOff()
			{
				if (Me.IsAutoAttacking)
					{ Lua.DoString("StopAttack()"); }
			}


			private void AutoAttackOn()
			{
				if (!Me.IsAutoAttacking)
					{ Lua.DoString("StartAttack()"); }
			}


			private Composite TryCast(int spellId, ProvideBoolDelegate requirements = null)
			{
				requirements = requirements ?? (context => true);
				
				return new Decorator(context => SpellManager.CanCast(spellId) && requirements(context),
					new Action(context =>
					{
						QBCLog.DeveloperInfo("MiniCombatRoutine used {0}", Utility.GetSpellNameFromId(spellId));
						SpellManager.Cast(spellId);
					}));
			}
		}
	}


	public partial class UtilityBehaviorPS
	{
		public class PreferAggrodMob : PrioritySelector
		{
			public PreferAggrodMob()
			{
				Children = CreateChildren();
			}


			// BT contruction-time properties...

			// BT visit-time properties...

			// Convenience properties...


			private List<Composite> CreateChildren()
			{
				return new List<Composite>()
				{
					new Decorator(context => Me.Combat,
						new Action(context =>
						{
							var currentTarget = Me.CurrentTarget;

							// If our current target is aggro'd on us, we're done...
							if (Query.IsViable(currentTarget) && currentTarget.Aggro)
								{ return RunStatus.Failure; }

							// Otherwise, we want to go find a target that is aggro'd on us, if one available...
							var aggrodMob = Targeting.Instance.TargetList.FirstOrDefault(m => m.Aggro);
							if (!Query.IsViable(aggrodMob))
								{ return RunStatus.Failure; }

							Utility.Target(aggrodMob, false, PoiType.Kill);
							return RunStatus.Success;
						}))
				};
			}
		}
	}
}