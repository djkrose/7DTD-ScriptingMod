using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using JetBrains.Annotations;
using ScriptingMod.Commands;
using ScriptingMod.Exceptions;
using ScriptingMod.Extensions;
using ScriptingMod.ScriptEngines;
using UnityEngine;

namespace ScriptingMod.Tools
{
    using CommandObjectPair = NonPublic.SdtdConsole.CommandObjectPair;

    internal static class CommandTools
    {
        private static readonly CommandObjectPairComparer _commandObjectPairComparer = new CommandObjectPairComparer();
        private static CommandObjectComparer _commandObjectComparer = new CommandObjectComparer();
        private static FileSystemWatcher _scriptsWatcher;
        private static bool _scriptsChangedRunning;
        private static object _scriptsChangedLock = new object();

        /// <summary>
        /// Dictionary of event name => [NotNull] List of script filePaths
        /// </summary>
        private static Dictionary<string, List<string>> _events = new Dictionary<string, List<string>>();

        public static void InitEvents()
        {
            // A lot of other methods are already calling InvokeScriptEvents(..) directly.
            // Here are just the ones that need to be attached to actual events.
            // See enum ScriptEvents and it's usages for a full list of supported scripting events.

            #region ScriptingMod events

            // Called when a player got kicked due to failed EAC check
            EacTools.PlayerKicked += delegate (ClientInfo clientInfo, GameUtils.KickPlayerData kickPlayerData)
            {
                Log.Debug($"Event \"{typeof(EacTools)}.{nameof(EacTools.PlayerKicked)}\" invoked.");
                InvokeScriptEvents(new { type = ScriptEvents.eacPlayerKicked.ToString(), clientInfo, kickPlayerData });
            };

            // Called when a player successfully passed the EAC check
            EacTools.AuthenticationSuccessful += delegate (ClientInfo clientInfo)
            {
                Log.Debug($"Event \"{typeof(EacTools)}.{nameof(EacTools.AuthenticationSuccessful)}\" invoked.");
                InvokeScriptEvents(new { type = ScriptEvents.eacPlayerAuthenticated.ToString(), clientInfo });
            };

            #endregion

            #region Steam events

            var steam = Steam.Instance ?? throw new NullReferenceException("Steam not ready.");

            // Called first when a player is connecting before any authentication
            steam.PlayerConnectedEv += delegate(NetworkPlayer networkPlayer)
            {
                Log.Debug($"Event \"{typeof(Steam)}.{nameof(Steam.PlayerConnectedEv)}\" invoked.");
                InvokeScriptEvents(new { type = ScriptEvents.steamPlayerConnected.ToString(), networkPlayer });
            };

            // Called first when the server is about to shut down
            // TODO: Maybe remove, doesn't add much value
            steam.ApplicationQuitEv += delegate()
            {
                Log.Debug($"Event \"{typeof(Steam)}.{nameof(Steam.ApplicationQuitEv)}\" invoked.");
                InvokeScriptEvents(new { type = ScriptEvents.steamApplicationQuit.ToString() });
            };

            steam.ConnectedToServerEv += delegate()
            {
                Log.Debug($"Event \"{typeof(Steam)}.{nameof(Steam.ConnectedToServerEv)}\" invoked.");
                InvokeScriptEvents(new { type = ScriptEvents.steamConnectedToServer.ToString() });
            };

            // Called right before the game process ends as last event of shutdown.
            // TODO: maybe remove; not really useful
            steam.DestroyEv += delegate()
            {
                Log.Debug($"Event \"{typeof(Steam)}.{nameof(Steam.DestroyEv)}\" invoked.");
                InvokeScriptEvents(new { type = ScriptEvents.steamDestroy.ToString() });
            };

            // Called after the game has disconnected from Steam servers
            steam.DisconnectedFromServerEv += delegate(NetworkDisconnection reason)
            {
                Log.Debug($"Event \"{typeof(Steam)}.{nameof(Steam.DisconnectedFromServerEv)}\" invoked.");
                InvokeScriptEvents(new { type = ScriptEvents.steamDisconnectedFromServer.ToString(), reason });
            };

            steam.FailedToConnectEv += delegate(NetworkConnectionError error)
            {
                Log.Debug($"Event \"{typeof(Steam)}.{nameof(Steam.FailedToConnectEv)}\" invoked.");
                InvokeScriptEvents(new { type = ScriptEvents.steamFailedToConnect.ToString(), error });
            };

            // Invoked on every tick (too big performance impact for scripting event)
            //steam.UpdateEv += delegate ()
            //{
            //    Log.Debug($"Event \"{typeof(Steam)}.{nameof(Steam.UpdateEv)}\" invoked.");
            //    InvokeScriptEvents(new { type = ScriptEvents.steamUpdate.ToString() });
            //};

            // Invoked on every tick (too big performance impact for scripting event)
            //steam.LateUpdateEv += delegate ()
            //{
            //    Log.Debug($"Event \"{typeof(Steam)}.{nameof(Steam.LateUpdateEv)}\" invoked.");
            //    InvokeScriptEvents(new { type = ScriptEvents.steamLateUpdate.ToString() });
            //};

            // Called after a player disconnected, a chat message was distributed, and all associated game data has been unloaded
            // TODO: Maybe remove; it's similar to "playerDisconnected" and the passed networkPlayer cannot be used on a disconnected client anyway.
            steam.PlayerDisconnectedEv += delegate (NetworkPlayer networkPlayer)
            {
                Log.Debug($"Event \"{typeof(Steam)}.{nameof(Steam.PlayerDisconnectedEv)}\" invoked.");
                InvokeScriptEvents(new { type = ScriptEvents.steamPlayerDisconnected.ToString(), networkPlayer });
            };

            steam.ServerInitializedEv += delegate ()
            {
                Log.Debug($"Event \"{typeof(Steam)}.{nameof(Steam.ServerInitializedEv)}\" invoked.");
                InvokeScriptEvents(new { type = ScriptEvents.steamServerInitialized.ToString() });
            };

            #endregion

            #region UnityEngine.Application events

            // Called when main Unity thread logs an error message
            Application.logMessageReceived += delegate (string condition, string trace, LogType logType)
            {
                Log.Debug($"Event \"{typeof(Application)}.{nameof(Application.logMessageReceived)}\" invoked.");
                InvokeScriptEvents(new { type = ScriptEvents.logMessageReceived.ToString(), condition, trace, logType });
            };

            // Called when ANY Unity thread logs an error message
            Application.logMessageReceivedThreaded += delegate (string condition, string trace, LogType logType)
            {
                Log.Debug($"Event \"{typeof(Application)}.{nameof(Application.logMessageReceivedThreaded)}\" invoked.");
                InvokeScriptEvents(new { type = ScriptEvents.logMessageReceived.ToString(), condition, trace, logType });
            };

            #endregion

            #region GameManager events

            // Only called client-side // TODO: check
            GameManager.Instance.OnLocalPlayerChanged += delegate (EntityPlayerLocal player)
            {
                Log.Debug($"Event \"{typeof(GameManager)}.{nameof(GameManager.OnLocalPlayerChanged)}\" invoked.");
                InvokeScriptEvents(new { type = ScriptEvents.localPlayerChanged.ToString(), player });
            };

            // Called on shutdown when the world becomes null. Not called on startup apparently.
            // TODO: remove because not useful
            GameManager.Instance.OnWorldChanged += delegate (World world_)
            {
                Log.Debug($"Event \"{typeof(GameManager)}.{nameof(GameManager.OnWorldChanged)}\" invoked.");
                InvokeScriptEvents(new { type = ScriptEvents.gameManagerWorldChanged.ToString(), world = world_ });
            }; 

            #endregion

            #region World events

            var world = GameManager.Instance.World ?? throw new NullReferenceException(Resources.ErrorWorldNotReady);

            // Called on shutdown when the chunkCache is cleared; idx remains 0 tho. Not called on startup apparently.
            // TODO: remove because not useful
            world.ChunkClusters.ChunkClusterChangedDelegates += delegate (int chunkClusterIndex)
            {
                Log.Debug($"Event \"{typeof(ChunkClusterList)}.{nameof(ChunkClusterList.ChunkClusterChangedDelegates)}\" invoked.");
                InvokeScriptEvents(new { type = ScriptEvents.chunkClusterChanged.ToString(), chunkClusterIndex });
            };

            var chunkCluster = world.ChunkCache ?? throw new NullReferenceException(Resources.ErrorChunkCacheNotReady);

            // Called when chunks change display status, i.e. either get displayed or stop being displayed.
            // chunkLoaded   -> Called when a chunk is loaded into the game engine because a player needs it. Called frequently - use with care!
            // chunkUnloaded -> Called when a chunk is unloaded from the game engine because it is not used by any player anymore. Called frequently - use with care!
            chunkCluster.OnChunkVisibleDelegates += delegate (long chunkKey, bool displayed)
            {
                Log.Debug($"Event \"{typeof(ChunkCluster)}.{nameof(ChunkCluster.OnChunkVisibleDelegates)}\" invoked. (displayed={displayed}).");
                InvokeScriptEvents(new { type = displayed ? ScriptEvents.chunkLoaded.ToString() : ScriptEvents.chunkUnloaded.ToString(), chunkKey });
            };

            // Called after a chunk was loaded into memory
            chunkCluster.OnChunksFinishedLoadingDelegates += delegate ()
            {
                Log.Debug($"Event \"{typeof(ChunkCluster)}.{nameof(ChunkCluster.OnChunksFinishedLoadingDelegates)}\" invoked.");
                InvokeScriptEvents(new { type = ScriptEvents.chunksFinishedLoading.ToString() });
            };

            // Called when chunks are removed from Unity engine // TODO local only ???
            chunkCluster.OnChunksFinishedDisplayingDelegates += delegate ()
            {
                Log.Debug($"Event \"{typeof(ChunkCluster)}.{nameof(ChunkCluster.OnChunksFinishedDisplayingDelegates)}\" invoked.");
                InvokeScriptEvents(new { type = ScriptEvents.chunksFinishedDisplaying.ToString() });
            };

            // Called when world is loaded
            world.OnWorldChanged += delegate (string worldName)
            {
                Log.Debug($"Event \"{typeof(World)}.{nameof(World.OnWorldChanged)}\" invoked.");
                InvokeScriptEvents(new { type = ScriptEvents.worldChanged.ToString(), worldName });
            };

            // Called when any entity (zombie, item, air drop, player, ...) is spawned in the world, both loaded and newly created
            world.EntityLoadedDelegates += delegate (Entity entity)
            {
                Log.Debug($"Event \"{typeof(World)}.{nameof(World.EntityLoadedDelegates)}\" invoked.");
                InvokeScriptEvents(new { type = ScriptEvents.entityLoaded.ToString(), entity });
            };

            // Called when any entity (zombie, item, air drop, player, ...) disappears from the world, e.g. it got killed, picked up, despawned, logged off, ...
            world.EntityUnloadedDelegates += delegate (Entity entity, EnumRemoveEntityReason reason)
            {
                Log.Debug($"Event \"{typeof(World)}.{nameof(World.EntityUnloadedDelegates)}\" invoked.");
                InvokeScriptEvents(new { type = ScriptEvents.entityUnloaded.ToString(), entity, reason });
            };

            #endregion

            #region QuestEventManager events

            var questEventManager = QuestEventManager.Current ?? throw new NullReferenceException("QuestEventManager not ready.");

            questEventManager.AddItem += delegate (ItemStack stack)
            {
                Log.Debug($"Event \"{typeof(QuestEventManager)}.{nameof(QuestEventManager.AddItem)}\" invoked.");
                // TODO: verify
            };

            questEventManager.ZombieKill += delegate (string zombieName)
            {
                Log.Debug($"Event \"{typeof(QuestEventManager)}.{nameof(QuestEventManager.ZombieKill)}\" invoked.");
                // TODO: verify
            };

            // TODO: Add other quest manager events if necessary

            #endregion

            #region UserProfileManager events

            UserProfileManager userProfileManager = Platform.UserProfiles ?? throw new NullReferenceException("UserProfileManager not ready.");

            userProfileManager.OnUserCancelledSignIn += delegate (UserProfile userProfile)
            {
                Log.Debug($"Event \"{typeof(UserProfileManager)}.{nameof(UserProfileManager.OnUserCancelledSignIn)}\" invoked.");
                // TODO: verify
            };

            userProfileManager.OnUserJoinedGame += delegate (UserProfile userProfile)
            {
                Log.Debug($"Event \"{typeof(UserProfileManager)}.{nameof(UserProfileManager.OnUserJoinedGame)}\" invoked.");
                // TODO: verify
            };

            userProfileManager.OnUserLeftGame += delegate (UserProfile userProfile)
            {
                Log.Debug($"Event \"{typeof(UserProfileManager)}.{nameof(UserProfileManager.OnUserLeftGame)}\" invoked.");
                // TODO: verify
            };

            userProfileManager.OnUserSignInError += delegate (UserProfileManager.ProfileLoginErrorCode errorCode)
            {
                Log.Debug($"Event \"{typeof(UserProfileManager)}.{nameof(UserProfileManager.OnUserSignInError)}\" invoked.");
                // TODO: verify
            };

            #endregion

            // Called when game stats change including EnemyCount and AnimalCount, so it's called frequently. Use with care!
            GameStats.OnChangedDelegates += delegate(EnumGameStats gameState, object newValue)
            {
                Log.Debug($"Event \"{typeof(GameStats)}.{nameof(GameStats.OnChangedDelegates)}\" invoked.");
                InvokeScriptEvents(new { type = ScriptEvents.gameStatsChanged.ToString(), gameState, newValue});
            };

            //UserProfile primaryUser = userProfileManager.PrimaryUser;

            //primaryUser.OnUserJoinedGame += delegate(UserProfile userProfile)
            //{
            //    Log.Debug($"Event \"{typeof(UserProfile)}.{nameof(UserProfile.OnUserJoinedGame)}\" invoked.");
            //    // TODO: verify
            //};

            //primaryUser.OnUserLeftGame += delegate (UserProfile userProfile)
            //{
            //    Log.Debug($"Event \"{typeof(UserProfile)}.{nameof(UserProfile.OnUserLeftGame)}\" invoked.");
            //    // TODO: verify
            //};

            //primaryUser.OnDeviceDetached += delegate (UserProfile userProfile)
            //{
            //    Log.Debug($"Event \"{typeof(UserProfile)}.{nameof(UserProfile.OnDeviceDetached)}\" invoked.");
            //    // TODO: verify
            //};

            CraftingManager.RecipeUnlocked += delegate(string recipeName)
            {
                Log.Debug($"Event \"{typeof(CraftingManager)}.{nameof(CraftingManager.RecipeUnlocked)}\" invoked.");
                // TODO: verify
            };

            // TODO: Test out:
            // - LocalPlayerManager
            // - MapVisitor
            // - QuestJournal (EntityPlayer.QuestJournl)

            // ------- Events not suitable: --------

            // MapObjectManager.ChangedDelegates // only used client-side
            // MasterServerAnnouncer.action_0 // too difficult and not useful to provide
            // ServerListManager.GameServerDetailsEvent // only used client-side
            // MenuItemEntry.ItemClicked // only used client-side
            // LocalPlayerManager.* // only used client-side
            // Inventory.OnToolbeltItemsChangedInternal // only used client-side
            // BaseObjective.ValueChanged // only used client-side

            // TODO: Do something about console.log in event mode which can't work but still exists at the moment

            Log.Out("Subscribed to all relevant game events.");
        }

        public static void InitScripts()
        {
            var scripts = Directory.GetFiles(Constants.ScriptsFolder, "*.*", SearchOption.AllDirectories)
                .Where(s => s.EndsWith(LuaEngine.FileExtension, StringComparison.OrdinalIgnoreCase) ||
                            s.EndsWith(JsEngine.FileExtension, StringComparison.OrdinalIgnoreCase));

            foreach (string script in scripts)
            {
                var filePath = script; // Needed prior C# 5.0 as closure
                var fileName = FileHelper.GetRelativePath(filePath, Constants.ScriptsFolder);

                Log.Debug($"Loading script {fileName} ...");

                try
                {
                    bool scriptUsed = false;
                    var scriptEngine = ScriptEngine.GetInstance(Path.GetExtension(filePath));
                    var metadata = scriptEngine.LoadMetadata(filePath);

                    // Register commands
                    var commandNames = metadata.GetValue("commands", "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (commandNames.Length > 0)
                    {
                        scriptUsed = true;
                        var description       = metadata.GetValue("description", "");
                        var help              = metadata.GetValue("help", null);
                        var defaultPermission = metadata.GetValue("defaultPermission").ToInt() ?? 0;
                        var action            = new DynamicCommandHandler((p, si) => scriptEngine.ExecuteCommand(filePath, p, si));
                        var commandObject     = new DynamicCommand(commandNames, description, help, defaultPermission, action);
                        AddCommand(commandObject);
                        Log.Out($"Registered command{(commandNames.Length == 1 ? "" : "s")} \"{commandNames.Join(" ")}\" from script {fileName}.");
                    }

                    // Register events
                    var eventNames = metadata.GetValue("events", "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (eventNames.Length > 0)
                    {
                        scriptUsed = true;
                        foreach (var eventName in eventNames)
                        {
                            if (!EventExists(eventName))
                            {
                                Log.Warning($"Event \"{eventName}\" in script {fileName} is unknown and will be ignored.");
                                continue;
                            }

                            if (!_events.ContainsKey(eventName))
                                _events[eventName] = new List<string>();
                            _events[eventName].Add(filePath);
                        }
                        Log.Out($"Registered event{(eventNames.Length == 1 ? "" : "s")} \"{eventNames.Join(" ")}\" from script {fileName}.");
                    }

                    if (!scriptUsed)
                    {
                        Log.Out($"Script file {fileName} is ignored because it defines neither command names nor events.");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Could not load command script {fileName}: {ex}");
                }
            }

            SaveChanges();

            Log.Debug("All script commands added.");
        }

        private static bool EventExists(string eventName)
        {
            try
            {
                // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
                Enum.Parse(typeof(ScriptEvents), eventName);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        public static void InitScriptsMonitoring()
        {
            try
            {
                _scriptsWatcher = new FileSystemWatcher(Constants.ScriptsFolder);
                _scriptsWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName;
                _scriptsWatcher.IncludeSubdirectories = true;
                _scriptsWatcher.Changed += ScriptsChanged;
                _scriptsWatcher.Created += ScriptsChanged;
                _scriptsWatcher.Deleted += ScriptsChanged;
                _scriptsWatcher.Renamed += ScriptsChanged;
                _scriptsWatcher.EnableRaisingEvents = true;
                Log.Out("Monitoring of script folder changes activated.");
            }
            catch (Exception ex)
            {
                Log.Error("Could not initialize monitoring of scripting folder. Script file changes will not be detected. - " + ex);
            }
        }

        public static void InvokeScriptEvents([NotNull] object eventArgs)
        {
            var eventName = GetEventNameFromEventArgs(eventArgs);

            TrackInvocation(eventName, eventArgs);

            if (!_events.ContainsKey(eventName))
                return;

            Log.Debug($"Invoking event \"{eventName}\" ...");

            foreach (var filePath in _events[eventName])
            {
                var scriptEngine = ScriptEngine.GetInstance(Path.GetExtension(filePath));
                scriptEngine.ExecuteEvent(filePath, eventArgs);
            }
        }

        /// <summary>
        /// Track when and how this event was invoked first time
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="eventArgs"></param>
        [Conditional("DEBUG")]
        private static void TrackInvocation(string eventName, object eventArgs)
        {
#if DEBUG
            var invocationLog = Environment.NewLine +
                                DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine +
                                Environment.StackTrace + Environment.NewLine +
                                Dumper.Dump(eventArgs, 1).TrimEnd();

            var invokedEvent = PersistentData.Instance.InvokedEvents.FirstOrDefault(ie => ie.EventName == eventName);

            if (invokedEvent == null)
            {
                invokedEvent = new PersistentData.InvokedEvent()
                {
                    EventName = eventName,
                    FirstCall = invocationLog.Indent(8) + Environment.NewLine + new string(' ', 6),
                    LastCalls = new List<string>()
                };
                PersistentData.Instance.InvokedEvents.Add(invokedEvent);
            }

            // Rotate last 10 call logs with newest on top
            if (invokedEvent.LastCalls.Count == 10)
                invokedEvent.LastCalls.RemoveAt(invokedEvent.LastCalls.Count - 1);
            invokedEvent.LastCalls.Insert(0, invocationLog.Indent(10) + Environment.NewLine + new string(' ', 8));

            PersistentData.Instance.SaveOnShutdown();
#endif
        }

        /// <summary>
        /// Extracts the event name from the type property of the given event args object using reflection.
        /// If the property doesn't exist or the eventName is null or empty, an ArgumentException is thrown.
        /// </summary>
        /// <param name="eventArgs">An event args object that contains the event name in the property "type"</param>
        /// <returns>The event name; never null or empty</returns>
        /// <exception cref="ArgumentException">If the property doesn't exist or the event name is null or empty</exception>
        private static string GetEventNameFromEventArgs(object eventArgs)
        {
            const string eventNameProperty = "type";

            var eventNameGetter = eventArgs.GetType().GetProperty(eventNameProperty)?.GetGetMethod();

            if (eventNameGetter == null)
                throw new ArgumentException($"Event object {eventArgs.GetType()} is missing the mandatory \"{eventNameProperty}\" property.");

            if (eventNameGetter.ReturnType != typeof(string))
                throw new ArgumentException($"Property \"{eventNameProperty}\" in event object {eventArgs.GetType()} is of type {eventNameGetter.ReturnType} but should be {typeof(string)}.");

            var eventName = (string) eventNameGetter.Invoke(eventArgs, null);
            if (string.IsNullOrEmpty(eventName))
                throw new ArgumentException($"Property \"{eventNameProperty}\" in event object {eventArgs.GetType()} is null or empty.");

            return eventName;
        }

        /// <summary>
        /// Unload all our dynamic commands from the game
        /// </summary>
        private static void UnloadCommands()
        {
            var commandObjects = SdtdConsole.Instance.GetCommandObjects();
            for (int i = commandObjects.Count - 1; i >= 0; i--)
            {
                if (commandObjects.ElementAt(i) is DynamicCommand)
                    commandObjects.RemoveAt(i);
            }

            var commandObjectPairs = SdtdConsole.Instance.GetCommandObjectPairs();
            for (int i = commandObjectPairs.Count - 1; i >= 0; i--)
            {
                if (commandObjectPairs.ElementAt(i).CommandObject is DynamicCommand)
                    commandObjectPairs.RemoveAt(i);
            }

            // Clear out attached scripts but leave the eventName keys intact for reference of available events
            foreach (var eventName in _events.Keys)
            {
                _events[eventName] = new List<string>();
            }

            SaveChanges();
            Log.Out("Unloaded all scripting commands.");
        }

        private static void ScriptsChanged(object sender, FileSystemEventArgs args)
        {
            // Allow only one simultaneous event and skip all others
            lock (_scriptsChangedLock)
            {
                if (_scriptsChangedRunning)
                    return;
                _scriptsChangedRunning = true;
            }

            ThreadManager.AddSingleTask(info =>
            {
                try
                {
                    Log.Out("Changes in scripts folder detected. Reloading commands ...");

                    // Let all other associated events pass by
                    Thread.Sleep(500);

                    // Reload commands
                    UnloadCommands();
                    InitScripts();
                }
                catch (Exception ex)
                {
                    Log.Error("Error occured while changes in script folder were processed: " + ex);
                }
                finally
                {
                    _scriptsChangedRunning = false;
                }
            });
        }

        ///// <summary>
        ///// Parses the file as command script and tries to create a command object from it.
        ///// </summary>
        ///// <param name="filePath">Full path of the file to parse.</param>
        ///// <returns>The new command object, or null if the script has no command name in metadata and therefore is not a command script.</returns>
        //[CanBeNull]
        //private static DynamicCommand CreateCommandObject(string filePath)
        //{
        //    var scriptEngine     = ScriptEngine.GetInstance(Path.GetExtension(filePath));
        //    var metadata         = scriptEngine.LoadMetadata(filePath);
        //    var commands         = metadata.GetValue("commands", "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        //    var description      = metadata.GetValue("description", "");
        //    var help             = metadata.GetValue("help", null);
        //    int defaultPermision = metadata.GetValue("defaultPermission").ToInt() ?? 0;

        //    // Skip files that have no command name defined and therefore are not commands but helper scripts.
        //    if (commands.Length == 0)
        //        return null;

        //    var action = new DynamicCommandHandler((p, si) => scriptEngine.ExecuteCommand(filePath, p, si));
        //    return new DynamicCommand(commands, description, help, defaultPermision, action);
        //}

        /// <summary>
        /// Registers the given command object with it's command names into the Console.
        /// The command object or command names must not already exist in the console.
        /// To make all command changes persistent, SaveChanges() must be called afterwards.
        /// Adapted from: SdtdConsole.RegisterCommands
        /// </summary>
        /// <param name="commandObject"></param>
        private static void AddCommand(DynamicCommand commandObject)
        {
            if (commandObject == null)
                throw new ArgumentNullException(nameof(commandObject));

            var commands = commandObject.GetCommands();

            if (commands == null || commands.Length == 0 || commands.All(string.IsNullOrEmpty))
                throw new ArgumentException("No command name(s) defined.");

            if (SdtdConsole.Instance.GetCommandObjects().Contains(commandObject))
                throw new ArgumentException($"The command object \"{commands.Join(" ")}\" already exists and cannot be registered twice.");

            foreach (string command in commands)
            {
                if (string.IsNullOrEmpty(command))
                    continue;

                if (CommandExists(command))
                    throw new ArgumentException($"The command \"{command}\" already exists and cannot be registered twice.");

                var commandObjectPair = new CommandObjectPair(command, commandObject);
                AddSortedCommandObjectPair(commandObjectPair);
            }

            AddCommandObjectSorted(commandObject);
        }

        private class CommandObjectComparer : IComparer<IConsoleCommand>
        {
            [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
            public int Compare(IConsoleCommand o1, IConsoleCommand o2)
            {
                return string.Compare(o1.GetCommands()[0], o2.GetCommands()[0], StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Inserts a new CommandObject in the list at the position sorted by the first command name.
        /// See: https://stackoverflow.com/a/12172412/785111
        /// </summary>
        /// <param name="item"></param>
        private static void AddCommandObjectSorted(IConsoleCommand item)
        {
            var commandObjects = SdtdConsole.Instance.GetCommandObjects();
            var index = commandObjects.BinarySearch(item, _commandObjectComparer);
            if (index < 0) index = ~index;
            commandObjects.Insert(index, item);
            //Log.Debug($"Inserted new command object at index {index} of {commandObjects.Count-1}.");
        }

        private class CommandObjectPairComparer : IComparer<CommandObjectPair>
        {
            public int Compare(CommandObjectPair o1, CommandObjectPair o2)
            {
                return string.Compare(o1.Command, o2.Command, StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Inserts a new CommandObjectPair object in the list at the position sorted by the command name
        /// See: https://stackoverflow.com/a/12172412/785111
        /// </summary>
        /// <param name="item">An object of struct type SdtdConsole.OL</param>
        private static void AddSortedCommandObjectPair(CommandObjectPair item)
        {
            var commandObjectPairs = SdtdConsole.Instance.GetCommandObjectPairs();
            var index = Array.BinarySearch(commandObjectPairs.ToArray(), item, _commandObjectPairComparer);
            if (index < 0) index = ~index;
            commandObjectPairs.Insert(index, item);
            //Log.Debug($"Inserted new command object pair at index {index} of {commandObjectPairs.Count-1}.");
        }

        private static void SaveChanges()
        {
            Log.Debug("Updating readonly copy of command list ...");
            SdtdConsole.Instance.SetCommandObjectsReadOnly(new ReadOnlyCollection<IConsoleCommand>(SdtdConsole.Instance.GetCommandObjects()));
            Log.Debug("Saving changes to commands and permissions to disk ...");
            GameManager.Instance.adminTools.Save();
        }

        private static bool CommandExists(string command)
        {
            return SdtdConsole.Instance.GetCommandObjectPairs().Any(pair => command.Equals(pair.Command, StringComparison.OrdinalIgnoreCase));
        }

        public static void HandleCommandException(Exception ex)
        {
            if (ex is FriendlyMessageException)
            {
                Log.Debug(ex.Message);
                SdtdConsole.Instance.Output(ex.Message);
            }
            else
            {
                Log.Exception(ex);
                SdtdConsole.Instance.Output(string.Format(Resources.ErrorDuringCommand, ex.Message));
            }
        }

        /// <summary>
        /// Parses two integer coordinates from the given position in the parameter list.
        /// </summary>
        /// <returns>The vector with the two values in x an z, y is always 0.</returns>
        /// <exception cref="FriendlyMessageException">If the coordinates are no integer values or the list is too short</exception>
        public static Vector3i ParseXZ(List<string> parameters, int fromIndex)
        {
            try
            {
                return new Vector3i(int.Parse(parameters[fromIndex]), 0, int.Parse(parameters[fromIndex + 1]));
            }
            catch (Exception)
            {
                throw new FriendlyMessageException(Resources.ErrorCoordinateNotInteger);
            }
        }

        /// <summary>
        /// Parses three integer coordinates from the given position in the parameter list.
        /// </summary>
        /// <returns>The vector with the three values.</returns>
        /// <exception cref="FriendlyMessageException">If the coordinates are no integer values or the list is too short</exception>
        public static Vector3i ParseXYZ(List<string> parameters, int fromIndex)
        {
            try
            {
                return new Vector3i(int.Parse(parameters[fromIndex]), int.Parse(parameters[fromIndex + 1]), int.Parse(parameters[fromIndex + 2]));
            }
            catch (Exception)
            {
                throw new FriendlyMessageException(Resources.ErrorCoordinateNotInteger);
            }
        }

        /// <summary>
        /// Looks for an option with string value of the format "paramName=lala" in the list of parameters
        /// and if existing returns it. If no such parameter exists, null is returned.
        /// </summary>
        /// <returns>The string of the option value, which can be empty, or null if option does not exist</returns>
        [CanBeNull]
        public static string ParseOption(List<string> parameters, string paramName, bool remove = false)
        {
            var index = parameters.FindIndex(p => p.StartsWith(paramName + "="));
            if (index == -1)
                return null;

            string value = parameters[index].Split(new char[] {'='}, 2)[1];
            if (remove)
                parameters.RemoveAt(index);
            return value;
        }


        /// <summary>
        /// Looks for an option with int value of the format "/paramName=123" in the list of parameters
        /// and if existing parses it to int. If no such parameter exists, null is returned.
        /// </summary>
        /// <returns>The parsed int value, or null if option is missing in parameters</returns>
        /// <exception cref="FriendlyMessageException">If the option exists but cannot be parsed to int</exception>
        [CanBeNull]
        public static int? ParseOptionAsInt(List<string> parameters, string paramName, bool remove = false)
        {
            var value = ParseOption(parameters, paramName, remove);
            if (value == null)
                return null;

            if (!int.TryParse(value, out int result))
                throw new FriendlyMessageException($"The value for parameter {paramName} is not a valid integer.");

            return result;
        }

    }
}
