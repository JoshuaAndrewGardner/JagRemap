using System;
using System.Linq;
using System.Collections.Generic;
using SharpHook;
using SharpHook.Native;

namespace JagRemap
{
    class Program
    {
        private enum KeyPressAction
        {
            Suppress, DontSuppress
        };

        private static List<KeyCode> modifiersDown;
        private static Dictionary<KeyCode, (KeyPressAction, Dictionary<KeyCode, List<KeyCode>>)> modifierMaps = 
            new Dictionary<KeyCode, (KeyPressAction, Dictionary<KeyCode, List<KeyCode>>)>() {
                {
                    KeyCode.VcCapsLock, (
                        KeyPressAction.Suppress,
                        new Dictionary<KeyCode, List<KeyCode>>() {
                            { KeyCode.VcQ, new List<KeyCode>() { KeyCode.VcRightShift, KeyCode.Vc1 } },
                            { KeyCode.VcW, new List<KeyCode>() { KeyCode.VcRightShift, KeyCode.Vc2 } },
                            { KeyCode.VcE, new List<KeyCode>() { KeyCode.VcRightShift, KeyCode.Vc3 } },
                            { KeyCode.VcR, new List<KeyCode>() { KeyCode.VcRightShift, KeyCode.Vc4 } },
                            { KeyCode.VcT, new List<KeyCode>() { KeyCode.VcRightShift, KeyCode.Vc5 } },
                            { KeyCode.VcY, new List<KeyCode>() { KeyCode.VcRightShift, KeyCode.Vc6 } },
                            { KeyCode.VcU, new List<KeyCode>() { KeyCode.VcRightShift, KeyCode.Vc7 } },
                            { KeyCode.VcI, new List<KeyCode>() { KeyCode.VcRightShift, KeyCode.Vc8 } },
                            { KeyCode.VcO, new List<KeyCode>() { KeyCode.VcMinus } },
                            { KeyCode.VcP, new List<KeyCode>() { KeyCode.VcEquals } },

                            { KeyCode.VcA, new List<KeyCode>() { KeyCode.VcRightShift, KeyCode.VcComma } },
                            { KeyCode.VcS, new List<KeyCode>() { KeyCode.VcRightShift, KeyCode.VcOpenBracket } },
                            { KeyCode.VcD, new List<KeyCode>() { KeyCode.VcOpenBracket } },
                            { KeyCode.VcF, new List<KeyCode>() { KeyCode.VcRightShift, KeyCode.Vc9 } },
                            { KeyCode.VcG, new List<KeyCode>() { KeyCode.VcSlash } },
                            { KeyCode.VcH, new List<KeyCode>() { KeyCode.VcBackSlash } },
                            { KeyCode.VcJ, new List<KeyCode>() { KeyCode.VcRightShift, KeyCode.Vc0 } },
                            { KeyCode.VcK, new List<KeyCode>() { KeyCode.VcCloseBracket } },
                            { KeyCode.VcL, new List<KeyCode>() { KeyCode.VcRightShift, KeyCode.VcCloseBracket } },
                            { KeyCode.VcSemicolon, new List<KeyCode>() { KeyCode.VcRightShift, KeyCode.VcPeriod } },

                            { KeyCode.VcV, new List<KeyCode>() { KeyCode.VcRightShift, KeyCode.VcBackSlash } },
                            { KeyCode.VcB, new List<KeyCode>() { KeyCode.VcRightShift, KeyCode.VcMinus } },
                            { KeyCode.VcN, new List<KeyCode>() { KeyCode.VcRightShift, KeyCode.VcEquals } },

                            { KeyCode.VcEquals, new List<KeyCode>() { KeyCode.VcRightShift, KeyCode.VcSemicolon, KeyCode.VcRightShift, KeyCode.VcEquals }}
                        }
                    )
                },
                {
                    KeyCode.VcLeftControl, (
                        KeyPressAction.DontSuppress,
                        new Dictionary<KeyCode, List<KeyCode>>() {
                            { KeyCode.VcBackspace, new List<KeyCode>() { KeyCode.VcLeftShift, KeyCode.VcNumLock, KeyCode.VcNumLock, KeyCode.VcLeft, KeyCode.VcNumLock, KeyCode.VcNumLock, KeyCode.VcLeftShift, KeyCode.VcBackspace } },
                            { KeyCode.VcDelete, new List<KeyCode>() { KeyCode.VcLeftShift, KeyCode.VcNumLock, KeyCode.VcNumLock, KeyCode.VcRight, KeyCode.VcNumLock, KeyCode.VcNumLock, KeyCode.VcLeftShift, KeyCode.VcBackspace } },
                        }
                    )
                }
            };
        private static readonly List<KeyCode> modifierKeys = modifierMaps.Keys.ToList();
        private static readonly List<KeyCode> emptyList = new List<KeyCode>();
        private static List<KeyCode> simulatedKeysDown = new List<KeyCode>();
        private static IGlobalHook hook;
        private static EventSimulator eventSimulator;

        private static bool SimulatedPress = false;

        static void Main()
        {
            modifiersDown = new List<KeyCode>();
            eventSimulator = new EventSimulator();

            ResetCapsLock();
            ResetNumLock();

            hook = new SimpleGlobalHook();
            hook.KeyPressed += OnKeyPressed;
            hook.KeyReleased += OnKeyReleased;
            hook.Run();
        }

        private static void ResetCapsLock()
        {
            if (Console.CapsLock)
            {
                SimulateKeyPresses(new List<KeyCode>() { KeyCode.VcCapsLock });
            }
        }

        private static void ResetNumLock()
        {
            if (!Console.NumberLock)
            {
                SimulateKeyPresses(new List<KeyCode>() { KeyCode.VcNumLock });
            }
        }

        private static List<KeyCode> GetKeyMap(KeyCode key)
        {
            return modifierMaps
                .Where(entry => modifiersDown.Contains(entry.Key) && entry.Value.Item2.Keys.ToList().Contains(key))
                .Select(entry => entry.Value.Item2[key])
                .DefaultIfEmpty(emptyList)
                .First();
        }

        private static bool SuppressModifierKey(KeyCode key)
        {
            return modifierMaps
                .Where(entry => entry.Key == key)
                .Select(map => map.Value.Item1 == KeyPressAction.Suppress)
                .DefaultIfEmpty(false)
                .First();
        }

        private static void OnKeyPressed(object sender, KeyboardHookEventArgs e)
        {
            if (SimulatedPress)
            {
                return;
            }

            KeyCode currentKey = e.Data.KeyCode;
            modifiersDown.AddRange(modifierKeys.Where(key => key == currentKey && !modifiersDown.Contains(key)));


            if (SuppressModifierKey(currentKey))
            {
                e.Reserved = EventReservedValueMask.SuppressEvent;
                return;
            }

            List<KeyCode> mappedKeys = GetKeyMap(currentKey);
            if (mappedKeys.Count == 0)
            {
                return;
            }

            e.Reserved = EventReservedValueMask.SuppressEvent;
            SimulateKeyPresses(mappedKeys);
        }

        private static void OnKeyReleased(object sender, KeyboardHookEventArgs e)
        {
            if (SimulatedPress)
            {
                return;
            }

            KeyCode currentKey = e.Data.KeyCode;
            modifiersDown.RemoveAll(key => key == currentKey);
        }

        private static void SimulateKeyPresses(List<KeyCode> keys)
        {
            SimulatedPress = true;

            keys.ForEach(key =>
                {
                    if (simulatedKeysDown.Contains(key))
                    {
                        simulatedKeysDown.Remove(key);
                        eventSimulator.SimulateKeyRelease(key);
                    }
                    else
                    {
                        simulatedKeysDown.Add(key);
                        eventSimulator.SimulateKeyPress(key);
                    }
                });
            simulatedKeysDown.ForEach(key => eventSimulator.SimulateKeyRelease(key));
            simulatedKeysDown.Clear();

            SimulatedPress = false;
        }
    }
}
