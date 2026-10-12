// Scene objects of the fishing system (contract 6.4). Called from LakeDockBuilder.Build right after AddBellAnchor.
// "Fishing" is a plain GameObject (C 2.8 #15: no in-scene NetworkObjects in M2) holding FishingHost, which is inert on clients.
// T27 later adds FightTelemetry to the same root.
using FishGame.Core;
using FishGame.Fishing;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishGame.EditorTools
{
    public static class FishingSceneObjects
    {
        public static void AddTo(Scene scene)
        {
            var go = new GameObject("Fishing");
            go.layer = Layers.Default;
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<FishingHost>();
        }
    }
}
