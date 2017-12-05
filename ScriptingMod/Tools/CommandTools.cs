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
        /// Array of (int)eventType => List of script filePaths
        /// Elements that are null mean that there is no script event attached to this event type.
        /// Using an array may look "unclean" but it's much faster than Dictionary.
        /// </summary>
        private static List<string>[] _events = new List<string>[(int)Enum.GetValues(typeof(ScriptEvent)).Cast<ScriptEvent>().Max() + 1];

        /// <summary>
        /// Subscribes to additional scripting events that are not called directly;
        /// MUST be called in GameStartDone or later because World is used
        /// </summary>
        public static void InitEvents()
        {
            // A lot of other methods are already calling InvokeScriptEvents(..) directly.
            // Here are just the ones that need to be attached to actual events.
            // See enum ScriptEvents and it's usages for a full list of supported scripting events.

            // Called when a player got kicked due to failed EAC check
            EacTools.PlayerKicked += delegate (ClientInfo clientInfo, GameUtils.KickPlayerData kickPlayerData)
            {
                InvokeScriptEvents(new EacPlayerKickedEventArgs(ScriptEvent.eacPlayerKicked, clientInfo, kickPlayerData));
            };

            // Called when a player successfully passed the EAC check
            EacTools.AuthenticationSuccessful += delegate (ClientInfo clientInfo)
            {
                InvokeScriptEvents(new EacPlayerAuthenticatedEventArgs(ScriptEvent.eacPlayerAuthenticated, clientInfo));
            };

            // Called when the server was registered with Steam and announced to the master servers (also done for non-public dedicated servers)
            Steam.Masterserver.Server.AddEventServerRegistered(delegate()
            {
                InvokeScriptEvents(new ServerRegisteredEventArgs(ScriptEvent.serverRegistered, Steam.Masterserver.Server));
            });

            // Called when ANY Unity thread logs an error message, incl. the main thread
            Application.logMessageReceivedThreaded += delegate (string condition, string trace, LogType logType)
            {
                InvokeScriptEvents(new LogMessageReceivedEventArgs(ScriptEvent.logMessageReceived, condition, trace, logType));
            };

            var world = GameManager.Instance.World ?? throw new NullReferenceException(Resources.ErrorWorldNotReady);

            // Called when any entity (zombie, item, air drop, player, ...) is spawned in the world, both loaded and newly created
            world.EntityLoadedDelegates += delegate (Entity entity)
            {
                InvokeScriptEvents(new EntityLoadedEventArgs(ScriptEvent.entityLoaded, entity));
            };

            // Called when any entity (zombie, item, air drop, player, ...) disappears from the world, e.g. it got killed, picked up, despawned, logged off, ...
            world.EntityUnloadedDelegates += delegate (Entity entity, EnumRemoveEntityReason reason)
            {
                InvokeScriptEvents(new EntityUnloadedEventArgs(ScriptEvent.entityUnloaded, entity, reason));
            };

            // Called when chunks change display status, i.e. either get displayed or stop being displayed.
            // chunkLoaded   -> Called when a chunk is loaded into the game engine because a player needs it. Called frequently - use with care!
            // chunkUnloaded -> Called when a chunk is unloaded from the game engine because it is not used by any player anymore. Called frequently - use with care!
            world.ChunkCache.OnChunkVisibleDelegates += delegate (long chunkKey, bool displayed)
            {
                InvokeScriptEvents(new ChunkLoadedUnloadedEventArgs(displayed ? ScriptEvent.chunkLoaded : ScriptEvent.chunkUnloaded, chunkKey));
            };

            // Called when game stats change including EnemyCount and AnimalCount, so it's called frequently. Use with care!
            GameStats.OnChangedDelegates += delegate(EnumGameStats gameState, object newValue)
            {
                InvokeScriptEvents(new GameStatsChangedEventArgs(ScriptEvent.gameStatsChanged, gameState, newValue));
            };

            #region Event notes

            // ------------ Events intentionally removed -----------
            // - Steam.Instance.PlayerConnectedEv
            //   Called first when a player is connecting before any authentication
            //   Removed because Api.PlayerLogin is also called before authentication and also contains clientInfo.networkPlayer
            // - Steam.Instance.ApplicationQuitEv
            //   Called first when the server is about to shut down
            //   Removed because it doesn't add much value
            // - Steam.Instance.DestroyEv
            //   Called right before the game process ends as last event of shutdown
            //   Removed because it doesn't add much value
            // - Steam.Instance.DisconnectedFromServerEv
            //   Called after the game has disconnected from Steam servers and shuts down
            //   Removed because it doesn't add much value
            // - Steam.Instance.UpdateEv
            //   Invoked on every tick
            //   Removed because too big performance impact for scripting event
            // - Steam.Instance.LateUpdateEv
            //   Invoked on every tick
            //   Removed because too big performance impact for scripting event
            // - Steam.Instance.PlayerDisconnectedEv
            //   Called after a player disconnected, a chat message was distributed, and all associated game data has been unloaded
            //   Removed because it's similar to "playerDisconnected" and the passed networkPlayer cannot be used on a disconnected client anyway
            // - Application.logMessageReceived
            //   Called when main Unity thread logs an error message
            //   Removed because it is included in logMessageReceivedThreaded
            // - GameManager.Instance.OnWorldChanged
            //   Called on shutdown when the world becomes null. Not called on startup apparently.
            //   Removed because not useful
            // - GameManager.Instance.World.ChunkClusters.ChunkClusterChangedDelegates
            //   Called on shutdown when the chunkCache is cleared; idx remains 0 tho. Not called on startup apparently.
            //   Removed because not useful

            // --------- Events never invoked on dedicated server ----------
            // - Steam.ConnectedToServerEv
            // - Steam.FailedToConnectEv
            // - Steam.ServerInitializedEv
            // - GameManager.Instance.OnLocalPlayerChanged
            // - World.OnWorldChanged
            // - ChunkCluster.OnChunksFinishedDisplayingDelegates
            // - ChunkCluster.OnChunksFinishedLoadingDelegates
            // - MapObjectManager.ChangedDelegates
            // - ServerListManager.GameServerDetailsEvent
            // - MenuItemEntry.ItemClicked
            // - LocalPlayerManager.*
            // - Inventory.OnToolbeltItemsChangedInternal
            // - BaseObjective.ValueChanged
            // - UserProfile.*
            // - CraftingManager.RecipeUnlocked
            // - QuestJournal.* (from EntityPlayer.QuestJournal)
            // - QuestEventManager.*
            // - UserProfileManager.*

            // -------- TODO: Events to explore further --------
            // - MapVisitor - needs patching to attach to always newly created object; use-case questionable
            // - AIWanderingHordeSpawner.HordeArrivedDelegate hordeArrivedDelegate_0
            // - Entity.* for each zombie/player entity

            // ----------- TODO: More event ideas --------------
            // - Geofencing...trigger event when a player or zombie gets into a predefined area.
            // - Trigger server events on quest progress/completion. - So server admins could award questing further, or even unlock account features like forum access on quest completions.
            // - Event for Explosions (TNT, dynamite, fuel barrel)
            // - Event for large collapses (say more than 50 blocks)
            // - Event for destruction of a car(e.g.to spawn a new car somewhere else)
            // - Event for idling more than X minutes
            // - Event for blacing bed
            // - Event for placing LCB
            // - Event on zombie/entity proximity (triggered when a player gets or leaves withing reach of X meters of a zombie) [Xyth]
            // - Exploring of new land
            // - Bloodmoon starting/ending
            // - Item was dropped [Xyth]
            // - Item durability hits zero [Xyth]
            // - Screamer spawned for a chunk/player/xyz [kenyer]
            // - AirDrop spawned
            // - Player banned
            // - Player unbanned
            // - New Player connected for first time
            // - Events for ScriptingMod things
            // - Command that triggers when someone is in the air for more than X seconds, to catch hackers [war4head]

            #endregion

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
                var fileRelativePath = FileTools.GetRelativePath(filePath, Constants.ScriptsFolder);
                var fileName = Path.GetFileName(filePath);

                if (fileName.StartsWith("_"))
                {
                    Log.Out($"Script file {fileRelativePath} is ignored because it starts with underscore.");
                    continue;
                }

                Log.Debug($"Loading script {fileRelativePath} ...");

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
                        Log.Out($"Registered command{(commandNames.Length == 1 ? "" : "s")} \"{commandNames.Join(" ")}\" from script {fileRelativePath}.");
                    }

                    // Register events
                    var eventNames = metadata.GetValue("events", "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (eventNames.Length > 0)
                    {
                        scriptUsed = true;
                        foreach (var eventName in eventNames)
                        {
                            ScriptEvent eventType;
                            try
                            {
                                // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
                                eventType = (ScriptEvent)Enum.Parse(typeof(ScriptEvent), eventName);
                            }
                            catch (Exception)
                            {
                                Log.Warning($"Event \"{eventName}\" in script {fileRelativePath} is unknown and will be ignored.");
                                continue;
                            }

                            if (_events[(int)eventType] == null)
                                _events[(int)eventType] = new List<string>();
                            _events[(int)eventType].Add(filePath);
                        }
                        Log.Out($"Registered event{(eventNames.Length == 1 ? "" : "s")} \"{eventNames.Join(" ")}\" from script {fileRelativePath}.");
                    }

                    if (!scriptUsed)
                    {
                        Log.Out($"Script file {fileRelativePath} is ignored because it defines neither command names nor events.");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Could not load command script {fileRelativePath}: {ex}");
                }
            }

            SaveChanges();

            Log.Debug("All script commands added.");
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

        /// <summary>
        /// Returns true if ANY of the eventTypes is active
        /// </summary>
        /// <param name="eventTypes"></param>
        /// <returns></returns>
        public static bool IsAnyEventActive(params ScriptEvent[] eventTypes)
        {
            var logEvents = PersistentData.Instance.LogEvents;
            return eventTypes.Any(t => _events[(int) t] != null || logEvents.Contains(t));
        }

        public static void InvokeScriptEvents(ScriptEventArgs eventArgs)
        {
            TrackInvocation(eventArgs);

            if (PersistentData.Instance.LogEvents.Contains(eventArgs.type))
            {
                Log.Out("[EVENT] " + eventArgs.ToJson());
            }

            if (_events[(int)eventArgs.type] != null)
            {
                foreach (var filePath in _events[(int)eventArgs.type])
                {
                    var scriptEngine = ScriptEngine.GetInstance(Path.GetExtension(filePath));
                    scriptEngine.ExecuteEvent(filePath, eventArgs.type, eventArgs);
                }
            }
        }

        /// <summary>
        /// Track when and how this event was invoked first time;
        /// Only for development to learn if and when events are called.
        /// </summary>
        /// <param name="eventArgs"></param>
        [Conditional("DEBUG")]
        private static void TrackInvocation(ScriptEventArgs eventArgs)
        {
#if DEBUG
            var invocationLog = Environment.NewLine +
                                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine +
                                eventArgs.ToJson() + Environment.NewLine +
                                Environment.StackTrace;

            var invokedEvent = PersistentData.Instance.InvokedEvents.FirstOrDefault(ie => ie.EventName == eventArgs.type.ToString());

            if (invokedEvent == null)
            {
                invokedEvent = new PersistentData.InvokedEvent()
                {
                    EventName = eventArgs.type.ToString(),
                    FirstCall = invocationLog.Indent(8) + Environment.NewLine + new string(' ', 6),
                    LastCalls = new List<string>()
                };
                PersistentData.Instance.InvokedEvents.Add(invokedEvent);
            }

            // Rotate last 10 call logs with newest on top
            if (invokedEvent.LastCalls.Count == 10)
                invokedEvent.LastCalls.RemoveAt(invokedEvent.LastCalls.Count - 1);
            invokedEvent.LastCalls.Insert(0, invocationLog.Indent(10) + Environment.NewLine + new string(' ', 8));

            PersistentData.Instance.SaveLater();
#endif
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

            SaveChanges();

            // Clear out attached scripts
            Array.Clear(_events, 0, _events.Length);

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
