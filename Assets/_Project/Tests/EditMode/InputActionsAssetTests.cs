// Authority: none (EditMode tests). Loads the committed FishGameInput.inputactions through InputActionAsset.FromJson
// and checks maps, actions, types, schemes (spec 8.1, 17.1) and the default bindings (spec 8.3).
using System.IO;
using FishGame.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FishGame.Tests.EditMode
{
    public class InputActionsAssetTests
    {
        private static InputActionAsset LoadFromJson()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string path = Path.GetFullPath(Path.Combine(projectRoot, InputActionNames.AssetPath));
            Assert.IsTrue(File.Exists(path), "Missing input asset: " + path);
            return InputActionAsset.FromJson(File.ReadAllText(path));
        }

        private static bool HasGroup(InputBinding binding, string group)
        {
            if (string.IsNullOrEmpty(binding.groups)) return false;
            foreach (string g in binding.groups.Split(';'))
                if (g == group) return true;
            return false;
        }

        // A plain (non-composite) binding, or a composite part, with this exact path and group.
        private static bool HasBinding(InputAction action, string path, string group)
        {
            foreach (InputBinding b in action.bindings)
                if (!b.isComposite && b.path == path && HasGroup(b, group)) return true;
            return false;
        }

        // A composite part with this part name (e.g. "negative"), path and group.
        private static bool HasCompositePart(InputAction action, string partName, string path, string group)
        {
            foreach (InputBinding b in action.bindings)
                if (b.isPartOfComposite && b.name == partName && b.path == path && HasGroup(b, group)) return true;
            return false;
        }

        [Test]
        public void InputActions_FromJson_HasMapsActionsAndTypes()
        {
            InputActionAsset asset = LoadFromJson();
            try
            {
                Assert.AreEqual(3, asset.actionMaps.Count, "Exactly Player, Fishing, UI in M1 (no Boat map)");
                InputActionMap player = asset.FindActionMap(InputActionNames.PlayerMap);
                InputActionMap fishing = asset.FindActionMap(InputActionNames.FishingMap);
                InputActionMap ui = asset.FindActionMap(InputActionNames.UiMap);
                Assert.IsNotNull(player, "Player map");
                Assert.IsNotNull(fishing, "Fishing map");
                Assert.IsNotNull(ui, "UI map");
                Assert.IsNull(asset.FindActionMap("FishGameInput"), "No map may be named FishGameInput");

                Assert.AreEqual(InputActionNames.PlayerActions.Length, player.actions.Count, "Player action count");
                Assert.AreEqual(InputActionNames.FishingActions.Length, fishing.actions.Count, "Fishing action count");
                Assert.AreEqual(InputActionNames.UiActions.Length, ui.actions.Count, "UI action count");

                foreach (string name in InputActionNames.PlayerActions)
                    Assert.IsNotNull(asset.FindAction(InputActionNames.PlayerMap + "/" + name), "Missing Player/" + name);
                foreach (string name in InputActionNames.FishingActions)
                    Assert.IsNotNull(asset.FindAction(InputActionNames.FishingMap + "/" + name), "Missing Fishing/" + name);
                foreach (string name in InputActionNames.UiActions)
                    Assert.IsNotNull(asset.FindAction(InputActionNames.UiMap + "/" + name), "Missing UI/" + name);

                Assert.AreEqual(InputActionType.Value, asset.FindAction(InputActionNames.Move).type, "Move type");
                Assert.AreEqual("Vector2", asset.FindAction(InputActionNames.Move).expectedControlType, "Move control");
                Assert.AreEqual(InputActionType.Value, asset.FindAction(InputActionNames.Look).type, "Look type");
                Assert.AreEqual("Vector2", asset.FindAction(InputActionNames.Look).expectedControlType, "Look control");

                foreach (string name in InputActionNames.PlayerActions)
                {
                    if (name == "Move" || name == "Look") continue;
                    InputAction a = asset.FindAction(InputActionNames.PlayerMap + "/" + name);
                    Assert.AreEqual(InputActionType.Button, a.type, "Player/" + name + " type");
                }

                Assert.AreEqual(InputActionType.Button, asset.FindAction(InputActionNames.Reel).type, "Reel type");
                Assert.AreEqual(InputActionType.Value, asset.FindAction(InputActionNames.Steer).type, "Steer type");
                Assert.AreEqual("Axis", asset.FindAction(InputActionNames.Steer).expectedControlType, "Steer control");

                // UI (contract D17: names are the requirement; types follow the Unity UI defaults).
                Assert.AreEqual(InputActionType.PassThrough, asset.FindAction(InputActionNames.Navigate).type, "Navigate type");
                Assert.AreEqual(InputActionType.Button, asset.FindAction(InputActionNames.Submit).type, "Submit type");
                Assert.AreEqual(InputActionType.Button, asset.FindAction(InputActionNames.Cancel).type, "Cancel type");
                Assert.AreEqual(InputActionType.PassThrough, asset.FindAction(InputActionNames.Point).type, "Point type");
                Assert.AreEqual(InputActionType.PassThrough, asset.FindAction(InputActionNames.Click).type, "Click type");

                Assert.AreEqual(2, asset.controlSchemes.Count, "Exactly two control schemes");
                Assert.GreaterOrEqual(asset.FindControlSchemeIndex(InputActionNames.KeyboardMouseScheme), 0, "Keyboard&Mouse scheme");
                Assert.GreaterOrEqual(asset.FindControlSchemeIndex(InputActionNames.GamepadScheme), 0, "Gamepad scheme");
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void InputActions_FromJson_DefaultBindingsMatchSpec()
        {
            const string KM = InputActionNames.KeyboardMouseScheme;
            const string GP = InputActionNames.GamepadScheme;
            // { action path, binding path, group } for every plain binding required by spec 8.3.
            string[][] expected =
            {
                new[] { InputActionNames.Move, "<Gamepad>/leftStick", GP },
                new[] { InputActionNames.Look, "<Mouse>/delta", KM },
                new[] { InputActionNames.Look, "<Gamepad>/rightStick", GP },
                new[] { InputActionNames.Jump, "<Keyboard>/space", KM },
                new[] { InputActionNames.Jump, "<Gamepad>/buttonSouth", GP },
                new[] { InputActionNames.Sprint, "<Keyboard>/leftShift", KM },
                new[] { InputActionNames.Sprint, "<Gamepad>/leftStickPress", GP },
                new[] { InputActionNames.Primary, "<Mouse>/leftButton", KM },
                new[] { InputActionNames.Primary, "<Gamepad>/rightTrigger", GP },
                new[] { InputActionNames.Secondary, "<Mouse>/rightButton", KM },
                new[] { InputActionNames.Secondary, "<Gamepad>/leftTrigger", GP },
                new[] { InputActionNames.Interact, "<Keyboard>/e", KM },
                new[] { InputActionNames.Interact, "<Gamepad>/buttonWest", GP },
                new[] { InputActionNames.Shove, "<Keyboard>/f", KM },
                new[] { InputActionNames.Shove, "<Gamepad>/rightStickPress", GP },
                new[] { InputActionNames.Drop, "<Keyboard>/g", KM },
                new[] { InputActionNames.Drop, "<Gamepad>/buttonEast", GP },
                new[] { InputActionNames.Equip, "<Keyboard>/1", KM },
                new[] { InputActionNames.Equip, "<Mouse>/scroll/up", KM },
                new[] { InputActionNames.Equip, "<Mouse>/scroll/down", KM },
                new[] { InputActionNames.Equip, "<Gamepad>/buttonNorth", GP },
                new[] { InputActionNames.Journal, "<Keyboard>/j", KM },
                new[] { InputActionNames.Journal, "<Gamepad>/select", GP },
                new[] { InputActionNames.Pause, "<Keyboard>/escape", KM },
                new[] { InputActionNames.Pause, "<Gamepad>/start", GP },
                new[] { InputActionNames.Reel, "<Keyboard>/s", KM },
                new[] { InputActionNames.Steer, "<Gamepad>/leftStick/x", GP },
                new[] { InputActionNames.Submit, "<Keyboard>/enter", KM },
                new[] { InputActionNames.Submit, "<Gamepad>/buttonSouth", GP },
                new[] { InputActionNames.Cancel, "<Keyboard>/escape", KM },
                new[] { InputActionNames.Cancel, "<Gamepad>/buttonEast", GP },
                new[] { InputActionNames.Point, "<Mouse>/position", KM },
                new[] { InputActionNames.Click, "<Mouse>/leftButton", KM },
            };

            InputActionAsset asset = LoadFromJson();
            try
            {
                foreach (string[] row in expected)
                {
                    InputAction action = asset.FindAction(row[0]);
                    Assert.IsNotNull(action, row[0]);
                    Assert.IsTrue(HasBinding(action, row[1], row[2]), row[0] + " needs " + row[1] + " in group " + row[2]);
                }

                InputAction move = asset.FindAction(InputActionNames.Move);
                Assert.IsTrue(HasCompositePart(move, "up", "<Keyboard>/w", KM), "Move up = W");
                Assert.IsTrue(HasCompositePart(move, "down", "<Keyboard>/s", KM), "Move down = S");
                Assert.IsTrue(HasCompositePart(move, "left", "<Keyboard>/a", KM), "Move left = A");
                Assert.IsTrue(HasCompositePart(move, "right", "<Keyboard>/d", KM), "Move right = D");

                InputAction steer = asset.FindAction(InputActionNames.Steer);
                Assert.IsTrue(HasCompositePart(steer, "negative", "<Keyboard>/a", KM), "Steer A = -1");
                Assert.IsTrue(HasCompositePart(steer, "positive", "<Keyboard>/d", KM), "Steer D = +1");
                bool hasAxisComposite = false;
                foreach (InputBinding b in steer.bindings)
                    if (b.isComposite && b.path == "1DAxis") hasAxisComposite = true;
                Assert.IsTrue(hasAxisComposite, "Steer has a 1DAxis composite");
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }
    }
}
