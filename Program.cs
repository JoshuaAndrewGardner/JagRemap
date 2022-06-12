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

        private static HashSet<KeyCode> modifiersDown;
        private static Dictionary<KeyCode, Dictionary<KeyCode, List<KeyCode>>> modifierMaps =
            new Dictionary<KeyCode, Dictionary<KeyCode, List<KeyCode>>>() {
                {
                    KeyCode.VcCapsLock,
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
                },
                {
                    KeyCode.VcLeftControl,
                        new Dictionary<KeyCode, List<KeyCode>>() {
                            { KeyCode.VcBackspace, new List<KeyCode>() { KeyCode.VcLeftShift, KeyCode.VcLeftControl, KeyCode.VcLeft, KeyCode.VcLeftControl, KeyCode.VcBackspace } },
                            { KeyCode.VcDelete, new List<KeyCode>() { KeyCode.VcLeftShift, KeyCode.VcLeftControl, KeyCode.VcRight, KeyCode.VcLeftControl, KeyCode.VcBackspace } },
                        }
                }
            };

        private static readonly HashSet<KeyCode> suppressedModifiers = new HashSet<KeyCode> { KeyCode.VcCapsLock };
        private static readonly List<KeyCode> modifierKeys = modifierMaps.Keys.ToList();
        private static readonly HashSet<KeyCode> numLockToggleKeys = new HashSet<KeyCode> { KeyCode.VcLeft, KeyCode.VcRight, KeyCode.VcUp, KeyCode.VcDown };
        private static IGlobalHook hook;
        private static EventSimulator eventSimulator;

        private static bool SimulatedPress = false;

        static void Main()
        {
            modifiersDown = new HashSet<KeyCode>();
            eventSimulator = new EventSimulator();

            EnsureModifierState();

            hook = new SimpleGlobalHook();
            hook.KeyPressed += OnKeyPressed;
            hook.KeyReleased += OnKeyReleased;
            hook.Run();
        }

        private static void EnsureModifierState()
        {
            SimulatedPress = true;
            ResetCapsLock();
            ResetNumLock();
            SimulatedPress = false;
        }

        private static void ResetCapsLock()
        {
            if (Console.CapsLock)
            {
                eventSimulator.SimulateKeyPress(KeyCode.VcCapsLock);
                eventSimulator.SimulateKeyRelease(KeyCode.VcCapsLock);
            }
        }

        private static void ResetNumLock(bool active = false)
        {
            if (Console.NumberLock == active)
            {
                eventSimulator.SimulateKeyPress(KeyCode.VcNumLock);
                eventSimulator.SimulateKeyRelease(KeyCode.VcNumLock);
            }
        }

        private static bool IsKeyCurrentlyModified(KeyCode keyCode)
        {
            return modifiersDown
                .Any(modifier => modifierMaps[modifier].Keys.Contains(keyCode));
        }

        private static KeyCode GetModifyingKey(KeyCode keyCode)
        {
            return modifiersDown
                .Where(modifier => modifierMaps[modifier].Keys.Contains(keyCode))
                .First();
        }

        private static List<KeyCode> GetKeyMap(KeyCode key, KeyCode modifyingKey)
        {
            return modifierMaps[modifyingKey][key];
        }

        private static void OnKeyPressed(object sender, KeyboardHookEventArgs e)
        {
            if (SimulatedPress)
            {
                return;
            }

            KeyCode currentKeyCode = e.Data.KeyCode;
            if (modifierKeys.Contains(currentKeyCode))
            {
                modifiersDown.Add(currentKeyCode);
                if (suppressedModifiers.Contains(currentKeyCode))
                {
                    e.Reserved = EventReservedValueMask.SuppressEvent;
                }
                EnsureModifierState();
                return;
            }

            if (modifiersDown.Count == 0)
            {
                return;
            }

            if (!IsKeyCurrentlyModified(currentKeyCode))
            {
                return;
            }

            e.Reserved = EventReservedValueMask.SuppressEvent;
            SimulateKeyModification(currentKeyCode);
        }

        private static void OnKeyReleased(object sender, KeyboardHookEventArgs e)
        {
            if (SimulatedPress)
            {
                return;
            }

            KeyCode currentKeyCode = e.Data.KeyCode;
            modifiersDown.Remove(currentKeyCode);
        }

        private static void SimulateKeyToggle(Dictionary<KeyCode, bool> simulatedKeysDown, KeyCode key)
        {
            if (!simulatedKeysDown.ContainsKey(key))
            {
                simulatedKeysDown.Add(key, false);
            }

            if (!simulatedKeysDown[key])
            {
                eventSimulator.SimulateKeyPress(key);
            }
            else
            {
                eventSimulator.SimulateKeyRelease(key);
            }

            simulatedKeysDown[key] = !simulatedKeysDown[key];
        }

        private static void SimulateKeyModification(KeyCode key)
        {
            SimulatedPress = true;
            KeyCode modifier = GetModifyingKey(key);
            List<KeyCode> modifiedKeyPresses = GetKeyMap(key, modifier);
            Dictionary<KeyCode, bool> simulatedKeysDown = new Dictionary<KeyCode, bool>();
            if (!suppressedModifiers.Contains(modifier))
            {
                eventSimulator.SimulateKeyRelease(modifier);
            }
            if (modifiedKeyPresses.Any(key => numLockToggleKeys.Contains(key)))
            {
                ResetNumLock(true);
            }

            modifiedKeyPresses.ForEach(key =>
            {
                SimulateKeyToggle(simulatedKeysDown, key);
            });
            simulatedKeysDown
                .Where(record => record.Value)
                .ToList()
                .ForEach(record => SimulateKeyToggle(simulatedKeysDown, record.Key));

            if (modifiedKeyPresses.Any(key => numLockToggleKeys.Contains(key)))
            {
                ResetNumLock();
            }
            if (!suppressedModifiers.Contains(modifier))
            {
                eventSimulator.SimulateKeyPress(modifier);
            }
            SimulatedPress = false;
        }
    }
}
