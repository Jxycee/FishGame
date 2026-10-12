// Authority: test-only. T23: the presentation feedback (FishingFeedback, CatchRevealView) is observed from outside, because this assembly does not
// reference Presentation (C 2.8 #8): shader globals (_Ripples), the EventBus (CaptionRequested), the fake input's last rumble, and the
// "CatchReveal" child's active state, mesh, materials and pose. The scripted cast reuses T19's FishingPlayModeDriver (ruling F-Q7).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using FishGame.Core;
using FishGame.Data;
using FishGame.Fishing;
using FishGame.Gameplay;
using FishGame.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FishGame.Tests.PlayMode
{
    public class FeedbackSoloTests
    {
        private const string RevealName = "CatchReveal";

        private FishingRig _rig;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            FishingRig rig = _rig;
            _rig = null;
            yield return FishingPlayModeDriver.TearDown(rig);
        }

        // Catches: no cast-landing ripple at the landing point (or one stamped elsewhere); no cap.bite caption at the bite (or never on the
        // owner); no catch reveal after the announcement, or one without a mesh, a material per submesh, or that never hides; rumble left on at
        // Idle; a warning anywhere in the loop.
        [UnityTest]
        public IEnumerator FeedbackSolo_CastBiteLand_RippleCaptionAndReveal()
        {
            FishingDebugSettings.ForcedBiteWait = 0.1f;
            FishingDebugSettings.ForcedSpeciesId = "bluegill";
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig);
            FishingRig rig = _rig;

            Assert.That(ServiceRoots.App.TryGet(out EventBus bus), Is.True, "EventBus");
            Assert.That(ServiceRoots.App.TryGet(out ISettings settings), Is.True, "ISettings");
            Assert.That(settings.Current.captions, Is.True, "captions default on");

            var captions = new List<string>();
            Vector3 landing = Vector3.zero;
            bool haveLanding = false;
            rig.Net.CastResultReceived += (castId, valid, reason, point, roll) =>
            {
                if (!valid) return;
                landing = point;
                haveLanding = true;
            };

            using (bus.Subscribe<CaptionRequested>(c => captions.Add(c.Key)))
            {
                yield return FishingPlayModeDriver.EquipAndReady(rig);
                yield return FishingPlayModeDriver.ChargeAndRelease(rig);

                // (a) the cast-landing ripple, polled every frame until the fish is hooked (later ripples must not hide the first one).
                double start = Time.realtimeSinceStartupAsDouble;
                double seenWaiting = -1, seenRipple = -1;
                while (rig.Machine.State != RodState.Hooked)
                {
                    if (Time.realtimeSinceStartupAsDouble - start > 8.0) Assert.Fail("never hooked (state " + rig.Machine.State + ")");
                    if (seenWaiting < 0 && rig.Machine.State == RodState.Waiting) seenWaiting = Time.realtimeSinceStartupAsDouble;
                    if (seenRipple < 0 && haveLanding && HasRipple(RippleMath.CastLanding, landing, 0.5f)) seenRipple = Time.realtimeSinceStartupAsDouble;
                    yield return null;
                }
                if (seenRipple < 0 && haveLanding && HasRipple(RippleMath.CastLanding, landing, 0.5f)) seenRipple = Time.realtimeSinceStartupAsDouble;

                Assert.That(haveLanding, Is.True, "the host's cast result (landing point) never arrived");
                Assert.That(seenRipple, Is.GreaterThanOrEqualTo(0.0), "no _Ripples slot with strength CastLanding within 0.5 m of the landing " + landing);
                if (seenWaiting >= 0) Assert.That(seenRipple - seenWaiting, Is.LessThanOrEqualTo(1.5), "the landing ripple appeared too late");

                // (b) the bite caption (owner, settings.captions).
                Assert.That(captions, Does.Contain("cap.bite"), "CaptionRequested(cap.bite) at the bite");

                yield return FishingPlayModeDriver.FightRhythm(rig, 60f);
                Assert.That(rig.Machine.State, Is.EqualTo(RodState.Landed), "the scripted fight must land the fish");

                // (c) the reveal: active within 0.5 s of the announcement, with a mesh and one material per submesh, hidden again within 2.5 s.
                yield return PlayModeHarness.WaitUntil(() => rig.Announced.Count >= 1, 3f, "the catch was never announced");
                double announced = Time.realtimeSinceStartupAsDouble;
                Transform reveal = rig.Player.transform.Find(RevealName);
                Assert.That(reveal, Is.Not.Null, "PlayerFP has no '" + RevealName + "' child (CatchRevealView missing)");
                yield return PlayModeHarness.WaitUntil(() => reveal.gameObject.activeSelf, 0.5f, "the catch reveal did not show within 0.5 s of the announcement");
                AssertRevealRenders(reveal);


                // The terminal pulse (0.3 s at most) has ended and FishingFeedback itself zeroed the motors, well before the controller's Idle zero.
                yield return PlayModeHarness.WaitFrames(2, 0.6f - (float)(Time.realtimeSinceStartupAsDouble - announced));
                Assert.That(rig.Fake.LastRumbleLow, Is.EqualTo(0f), "rumble low after the terminal pulse");
                Assert.That(rig.Fake.LastRumbleHigh, Is.EqualTo(0f), "rumble high after the terminal pulse");

                yield return PlayModeHarness.WaitUntil(() => !reveal.gameObject.activeSelf, 2.5f, "the catch reveal never hid");
                Assert.That(Time.realtimeSinceStartupAsDouble - announced, Is.LessThan(3.0));
            }

            yield return FishingPlayModeDriver.WaitForState(rig, RodState.Idle, 5f);
            yield return PlayModeHarness.WaitFrames(3, 0.1f);

            // (d) rumble is off at Idle.
            Assert.That(rig.Fake.LastRumbleLow, Is.EqualTo(0f), "rumble low at Idle");
            Assert.That(rig.Fake.LastRumbleHigh, Is.EqualTo(0f), "rumble high at Idle");

            // No loop outlives the fight (AudioBus loops are children named Loop_<id>).

            rig.Log.AssertEmpty("feedback cast-bite-land loop");
        }

        // Catches: captions shown with settings.captions off; a cue still acting after the component is disabled (OnDisable must unsubscribe);
        // and, through the positive controls, that the silence is real and not a harness that cannot see captions or ripples.
        [UnityTest]
        public IEnumerator FeedbackSolo_CaptionsOffAndDisabled_StaySilent()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig, teleport: false);
            FishingRig rig = _rig;

            Assert.That(ServiceRoots.App.TryGet(out EventBus bus), Is.True, "EventBus");
            Assert.That(ServiceRoots.App.TryGet(out ISettings settings), Is.True, "ISettings");
            Behaviour feedback = FindBehaviour(rig.Player.gameObject, "FishingFeedback");

            var captions = new List<string>();
            using (bus.Subscribe<CaptionRequested>(c => captions.Add(c.Key)))
            {
                try
                {
                    // Positive control: enabled, captions on.
                    int nibbles = CountRipples(RippleMath.Nibble);
                    rig.Net.RaiseLocalCue(RodCue.Bite, 0);
                    rig.Net.RaiseLocalCue(RodCue.Nibble, 0);
                    yield return null;
                    Assert.That(captions, Is.EqualTo(new[] { "cap.bite" }), "positive control: the owner's Bite cue raises cap.bite");
                    Assert.That(CountRipples(RippleMath.Nibble), Is.EqualTo(nibbles + 1), "positive control: the Nibble cue spawns a ripple");

                    // Captions off: the ripple still spawns, the caption does not.
                    captions.Clear();
                    settings.Current.captions = false;
                    settings.NotifyChanged();
                    rig.Net.RaiseLocalCue(RodCue.Bite, 0);
                    rig.Net.RaiseLocalCue(RodCue.Snapped, 1);
                    yield return null;
                    Assert.That(captions, Is.Empty, "no caption may be requested while settings.captions is off");

                    // Disabled: nothing at all (captions on again, so only the disable can explain the silence).
                    settings.Current.captions = true;
                    settings.NotifyChanged();
                    feedback.enabled = false;
                    nibbles = CountRipples(RippleMath.Nibble);
                    int bites = CountRipples(RippleMath.Bite);
                    rig.Net.RaiseLocalCue(RodCue.Bite, 0);
                    rig.Net.RaiseLocalCue(RodCue.Nibble, 0);
                    rig.Net.RaiseLocalCue(RodCue.Snapped, 2);
                    yield return PlayModeHarness.WaitFrames(2, 0.05f);
                    Assert.That(captions, Is.Empty, "a disabled FishingFeedback must not request captions");
                    Assert.That(CountRipples(RippleMath.Nibble), Is.EqualTo(nibbles), "a disabled FishingFeedback must not spawn ripples");
                    Assert.That(CountRipples(RippleMath.Bite), Is.EqualTo(bites), "a disabled FishingFeedback must not spawn ripples");
                    Assert.That(rig.Player.transform.Find("SnapSilhouette").gameObject.activeSelf, Is.False, "no snap silhouette while disabled");
                    Assert.That(rig.Fake.LastRumbleLow, Is.EqualTo(0f));
                    Assert.That(rig.Fake.LastRumbleHigh, Is.EqualTo(0f));
                }
                finally
                {
                    settings.Current.captions = true;
                    settings.NotifyChanged();
                }
            }

            rig.Log.AssertEmpty("feedback captions and disable");
        }

        // Catches (T23 carry-forward): a loop started without a follow transform (it would play forever at the bus); loops not stopped when the
        // component is disabled or the fish is gone. The catfish (runner profile: strong pull, dives) fought by reeling only during its bursts and never
        // steering builds tension past 0.6 (Line_Creak) or dives (Dive_Thrum) within a few bursts.
        [UnityTest]
        public IEnumerator FeedbackSolo_Loops_FollowATransformAndStopOnDisable()
        {
            FishingDebugSettings.ForcedBiteWait = 0.1f;
            FishingDebugSettings.ForcedSpeciesId = "catfish";
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig);
            FishingRig rig = _rig;

            Assert.That(ServiceRoots.App.TryGet(out AudioBus audio), Is.True, "AudioBus");
            Behaviour feedback = FindBehaviour(rig.Player.gameObject, "FishingFeedback");
            Transform tip = rig.Player.transform.Find(PlayerRigPaths.RodTipAnchor);
            Transform anchor = rig.Player.transform.Find("FeedbackAnchor");
            Assert.That(anchor, Is.Not.Null, "FishingFeedback creates a FeedbackAnchor child");

            yield return FishingPlayModeDriver.EquipAndReady(rig);
            yield return FishingPlayModeDriver.ChargeAndRelease(rig);
            yield return FishingPlayModeDriver.WaitForState(rig, RodState.Hooked, 8f);

            Transform loop = null;
            float maxTension = 0f;
            double deadline = Time.realtimeSinceStartupAsDouble + 40.0;
            while (loop == null)
            {
                if (rig.Machine.State != RodState.Hooked) Assert.Fail("the fight ended (" + rig.Machine.State + ") before any loop started; max tension " + maxTension);
                if (Time.realtimeSinceStartupAsDouble > deadline) Assert.Fail("no Line_Creak or Dive_Thrum loop started within 40 s of fighting; max tension " + maxTension);
                bool burst = rig.Machine.Fight.State.Phase == FightPhase.Burst;
                maxTension = Mathf.Max(maxTension, rig.Machine.Fight.State.Tension);
                yield return FishingPlayModeDriver.Step(rig.Fake, new PlayerInputFrame { Reel = burst });
                loop = FindLoop(audio);
            }
            rig.Fake.Frame = default;

            yield return null;   // AudioBus.LateUpdate moves the loop to its follow target
            float toTip = Vector3.Distance(loop.position, tip.position);
            float toAnchor = Vector3.Distance(loop.position, anchor.position);
            Assert.That(Mathf.Min(toTip, toAnchor), Is.LessThan(0.05f), "a loop must follow the rod tip or the feedback anchor (" + loop.name + ")");

            feedback.enabled = false;
            yield return PlayModeHarness.WaitFrames(2, 0.05f);
            Assert.That(HasLoop(audio), Is.False, "OnDisable must stop every loop");
            Assert.That(rig.Fake.LastRumbleLow, Is.EqualTo(0f), "rumble low after disable");
            Assert.That(rig.Fake.LastRumbleHigh, Is.EqualTo(0f), "rumble high after disable");

            rig.Log.AssertEmpty("feedback loops");
        }

        // Catches: the reveal missing a material on a submesh (a multi-submesh species model draws invisible parts); the wrong material for a
        // fallback mesh or a model; the scale rule (+Z bounds length = LengthMm / 1000, junk 0.3 m); the pose not at CatchHold; ReleasedCreelFull
        // not dropping and splashing; Lost still showing a fish.
        [UnityTest]
        public IEnumerator FeedbackSolo_Reveal_ModelFallbackAndCreelFull_CoverEverySubmesh()
        {
            _rig = new FishingRig();
            yield return FishingPlayModeDriver.Arrange(_rig, teleport: false);
            FishingRig rig = _rig;

            ushort perch = rig.Registry.SpeciesIndex("perch");
            ushort boot = rig.Registry.JunkIndex("old_boot");
            ushort bluegill = rig.Registry.SpeciesIndex("bluegill");
            FishSpeciesDef perchDef = rig.Registry.SpeciesAt(perch);
            FieldInfo modelField = typeof(FishSpeciesDef).GetField("model", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(modelField, Is.Not.Null, "FishSpeciesDef.model");
            Mesh original = (Mesh)modelField.GetValue(perchDef);
            Mesh twoSubmeshes = MakeTwoSubmeshMesh();
            Transform reveal = rig.Player.transform.Find(RevealName);
            Transform hold = rig.Player.transform.Find(PlayerRigPaths.CatchHold);
            Assert.That(reveal, Is.Not.Null, "no CatchReveal child (CatchRevealView missing)");
            Assert.That(hold, Is.Not.Null, "no CatchHold");

            try
            {
                // A species with a two-submesh model: both submeshes get M_Toon_Fish; scaled so +Z bounds = 0.25 m; held at CatchHold.
                modelField.SetValue(perchDef, twoSubmeshes);
                Announce(rig, new CatchResult { IsJunk = false, ItemIndex = perch, LengthMm = 250, Quality = CatchQuality.Trophy }, AnnounceKind.Landed, true);
                yield return PlayModeHarness.WaitFrames(2, 0.4f);   // past the 0.15 s scale-in
                Assert.That(reveal.gameObject.activeSelf, Is.True, "the model reveal is shown");
                MeshFilter filter = reveal.GetComponent<MeshFilter>();
                MeshRenderer renderer = reveal.GetComponent<MeshRenderer>();
                Assert.That(filter.sharedMesh, Is.SameAs(twoSubmeshes), "the species model is used when it exists");
                Assert.That(renderer.sharedMaterials.Length, Is.EqualTo(2), "one material per submesh");
                foreach (Material m in renderer.sharedMaterials)
                {
                    Assert.That(m, Is.Not.Null, "a null submesh material");
                    Assert.That(m.name, Is.EqualTo("M_Toon_Fish"), "a species model is drawn with M_Toon_Fish");
                }
                Assert.That(twoSubmeshes.bounds.size.z * reveal.lossyScale.z, Is.EqualTo(0.25f).Within(0.01f), "+Z length = LengthMm / 1000");
                Vector3 expected = hold.position + Vector3.up * 0.25f;
                Assert.That(Vector3.Distance(renderer.bounds.center, expected), Is.LessThan(0.1f), "the fish is held at CatchHold");
                yield return PlayModeHarness.WaitUntil(() => !reveal.gameObject.activeSelf, 3f, "the model reveal never hid");

                // Lost and PityCast show nothing.
                Announce(rig, new CatchResult { IsJunk = false, ItemIndex = perch, LengthMm = 250 }, AnnounceKind.Lost, false);
                Announce(rig, default, AnnounceKind.PityCast, false);
                yield return PlayModeHarness.WaitFrames(2, 0.1f);
                Assert.That(reveal.gameObject.activeSelf, Is.False, "Lost and PityCast must not show a reveal");

                // Junk: the fallback mesh with M_Toon_FishFallback at 0.3 m.
                Announce(rig, new CatchResult { IsJunk = true, ItemIndex = boot }, AnnounceKind.Landed, false);
                yield return PlayModeHarness.WaitFrames(2, 0.4f);
                Assert.That(reveal.gameObject.activeSelf, Is.True, "the junk reveal is shown");
                Mesh junkMesh = reveal.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(junkMesh, Is.Not.Null.And.Not.SameAs(twoSubmeshes));
                Material[] junkMaterials = reveal.GetComponent<MeshRenderer>().sharedMaterials;
                Assert.That(junkMaterials.Length, Is.EqualTo(junkMesh.subMeshCount));
                Assert.That(junkMaterials[0].name, Is.EqualTo("M_Toon_FishFallback"), "a fallback mesh is drawn with M_Toon_FishFallback");
                Assert.That(junkMesh.bounds.size.z * reveal.lossyScale.z, Is.EqualTo(0.3f).Within(0.01f), "junk is 0.3 m long");
                yield return PlayModeHarness.WaitUntil(() => !reveal.gameObject.activeSelf, 3f, "the junk reveal never hid");

                // ReleasedCreelFull (a species without a model): held, then falls and splashes with a Landed ripple, then hides.
                int landedRipples = CountRipples(RippleMath.Landed);
                Announce(rig, new CatchResult { IsJunk = false, ItemIndex = bluegill, LengthMm = 180 }, AnnounceKind.ReleasedCreelFull, false);
                yield return null;
                Assert.That(reveal.gameObject.activeSelf, Is.True, "the creel-full reveal is shown");
                float holdY = hold.position.y + 0.25f;
                float lowest = float.MaxValue;
                double deadline = Time.realtimeSinceStartupAsDouble + 4.0;
                while (reveal.gameObject.activeSelf && Time.realtimeSinceStartupAsDouble < deadline)
                {
                    lowest = Mathf.Min(lowest, reveal.GetComponent<MeshRenderer>().bounds.center.y);
                    yield return null;
                }
                Assert.That(reveal.gameObject.activeSelf, Is.False, "the creel-full reveal never hid");
                Assert.That(lowest, Is.LessThan(holdY - 0.3f), "ReleasedCreelFull must drop the fish after the hold");
                Assert.That(CountRipples(RippleMath.Landed), Is.GreaterThan(landedRipples), "the drop splashes with a Landed ripple");
            }
            finally
            {
                modelField.SetValue(rig.Registry.SpeciesAt(perch), original);
                UnityEngine.Object.Destroy(twoSubmeshes);
            }

            rig.Log.AssertEmpty("catch reveal variants");
        }

        // ------------------------------------------------------------------ helpers

        private static void AssertRevealRenders(Transform reveal)
        {
            MeshFilter filter = reveal.GetComponent<MeshFilter>();
            MeshRenderer renderer = reveal.GetComponent<MeshRenderer>();
            Assert.That(renderer, Is.Not.Null, "reveal MeshRenderer");
            Assert.That(renderer.enabled, Is.True, "the reveal MeshRenderer must be enabled");
            Assert.That(filter.sharedMesh, Is.Not.Null, "the reveal needs a mesh");
            Assert.That(renderer.sharedMaterials.Length, Is.EqualTo(filter.sharedMesh.subMeshCount), "one material per submesh");
            foreach (Material m in renderer.sharedMaterials) Assert.That(m, Is.Not.Null, "a null reveal material");
        }

        // AudioBus loops are children named Loop_<id>; a stopped loop's object is destroyed at the end of the frame.
        private static bool HasLoop(AudioBus bus)
        {
            return FindLoop(bus) != null;
        }

        private static Transform FindLoop(AudioBus bus)
        {
            foreach (Transform child in bus.transform)
            {
                if (child.name.StartsWith("Loop_", StringComparison.Ordinal)) return child;
            }
            return null;
        }

        private static bool HasRipple(float strength, Vector3 near, float radius)
        {
            foreach (Vector4 v in Shader.GetGlobalVectorArray(ShaderGlobals.Ripples))
            {
                if (Mathf.Abs(v.w - strength) > 1e-4f) continue;
                if (new Vector2(v.x - near.x, v.y - near.z).magnitude <= radius) return true;
            }
            return false;
        }

        private static int CountRipples(float strength)
        {
            int n = 0;
            foreach (Vector4 v in Shader.GetGlobalVectorArray(ShaderGlobals.Ripples))
            {
                if (Mathf.Abs(v.w - strength) <= 1e-4f) n++;
            }
            return n;
        }

        private static Behaviour FindBehaviour(GameObject root, string typeName)
        {
            foreach (Behaviour b in root.GetComponents<Behaviour>())
            {
                if (b != null && b.GetType().Name == typeName) return b;
            }
            Assert.Fail("PlayerFP has no " + typeName);
            return null;
        }

        // FishingRodNetwork.Announced is a field-like event with no public raiser (it is raised from CatchAnnouncedRpc); reflect its delegate.
        private static void Announce(FishingRig rig, CatchResult result, AnnounceKind kind, bool clean)
        {
            FieldInfo field = typeof(FishingRodNetwork).GetField("Announced", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "FishingRodNetwork.Announced backing field");
            var handler = (Action<CatchAnnouncement>)field.GetValue(rig.Net);
            Assert.That(handler, Is.Not.Null, "nothing is subscribed to Announced (CatchRevealView / FishingFeedback missing)");
            handler(new CatchAnnouncement(rig.Player.Slot, in result, kind, rig.Net.OwnerClientId, clean));
        }

        // Two quads in two submeshes, 0.5 m long on +Z, with vertex colours (a Toon mesh needs the channel).
        private static Mesh MakeTwoSubmeshMesh()
        {
            var mesh = new Mesh { name = "TestTwoSubmeshFish" };
            mesh.vertices = new[]
            {
                new Vector3(-0.1f, 0f, 0f), new Vector3(0.1f, 0f, 0f), new Vector3(0.1f, 0.1f, 0.25f), new Vector3(-0.1f, 0.1f, 0.25f),
                new Vector3(-0.1f, 0.1f, 0.25f), new Vector3(0.1f, 0.1f, 0.25f), new Vector3(0.1f, 0f, 0.5f), new Vector3(-0.1f, 0f, 0.5f),
            };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white, Color.white, Color.white, Color.white, Color.white };
            mesh.subMeshCount = 2;
            mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            mesh.SetTriangles(new[] { 4, 6, 5, 4, 7, 6 }, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
