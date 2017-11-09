using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace ScriptingMod.Tools
{
    internal static class PatchTools
    {
        class TestEntityZombie : EntityZombie
        {
            public void TestDropCorpseBlock()
            {
                dropCorpseBlock();
            }
        }

        public static void InitPatches()
        {
            MethodInfo methodToReplace;
            MethodInfo methodToInject;

            Log.Out("Initializing patches ...");
            methodToReplace = Extensions.NonPublic.GetMethod(typeof(global::EntityZombie), "dropCorpseBlock");
            methodToInject = Extensions.NonPublic.GetMethod(typeof(Patches.EntityZombie), "dropCorpseBlock");
            //ReplaceMethod3(methodToReplace, methodToInject);
            Log.Out("Testing patched method ...");
            new TestEntityZombie().TestDropCorpseBlock();
            Log.Out("Patched item duplication exploit on zombie corpses.");

        }

        // see: https://stackoverflow.com/a/39214531/785111
        public static void ReplaceMethod2(MethodInfo methodToReplace, MethodInfo methodToInject)
        {
            unsafe
            {
                if (IntPtr.Size == 4)
                {
                    Log.Out($"Replacing method {methodToReplace.DeclaringType}.{methodToReplace.Name} in x68 mode ...");

                    uint* tarPtr = (uint*)methodToReplace.MethodHandle.Value.ToPointer();
                    uint* injPtr = (uint*)methodToInject.MethodHandle.Value.ToPointer();

                    uint* tar = (uint*)*(tarPtr + 5) + 12;
                    uint* inj = (uint*)*(injPtr + 5) + 12;
                    *tar = *inj;
                }
                else
                {
                    Log.Out($"Replacing method {methodToReplace.DeclaringType}.{methodToReplace.Name} in x64 mode ...");

                    ulong* tarPtr = (ulong*)methodToReplace.MethodHandle.Value.ToPointer();
                    ulong* injPtr = (ulong*)methodToInject.MethodHandle.Value.ToPointer();

                    ulong* tar = (ulong*)*(tarPtr + 5) + 12;
                    ulong* inj = (ulong*)*(injPtr + 5) + 12;
                    *tar = *inj;
                }
            }
        }

        // see: https://stackoverflow.com/a/36415711/785111
        public static void ReplaceMethod1(MethodInfo methodToReplace, MethodInfo methodToInject)
        {
            RuntimeHelpers.PrepareMethod(methodToReplace.MethodHandle);
            RuntimeHelpers.PrepareMethod(methodToInject.MethodHandle);

            unsafe
            {
                if (IntPtr.Size == 4)
                {
                    int* inj = (int*) methodToInject.MethodHandle.Value.ToPointer() + 2;
                    int* tar = (int*) methodToReplace.MethodHandle.Value.ToPointer() + 2;

//#if DEBUG
                    if (Debugger.IsAttached)
                    {
                        //Console.WriteLine("\nVersion x86 Debug\n");
                        Log.Out($"Replacing method {methodToReplace.DeclaringType}.{methodToReplace.Name} in x68 debug mode ...");

                        byte* injInst = (byte*) *inj;
                        byte* tarInst = (byte*) *tar;

                        int* injSrc = (int*) (injInst + 1);
                        int* tarSrc = (int*) (tarInst + 1);

                        *tarSrc = (((int) injInst + 5) + *injSrc) - ((int) tarInst + 5);
                    }
//#else
                    else
                    {
                        //Console.WriteLine("\nVersion x86 Release\n");
                        Log.Out($"Replacing method {methodToReplace.DeclaringType}.{methodToReplace.Name} in x68 release mode ...");
                        *tar = *inj;
                    }
//#endif
                }
                else
                {
                    long* inj = (long*) methodToInject.MethodHandle.Value.ToPointer() + 1;
                    long* tar = (long*) methodToReplace.MethodHandle.Value.ToPointer() + 1;

                    //#if DEBUG
                    if (Debugger.IsAttached)
                    {
                        //Console.WriteLine("\nVersion x64 Debug\n");
                        Log.Out($"Replacing method {methodToReplace.DeclaringType}.{methodToReplace.Name} in x64 debug mode ...");
                        byte* injInst = (byte*) *inj;
                        byte* tarInst = (byte*) *tar;


                        int* injSrc = (int*) (injInst + 1);
                        int* tarSrc = (int*) (tarInst + 1);

                        *tarSrc = (((int) injInst + 5) + *injSrc) - ((int) tarInst + 5);
                    }
//#else
                    else
                    {
                        //Console.WriteLine("\nVersion x64 Release\n");
                        Log.Out($"Replacing method {methodToReplace.DeclaringType}.{methodToReplace.Name} in x64 release mode ...");
                        *tar = *inj;
                    }
                    //#endif
                }
            }

        }
    }
}
