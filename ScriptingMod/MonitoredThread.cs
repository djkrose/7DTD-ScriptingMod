using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace ScriptingMod
{
    internal class MonitoredThread
    {
        public event EventHandler<ProgressedEventArgs> Progressed;
        public event EventHandler<EventArgs> Completed;
        public event EventHandler<AbortedEventArgs> Aborted;
        public event EventHandler<CrashedEventArgs> Crashed;

        private Action<MonitoredThread> _action;
        private int _timeoutProgress; // ms
        private int _timeoutTotal; // ms
        private Thread _thread;
        private string _threadName;
        private object _waitLock = new object();

        private string ThreadName => _thread.Name + "(" + _thread.ManagedThreadId + ")";

        public static MonitoredThread Start(Action<MonitoredThread> action, string threadName = "unnamed", int timeoutProgress = 0, int timeoutTotal = 0)
        {
            var mt = new MonitoredThread(action, threadName, timeoutProgress, timeoutTotal);
            mt.Start();
            return mt;
        }

        public MonitoredThread(Action<MonitoredThread> action, string threadName = "unnamed", int timeoutProgress = 0, int timeoutTotal = 0)
        {
            _action = action;
            _timeoutProgress = timeoutProgress;
            _timeoutTotal = timeoutTotal;
            _threadName = threadName;
        }

        public void Start()
        {
            _thread = new Thread(ActionWrapper);
            _thread.IsBackground = true;
            _thread.Name = _threadName;
            _thread.Start();
            Log.Debug($"[Start] Thread '{ThreadName}' started.");

            MonitorThreadProgress();
            MonitorThreadTimeout();
        }

        public void ReportProgress(int percent)
        {
            Progressed?.Invoke(this, new ProgressedEventArgs() { Progress = percent });
        }

        private void ActionWrapper()
        {
            try
            {
                Log.Debug($"[ActionWrapper] Thread '{ThreadName}' running.");
                _action(this);
                Log.Debug($"[ActionWrapper] Thread '{ThreadName}' ended normally.");
                Completed?.Invoke(this, new EventArgs());
            }
            catch (ThreadAbortException ex)
            {
                Log.Debug($"[ActionWrapper] Thread '{ThreadName}' was aborted: " + ex);
                Aborted?.Invoke(this, new AbortedEventArgs() {Exception = ex});
            }
            catch (Exception ex)
            {
                Log.Debug($"[ActionWrapper] Thread '{ThreadName}' threw exception: " + ex);
                Crashed?.Invoke(this, new CrashedEventArgs() {Exception = ex});
            }
            finally
            {
                lock (_waitLock)
                    Monitor.Pulse(_waitLock);
            }
        }

        private void MonitorThreadProgress()
        {
            if (_timeoutProgress <= 0)
                return;

            ThreadPool.UnsafeQueueUserWorkItem(delegate
            {
                Log.Debug($"[MonitorThreadProgress] Monitor running for thread '{ThreadName}'.");

                lock (_waitLock)
                {
                    while (_thread.IsAlive && Monitor.Wait(_waitLock, _timeoutProgress))
                    {
                        // iterates until thread ended or progress pulse timed out
                        Log.Debug($"[MonitorThreadProgress] Received pulse for thread '{ThreadName}' in state {_thread.ThreadState.ToString()}.");
                    }
                }

                if (_thread.IsAlive)
                {
                    Log.Debug($"[MonitorThreadProgress] Trying to abort thread '{ThreadName}' after {_timeoutProgress} ms since last progress.");
                    TryAbort(); // MUST be outside of lock! otherwise deadlock!
                    Log.Warning($"[MonitorThreadProgress] Thread '{ThreadName}' aborted after {_timeoutProgress} ms since last progress.");
                }
                else
                {
                    Log.Debug($"[MonitorThreadProgress] Thread '{ThreadName}' ended in state {_thread.ThreadState.ToString()}.");
                }
            }, null);

            Log.Debug($"[MonitorThreadProgress] Monitor started for thread '{ThreadName}'.");
        }

        private void PulseWaitlock(object sender, EventArgs e)
        {
            lock (_waitLock)
                Monitor.Pulse(_waitLock);
        }

        private void MonitorThreadTimeout()
        {
            if (_timeoutTotal <= 0)
                return;

            ThreadPool.UnsafeQueueUserWorkItem(delegate
            {
                Log.Debug($"[MonitorThreadTimeout] Monitor running for thread '{ThreadName}'.");

                // Wait until original thread ended
                if (!_thread.Join(_timeoutTotal))
                {
                    Log.Debug($"[MonitorThreadTimeout] Trying to abort thread '{ThreadName}' after {_timeoutTotal} ms total runtime.");
                    TryAbort();
                    Log.Warning($"[MonitorThreadTimeout] Thread '{ThreadName}' aborted after {_timeoutTotal} ms total runtime.");
                }
                else
                {
                    Log.Debug($"[MonitorThreadTimeout] Thread '{ThreadName}' ended in state {_thread.ThreadState.ToString()}.");
                }
            }, null);

            Log.Debug($"[MonitorThreadTimeout] Monitor started for thread '{ThreadName}'.");
        }

        private void TryAbort()
        {
            try
            {
                _thread.Abort();
                Log.Debug($"[TryAbort] Thread '{ThreadName}' aborted.");
            }
            catch (ThreadStateException ex)
            {
                // thread probably ended normally in the meantime
                Log.Debug($"[TryAbort] Aborting thread '{ThreadName}' failed: " + ex);
            }
        }

        public class ProgressedEventArgs : EventArgs
        {
            public int Progress;
        }

        public class AbortedEventArgs : EventArgs
        {
            public Exception Exception;
        }

        public class CrashedEventArgs : EventArgs
        {
            public Exception Exception;
        }

    }
}
