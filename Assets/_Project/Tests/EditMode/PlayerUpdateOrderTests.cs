// Authority: none (pure EditMode tests). Per-frame script order of the owner player components, read from [DefaultExecutionOrder] by reflection
// (no attribute = 0). Spec 8.2: a ladder entry must flip the context to Climbing before PlayerInteractor looks at it.
using System;
using FishGame.Fishing;
using FishGame.Gameplay;
using FishGame.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace FishGame.Tests.EditMode
{
    public class PlayerUpdateOrderTests
    {
        private static int OrderOf(Type type)
        {
            var attribute = (DefaultExecutionOrder)Attribute.GetCustomAttribute(type, typeof(DefaultExecutionOrder), true);
            return attribute != null ? attribute.order : 0;
        }

        // Catches: PlayerInteractor updating at the same order as (or before) PlayerLocomotion: on the frame a player walks into a ladder the context
        // is still Free when an early interactor runs, so an E press on that frame could reach the bell. Defensive since R70 (no press climbs).
        [Test]
        public void PlayerInteractor_RunsAfterPlayerLocomotion()
        {
            Assert.That(OrderOf(typeof(PlayerInteractor)), Is.GreaterThan(OrderOf(typeof(PlayerLocomotion))),
                "PlayerInteractor must update after PlayerLocomotion so a climb started this frame never reaches an interactable");
        }

        // Catches: the documented chain drifting (input adapter -1000, rod -100, locomotion 0, interactor 100, body 200).
        [Test]
        public void PlayerComponents_UpdateOrderChain_IsAdapterRodLocomotionInteractorBody()
        {
            int adapter = OrderOf(typeof(PlayerInputAdapter));
            int rod = OrderOf(typeof(FishingRodController));
            int locomotion = OrderOf(typeof(PlayerLocomotion));
            int interactor = OrderOf(typeof(PlayerInteractor));
            int body = OrderOf(typeof(PlayerBody));
            Assert.That(adapter, Is.LessThan(rod), "input adapter before rod controller");
            Assert.That(rod, Is.LessThan(locomotion), "rod controller before locomotion");
            Assert.That(locomotion, Is.LessThan(interactor), "locomotion before interactor");
            Assert.That(interactor, Is.LessThan(body), "interactor before body");
        }
    }
}
