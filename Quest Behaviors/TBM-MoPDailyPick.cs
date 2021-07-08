using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Action = Styx.TreeSharp.Action;

namespace Styx.Bot.Quest_Behaviors
{
    [CustomBehaviorFileName(@"TBM-MoPDailyPick")]
    public class MoPDailyPick : CustomForcedBehavior
    {
        public MoPDailyPick(Dictionary<string, string> args) : base(args)
        {
            try {  }

            catch (Exception except)
            {
                LogMessage("error", "BEHAVIOR MAINTENANCE PROBLEM: " + except.Message
                                    + "\nFROM HERE:\n"
                                    + except.StackTrace + "\n");
                IsAttributeProblem = true;
            }
        }

        #region Variables

        // Private variables for internal state
        private bool _isBehaviorDone;
        private bool _Init;
        private bool _IsDisposed;
        private Composite _Root;
        #endregion

        #region Dispose
        ~MoPDailyPick() { OnFinished(false); }

        public void OnFinished(bool isExplicitlyInitiatedDispose)
        {
            if (!_IsDisposed)
            {
                // NOTE: we should call any OnFinished() method for any managed or unmanaged
                // resource, if that resource provides a OnFinished() method.

                // Clean up managed resources, if explicit disposal...
                if (isExplicitlyInitiatedDispose) { }  // empty, for now

                BotEvents.OnBotStopped -= BotEvents_OnBotStopped;
                _isBehaviorDone = false;
                _Init = false;

                base.OnFinished();
            }
            _IsDisposed = true;
        }

        public override void OnFinished()
        {
            OnFinished(true);
            GC.SuppressFinalize(this);
        }

        public void BotEvents_OnBotStopped(EventArgs args) { OnFinished(); }
        #endregion

        #region Methods
        private void Init()
        {
            _Init = true;
            BotEvents.OnBotStopped += BotEvents_OnBotStopped;
        }

        #region LoadNextProfile

        private void LoadNextProfile()
        {
            try
            {
                string path = Utilities.AssemblyDirectory + @"\Plugins\BrodiesPluginRevival\BrodiesPluginRevival.dll";
                Assembly testAssembly = Assembly.LoadFile(path);
                Type brodiesMain = testAssembly.GetType("BrodiesPluginRevival.BrodiesMain");
                object bMain = Activator.CreateInstance(brodiesMain);

                brodiesMain.InvokeMember("MoPDailyProfileChange", BindingFlags.InvokeMethod | BindingFlags.Instance | BindingFlags.Public, null, bMain, null);
            }
            catch (Exception e)
            {
                Logging.Write(e.Message);
            }
            _isBehaviorDone = true;
        }

        #endregion
        #endregion

        #region Overrides of CustomForcedBehavior
        protected override Composite CreateBehavior()
        {
            return _Root ?? (_Root =
                new PrioritySelector(context => !_isBehaviorDone,
                    // Initialize the QuestBehavior.
                    new Decorator(context => !_Init,
                        new Action(context => Init())
                    ),

                    new Action(context => LoadNextProfile())
               )
            );
        }

        public override bool IsDone { get { return _isBehaviorDone; } }

        public override void OnStart()
        {
            // This reports problems, and stops BT processing if there was a problem with attributes...
            // We had to defer this action, as the 'profile line number' is not available during the element's
            // constructor call.
            OnStart_HandleAttributeProblem();

            if (!IsDone) { }
        }
        #endregion
    }
}