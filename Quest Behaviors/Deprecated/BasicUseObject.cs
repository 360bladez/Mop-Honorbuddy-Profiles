// Behavior originally contributed by Natfoth.
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

using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion



namespace Honorbuddy.Quest_Behaviors.BasicUseObject
{
	[CustomBehaviorFileName(@"Deprecated\BasicUseObject")]
	[CustomBehaviorFileName(@"BasicUseObject")]  // Deprecated location--do not use
	public class BasicUseObject : CustomForcedBehavior
	{
		public BasicUseObject(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				ObjectId = GetAttributeAsNullable<int>("ObjectId", true, ConstrainAs.MobId, null) ?? 0;
				QuestId = GetAttributeAsNullable<int>("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
				QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
				QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;

				Counter = 1;
				MovedToTarget = false;

				QuestBehaviorBase.DeprecationWarning_Behavior(this, "InteractWith", BuildReplacementArguments());
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

		private List<Tuple<string, string>> BuildReplacementArguments()
		{
			var replacementArgs = new List<Tuple<string, string>>();

			QuestBehaviorBase.BuildReplacementArgs_QuestSpec(replacementArgs, QuestId, QuestRequirementComplete, QuestRequirementInLog);
			QuestBehaviorBase.BuildReplacementArg(replacementArgs, ObjectId, "MobId", 0);
   
			return replacementArgs;
		}
		
		// Attributes provided by caller
		public int ObjectId { get; private set; }
		public int QuestId { get; private set; }
		public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
		public QuestInLogRequirement QuestRequirementInLog { get; private set; }

		// Private variables for internal state
		private bool _isBehaviorDone;
		private bool _isDisposed;
		private List<WoWGameObject> _objectList;
		private Composite _root;

		// Private properties
		private int Counter { get; set; }
		private LocalPlayer Me { get { return (StyxWoW.Me); } }
		private bool MovedToTarget { get; set; }

		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return ("$Id: BasicUseObject.cs 1581 2014-06-27 02:34:30Z Mainhaxor $"); } }
		public override string SubversionRevision { get { return ("$Rev: 1581 $"); } }


		~BasicUseObject()
		{
			Dispose(false);
		}


		public void Dispose(bool isExplicitlyInitiatedDispose)
		{
			if (!_isDisposed)
			{
				// NOTE: we should call any Dispose() method for any managed or unmanaged
				// resource, if that resource provides a Dispose() method.

				// Clean up managed resources, if explicit disposal...
				if (isExplicitlyInitiatedDispose)
				{
					// empty, for now
				}

				// Clean up unmanaged resources (if any) here...
				TreeRoot.GoalText = string.Empty;
				TreeRoot.StatusText = string.Empty;

				// Call parent Dispose() (if it exists) here ...
				base.Dispose();
			}

			_isDisposed = true;
		}


		public void UseGameObjectFunc()
		{
			QBCLog.Info("Using ObjectId({0})", ObjectId);
			_objectList[0].Interact();
			StyxWoW.SleepForLagDuration();
			Counter++;
			StyxWoW.Sleep(6000);
		}


		#region Overrides of CustomForcedBehavior

		protected override Composite CreateBehavior()
		{
			return _root ?? (_root =
				new PrioritySelector(

					new Decorator(ret => Counter > 1,
						new Action(ret => _isBehaviorDone = true)),

						new PrioritySelector(

						   new Decorator(ret => !MovedToTarget,
								new Action(delegate
								{
									MovedToTarget = true;
									return RunStatus.Success;

								})
								),

							new Decorator(ret => StyxWoW.Me.IsMoving,
								new Action(delegate
								{
									WoWMovement.MoveStop();
									StyxWoW.SleepForLagDuration();
								})
								),

							new Decorator(ret => MovedToTarget,
								new Action(delegate
								{
									// CurrentUnit.Interact();

									TreeRoot.GoalText = "BasicUseObject Running";
									TreeRoot.StatusText = "Using Object";

									ObjectManager.Update();

									_objectList = ObjectManager.GetObjectsOfType<WoWGameObject>()
										.Where(u => u.Entry == ObjectId && !u.InUse && !u.IsDisabled)
										.OrderBy(u => u.Distance).ToList();

									if (_objectList.Count >= 1)
									{

										StyxWoW.Sleep(1000);
										UseGameObjectFunc();
									}

									if (Me.Combat)
									{
										return RunStatus.Success;
									}


									if (Counter > 1)
									{
										return RunStatus.Success;
									}
									return RunStatus.Running;
								})
								),

							new Action(ret => StyxWoW.Sleep(1000))
						)
					));
		}


		public override void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}


		public override bool IsDone
		{
			get
			{
				return (_isBehaviorDone     // normal completion
						|| !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete));
			}
		}


		public override void OnStart()
		{
			// This reports problems, and stops BT processing if there was a problem with attributes...
			// We had to defer this action, as the 'profile line number' is not available during the element's
			// constructor call.
			OnStart_HandleAttributeProblem();

			// If the quest is complete, this behavior is already done...
			// So we don't want to falsely inform the user of things that will be skipped.
			if (!IsDone)
			{
				this.UpdateGoalText(QuestId);
			}
		}

		#endregion
	}
}
