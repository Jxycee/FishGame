using System;
using System.Collections.Generic;
using FishGame.Core;
using FishGame.Gameplay;
using UnityEngine;
using UnityEngine.UIElements;

namespace FishGame.UI
{
    // Client-only. Nameplates for remote players (PlayerNetState.Active minus Local), projected from each player's
    // NameplateAnchor with Camera.main, tinted with the slot colour, alpha fading from 15 m (opaque) to 25 m (hidden).
    internal sealed class NameplateOverlay
    {
        private const float FadeStartMeters = 15f;
        private const float FadeEndMeters = 25f;
        private const float FallbackHeight = 2.05f;

        private sealed class Entry
        {
            public Label Label;
            public Transform Anchor;
            public string Name;
            public int Slot = -1;
        }

        private readonly VisualElement _layer;
        private readonly Dictionary<PlayerNetState, Entry> _entries = new Dictionary<PlayerNetState, Entry>();
        private readonly List<PlayerNetState> _stale = new List<PlayerNetState>();
        private bool _active;

        public NameplateOverlay(VisualElement layer)
        {
            _layer = layer;
            SetActive(false);
        }

        public void SetActive(bool active)
        {
            _active = active;
            UiQuery.Show(_layer, active);
            if (!active) Clear();
        }

        public void Tick(Camera camera)
        {
            if (!_active || camera == null || _layer.panel == null) return;
            var players = PlayerNetState.Active;
            var local = PlayerNetState.Local;

            _stale.Clear();
            foreach (var pair in _entries)
            {
                if (pair.Key == null || pair.Key == local || !Contains(players, pair.Key)) _stale.Add(pair.Key);
            }
            foreach (var key in _stale)
            {
                _entries[key].Label.RemoveFromHierarchy();
                _entries.Remove(key);
            }

            var cameraTransform = camera.transform;
            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player == null || player == local) continue;

                if (!_entries.TryGetValue(player, out var entry))
                {
                    entry = new Entry { Label = new Label(), Anchor = player.transform.Find(PlayerRigPaths.NameplateAnchor) };
                    entry.Label.AddToClassList("nameplate");
                    entry.Label.pickingMode = PickingMode.Ignore;
                    _layer.Add(entry.Label);
                    _entries.Add(player, entry);
                }

                var displayName = player.DisplayName ?? string.Empty;
                if (!string.Equals(displayName, entry.Name, StringComparison.Ordinal))
                {
                    entry.Name = displayName;
                    entry.Label.text = displayName;
                }

                int slot = player.Slot;
                if (slot != entry.Slot)
                {
                    entry.Slot = slot;
                    SlotPalette.GetRgb(slot < SlotPalette.Count ? slot : 0, out var r, out var g, out var b);
                    entry.Label.style.color = new Color(r, g, b, 1f);
                }

                var world = entry.Anchor != null ? entry.Anchor.position : player.transform.position + Vector3.up * FallbackHeight;
                var toPlayer = world - cameraTransform.position;
                var alpha = Mathf.Clamp01((FadeEndMeters - toPlayer.magnitude) / (FadeEndMeters - FadeStartMeters));
                var visible = alpha > 0f && Vector3.Dot(cameraTransform.forward, toPlayer) > 0f;
                entry.Label.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                if (!visible) continue;

                entry.Label.style.opacity = alpha;
                var panelPosition = RuntimePanelUtils.CameraTransformWorldToPanel(_layer.panel, world, camera);
                entry.Label.style.left = panelPosition.x;
                entry.Label.style.top = panelPosition.y;
            }
        }

        private void Clear()
        {
            foreach (var pair in _entries) pair.Value.Label.RemoveFromHierarchy();
            _entries.Clear();
        }

        private static bool Contains(IReadOnlyList<PlayerNetState> players, PlayerNetState player)
        {
            for (var i = 0; i < players.Count; i++)
            {
                if (players[i] == player) return true;
            }
            return false;
        }
    }
}
