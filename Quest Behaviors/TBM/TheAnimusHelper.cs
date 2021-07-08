using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;

namespace Styx.Bot.Quest_Behaviors {
    [CustomBehaviorFileName(@"TALoader")]
    public class TheAnimusHelper : CustomForcedBehavior {
        public TheAnimusHelper(Dictionary<string, string> args)
            : base(args) {
            try { ProfileName = GetAttributeAs("ProfileName", false, ConstrainAs.StringNonEmpty, null) ?? ""; }

            catch (Exception except) {
                LogMessage("error", "BEHAVIOR MAINTENANCE PROBLEM: " + except.Message
                                    + "\nFROM HERE:\n"
                                    + except.StackTrace + "\n");
                IsAttributeProblem = true;
            }
        }

        #region Variables
        public string ProfileName { get; private set; }

        // Private variables for internal state
        private bool _isBehaviorDone;
        private bool _Init;
        private bool _IsDisposed;
        private Composite _Root;
        #endregion

        #region OnFinished
        ~TheAnimusHelper() { OnFinished(false); }

        public void OnFinished(bool isExplicitlyInitiatedDispose) {
            if (!_IsDisposed) {
                // NOTE: we should call any OnFinished() method for any managed or unmanaged
                // resource, if that resource provides a OnFinished() method.

                // Clean up managed resources, if explicit disposal...
                if (isExplicitlyInitiatedDispose) { }  // empty, for now

                // Clean up unmanaged resources (if any) here...
                BotEvents.OnBotStopped -= BotEvents_OnBotStopped;
                // Disabled until I can find out a safer way to to it.
                // Chat.Addon -= ChatAddon;
                _isBehaviorDone = false;
                _Init = false;

                // Call parent OnFinished() (if it exists) here ...
                base.OnFinished();
            }
            _IsDisposed = true;
        }

        public override void OnFinished() {
            OnFinished(true);
            GC.SuppressFinalize(this);
        }

        public void BotEvents_OnBotStopped(EventArgs args) { OnFinished(); }
        #endregion

        #region Methods
        #region Init
        private void Init() {
            _Init = true;
            BotEvents.OnBotStopped += BotEvents_OnBotStopped;
        }
        #endregion

        #region LoadNewProfile

        private Type _plugin;
        private MethodInfo _method;
        private void LoadNewProfile(string _profile)
        {
            _plugin = _plugin ??
                AppDomain.CurrentDomain.GetAssemblies()
                    .Select(currentassembly => currentassembly.GetType("TheAnimus.TheAnimus", false, false))
                    .FirstOrDefault(t => t != null);
            if (_plugin == null)
            {
                Logging.Write("The Animus is not installed! Cannot load profile: " + _profile);
                return;
            }
            _method = _method ?? _plugin.GetMethod("LoadProfileByName", BindingFlags.Static | BindingFlags.Public);
            _method.Invoke(null, new []{_profile});
            
            _isBehaviorDone = true;
        }

        #endregion
        #endregion

        #region Overrides of CustomForcedBehavior
        protected override Composite CreateBehavior() {
            return _Root ?? (_Root =
                new PrioritySelector(context => !_isBehaviorDone,
                    #region Initialize
                    // Initialize the QuestBehavior.
                    new Decorator(context => !_Init,
                        new Action(context => Init())
                    ),
                    #endregion

                    #region ProfileName
                    // Check that ProfileName is provided in the call and then call the method.
                    new Decorator(context => (ProfileName != ""),
                        new Action(context => LoadNewProfile(ProfileName))
                    )
                    #endregion
                )
            );
        }

        public override bool IsDone { get { return _isBehaviorDone; } }

        public override void OnStart() {
            // This reports problems, and stops BT processing if there was a problem with attributes...
            // We had to defer this action, as the 'profile line number' is not available during the element's
            // constructor call.
            OnStart_HandleAttributeProblem();

            if (!IsDone) { }
        }
        #endregion
    }
}
