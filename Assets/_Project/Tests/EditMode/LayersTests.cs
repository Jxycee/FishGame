using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class LayersTests
    {
        [Test]
        public void Layers_UserLayerTables_AlignWithConstants()
        {
            int[] expectedIndices =
            {
                Layers.Player, Layers.RemotePlayer, Layers.Ground, Layers.Shore,
                Layers.Structure, Layers.Interactable, Layers.Fish, Layers.Ragdoll
            };
            Assert.That(Layers.UserLayerIndices, Is.EqualTo(expectedIndices));
            Assert.That(Layers.UserLayerNames, Is.EqualTo(new[]
            {
                "Player", "RemotePlayer", "Ground", "Shore", "Structure", "Interactable", "Fish", "Ragdoll"
            }));
            Assert.That(Layers.Water, Is.EqualTo(4));
            Assert.That(Layers.Default, Is.EqualTo(0));
        }

        [Test]
        public void Layers_Mask_ShiftsOneBit()
        {
            Assert.That(Layers.Mask(Layers.Default), Is.EqualTo(1));
            Assert.That(Layers.Mask(Layers.Water), Is.EqualTo(16));
            Assert.That(Layers.Mask(Layers.Ragdoll), Is.EqualTo(8192));
        }
    }
}
