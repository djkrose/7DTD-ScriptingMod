using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using ScriptingMod.Exceptions;

namespace ScriptingMod.Tools
{
    internal static class LockTools
    {
        /// <summary>
        /// Tries a bit harder to enter a lock than the normal Monitor.TryEnter by attempting it multiple times with timeouts.
        /// When lock is acquired, the action is executed, otherwise a LockTimeoutException is thrown
        /// </summary>
        /// <param name="lockObject">Object to lock on to</param>
        /// <param name="action">Action to execute with lock</param>
        /// <exception cref="LockTimeoutException">If the lock could not be acquired after some attempts</exception>
        public static void TryLockHarder(object lockObject, Action action)
        {
            lock (lockObject)
                action();
            return;

            int timeout = 1000; //ms
            int maxAttempts = 5;

            int attempt = 0;
            while (!Monitor.TryEnter(lockObject, timeout))
            {
                attempt++;
                if (attempt >= maxAttempts)
                {
                    throw new LockTimeoutException($"Could not get lock for {lockObject} after {attempt} attempts timed out after {timeout} ms.");
                }
            }

            try
            {
                action();
            }
            finally
            {
                Monitor.Exit(lockObject);
            }
        }
    }
}
