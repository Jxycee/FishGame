using System;
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    // RodCue -> SFX id map (C 7.11, spec 9.11). Pure Core.
    public class RodCueAudioTests
    {
        // Catches: a wrong id for any cue (the full C 7.11 map is pinned row by row), a literal that is not a known recipe,
        // a sound for None / Calm, a sound for an out-of-range cue value.
        [Test]
        public void RodCueAudio_EveryMappedCue_HasKnownSfxId()
        {
            var expected = new System.Collections.Generic.Dictionary<RodCue, string>
            {
                { RodCue.Equip, SfxIds.RodThunk }, { RodCue.Stow, SfxIds.RodThunk }, { RodCue.Ready, SfxIds.ReelClick },
                { RodCue.ChargeStart, SfxIds.CastWhoosh }, { RodCue.CastRelease, SfxIds.CastWhoosh },
                { RodCue.InvalidRelease, SfxIds.RodThunk }, { RodCue.Landing, SfxIds.BitePlop }, { RodCue.Recall, SfxIds.RodThunk },
                { RodCue.Nibble, SfxIds.NibbleTick }, { RodCue.Bite, SfxIds.SplashBig }, { RodCue.HookSet, SfxIds.ReelClick },
                { RodCue.SideRun, SfxIds.SplashSmall }, { RodCue.Dive, SfxIds.DiveThrum }, { RodCue.Exhausted, SfxIds.SplashSmall },
                { RodCue.Snapped, SfxIds.Snap }, { RodCue.Escaped, SfxIds.SplashSmall }, { RodCue.LineLanded, SfxIds.CatchFlop },
            };
            Assert.AreEqual(17, expected.Count, "the C 7.11 map has 17 sounding cues");

            foreach (RodCue cue in Enum.GetValues(typeof(RodCue)))
            {
                string id = RodCueAudio.SfxFor(cue);
                if (cue == RodCue.None || cue == RodCue.Calm)
                {
                    Assert.IsNull(id, cue + " is silent");
                    continue;
                }
                Assert.IsNotNull(id, cue + " must have a sound");
                Assert.IsTrue(SfxRecipes.TryGet(id, out _), cue + " -> '" + id + "' is not a known recipe id");
                Assert.AreEqual(expected[cue], id, cue.ToString());
            }
            Assert.IsNull(RodCueAudio.SfxFor((RodCue)200), "an unknown cue value is silent");
        }

        // Catches: IsStinger true for the wrong cues (a stinger ducks ambience, so Nibble or Escaped must not), or missing Bite / Snapped.
        [Test]
        public void RodCueAudio_BiteAndSnap_AreStingers()
        {
            foreach (RodCue cue in Enum.GetValues(typeof(RodCue)))
            {
                bool want = cue == RodCue.Bite || cue == RodCue.Snapped;
                Assert.AreEqual(want, RodCueAudio.IsStinger(cue), cue.ToString());
            }
            Assert.IsFalse(RodCueAudio.IsStinger((RodCue)200));
        }
    }
}
