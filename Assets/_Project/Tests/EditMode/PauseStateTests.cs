using System.Collections.Generic;
using FishGame.Runtime;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class PauseStateTests
    {
        [Test]
        public void PauseState_SetPaused_RaisesChangedOnlyOnRealChange()
        {
            IPauseState pause = new PauseState();
            var seen = new List<bool>();
            pause.Changed += p => seen.Add(p);

            Assert.IsFalse(pause.Paused);
            pause.SetPaused(false); // no change
            pause.SetPaused(true);
            pause.SetPaused(true);  // no change
            pause.SetPaused(false);

            CollectionAssert.AreEqual(new[] { true, false }, seen);
            Assert.IsFalse(pause.Paused);
        }
    }
}
