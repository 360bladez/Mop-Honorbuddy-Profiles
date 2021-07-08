using System.Collections.Generic;
using System.Linq;

using CommonBehaviors.Actions;

using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;

namespace Honorbuddy.Quest_Behaviors.SpecificQuests.LaosyScouting
{
	[CustomBehaviorFileName(@"SpecificQuests\30264-VOEB-EnemyattheGates")]
	public class EnemyattheGates : CustomForcedBehavior
	{
		public EnemyattheGates(Dictionary<string, string> args)
			: base(args)
		{
			try
			{
				QuestId = 30264; // GetAttributeAsQuestId("QuestId", true, null) ?? 0;
			}
			catch
			{
				Logging.Write("Problem parsing a QuestId in behavior: Enemy at the Gates");
			}
		}
		public int QuestId { get; set; }
		private bool _isBehaviorDone;
		public int MobIdHiveling = 63972;
		public int MobIdWarWagon = 64274;
		public int MobIdCatapult = 64275;
		public int WarSerpent = 65336;
		private Composite _root;
		public WoWPoint Location = new WoWPoint(3048.918, -497.9261, 205.6379);
		public QuestCompleteRequirement questCompleteRequirement = QuestCompleteRequirement.NotComplete;
		public QuestInLogRequirement questInLogRequirement = QuestInLogRequirement.InLog;
		static public bool InVehicle { get { return Lua.GetReturnVal<int>("if IsPossessBarVisible() or UnitInVehicle('player') or not(GetBonusBarOffset()==0) then return 1 else return 0 end", 0) == 1; } }
		private LocalPlayer Me { get { return (StyxWoW.Me); } }

		public override bool IsDone { get { return _isBehaviorDone; } }
		
		public override void OnStart()
		{
			OnStart_HandleAttributeProblem();
			if (!IsDone)
			{
				PlayerQuest Quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);
				TreeRoot.GoalText = Quest != null ? "\"" + Quest.Name + "\"" : "In Progress";
			}
		}

		public List<WoWUnit> Hivelings
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == MobIdHiveling && !u.IsDead && u.Distance < 500).OrderBy(u => u.Distance).ToList();
			}
		}
		public List<WoWUnit> WarWagon
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == MobIdWarWagon && !u.IsDead && u.Distance < 500).OrderBy(u => u.Distance).ToList();
			}
		}
		public List<WoWUnit> Catapult
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == MobIdCatapult && !u.IsDead && u.Distance < 500).OrderBy(u => u.Distance).ToList();
			}
		}
		
		public bool IsQuestComplete()
		{
			var quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);
			return quest == null || quest.IsCompleted;
		}
		private bool IsObjectiveComplete(int objectiveId, uint questId)
		{
			if (Me.QuestLog.GetQuestById(questId) == null)
			{
				return false;
			}
			int returnVal = Lua.GetReturnVal<int>("return GetQuestLogIndexByID(" + questId + ")", 0);
			return
				Lua.GetReturnVal<bool>(
					string.Concat(new object[] { "return GetQuestLogLeaderBoard(", objectiveId, ",", returnVal, ")" }), 2);
		}

		public Composite DoneYet
		{
			get
			{
				return
					new Decorator(ret => IsQuestComplete(), new Action(delegate
					{
						Lua.DoString("CastPetAction(2)");
						Lua.DoString("CastPetAction(3)");
						Lua.DoString("CastPetAction(4)");
						Lua.DoString("CastPetAction(5)");
						Lua.DoString("CastPetAction(6)");
						Lua.DoString("CastPetAction(7)");
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
				return new Decorator(r => !IsObjectiveComplete(2, (uint)QuestId), new Action(r =>
				{
					Lua.DoString("CastPetAction(1)");
					SpellManager.ClickRemoteLocation(Hivelings[0].Location);
				}));
			}
		}
		public Composite KillTwo
		{
			get
			{
				return new Decorator(r => !IsObjectiveComplete(3, (uint)QuestId), new Action(r =>
				{
					Lua.DoString("CastPetAction(1)");
					SpellManager.ClickRemoteLocation(WarWagon[0].Location);
				}));
			}
		}
		public Composite KillThree
		{
			get
			{
				return new Decorator(r => !IsObjectiveComplete(4, (uint)QuestId), new Action(r =>
				{
					Lua.DoString("CastPetAction(1)");
					SpellManager.ClickRemoteLocation(Catapult[0].Location);
				}));
			}
		}
		
		protected override Composite CreateBehavior()
		{
			return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, KillOne, KillTwo, KillThree, new ActionAlwaysSucceed())));
		}
	}
}
