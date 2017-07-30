using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using ScriptingMod.Exceptions;

namespace ScriptingMod.Extensions
{
    internal static class ReflectionExtensions
    {

        private const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;

        public static bool GetIsTriggered(this PowerTrigger obj)
        {
            return (bool)typeof(PowerTrigger).GetField("isTriggered", all).GetValue(obj);
        }

        public static void SetIsTriggered(this PowerTrigger obj, bool value)
        {
            typeof(PowerTrigger).GetField("isTriggered", all).SetValue(obj, value);
        }

        public static bool GetIsActive(this PowerTrigger obj)
        {
            return (bool)typeof(PowerTrigger).GetField("isActive", all).GetValue(obj);
        }

        public static void SetIsActive(this PowerTrigger obj, bool value)
        {
            typeof(PowerTrigger).GetField("isActive", all).SetValue(obj, value);
        }

        public static float GetDelayStartTime(this PowerTrigger obj)
        {
            return (float)typeof(PowerTrigger).GetField("delayStartTime", all).GetValue(obj);
        }

        public static void SetDelayStartTime(this PowerTrigger obj, float value)
        {
            typeof(PowerTrigger).GetField("delayStartTime", all).SetValue(obj, value);
        }

        public static float GetPowerTime(this PowerTrigger obj)
        {
            return (float)typeof(PowerTrigger).GetField("powerTime", all).GetValue(obj);
        }

        public static void SetPowerTime(this PowerTrigger obj, float value)
        {
            typeof(PowerTrigger).GetField("powerTime", all).SetValue(obj, value);
        }

        public static bool GetIsToggled(this PowerConsumerToggle obj)
        {
            return (bool) typeof(PowerConsumerToggle).GetField("isToggled", all).GetValue(obj);
        }

        public static void SetIsToggled(this PowerConsumerToggle obj, bool value)
        {
            typeof(PowerConsumerToggle).GetField("isToggled", all).SetValue(obj, value);
        }

        public static bool GetIsLocked(this PowerRangedTrap obj)
        {
            return (bool)typeof(PowerRangedTrap).GetField("isLocked", all).GetValue(obj);
        }

        public static void SetIsLocked(this PowerRangedTrap obj, bool value)
        {
            typeof(PowerRangedTrap).GetField("isLocked", all).SetValue(obj, value);
        }

        public static void SetHasChangesLocal(this PowerSource obj, bool value)
        {
            typeof(PowerSource).GetField("hasChangesLocal", all).SetValue(obj, value);
        }

    }
}
