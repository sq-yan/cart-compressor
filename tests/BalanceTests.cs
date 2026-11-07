using System.Collections.Generic;
using CartCompressor.Domain;
using NUnit.Framework;

namespace CartCompressor.Tests
{
    public class BalanceTests
    {
        [TestCase(-1, 1)] [TestCase(0, 1)]
        [TestCase(1, .7666667)] [TestCase(2, .7083333)]
        [TestCase(3, .65)] [TestCase(4, .65)] [TestCase(100, .65)]
        public void FactorTable(int level, double expected) => Assert.That(UpgradeBalance.Factor(level), Is.EqualTo(expected).Within(.000001));
        [Test] public void FirstLevelHasOneAndHalfTimesWeakerReduction() =>
            Assert.That((1 - UpgradeBalance.Factor(3)) / (1 - UpgradeBalance.Factor(1)), Is.EqualTo(1.5).Within(.00001));
        [Test] public void SecondLevelIsBetweenFirstAndThird()
        {
            Assert.That(UpgradeBalance.Factor(2), Is.LessThan(UpgradeBalance.Factor(1)));
            Assert.That(UpgradeBalance.Factor(2), Is.GreaterThan(UpgradeBalance.Factor(3)));
        }
    }
    public class AttunementTests
    {
        private CartAttunement carts;
        private Dictionary<string, int> levels;
        private int Rank(string id) => levels.TryGetValue(id, out int n) ? n : 0;
        private int Observe(int id, params string[] holders) => carts.Observe(id, holders, Rank);
        [SetUp] public void Setup() { carts = new CartAttunement(); levels = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2, ["c"] = 3 }; }
        [Test] public void UntrainedPlayerCannotActivate() => Assert.That(Observe(1, "x"), Is.Zero);
        [Test] public void TierBelongsOnlyToConsumer() { Assert.That(Observe(1, "a"), Is.EqualTo(1)); Assert.That(Observe(2, "c"), Is.EqualTo(3)); }
        [Test] public void ParkedCartRetainsTier() { Observe(1, "b"); Assert.That(Observe(1), Is.EqualTo(2)); }
        [TestCase("a", "c")] [TestCase("c", "a")]
        public void WeakestHolderWinsRegardlessOfOrder(string a, string b) => Assert.That(Observe(1, a, b), Is.EqualTo(1));
        [TestCase("x", "c")] [TestCase("c", "x")]
        public void UntrainedHolderAlwaysCancels(string a, string b) { Assert.That(Observe(1, a, b), Is.Zero); Assert.That(Observe(1), Is.Zero); }
        [Test] public void UnknownIdentityFailsClosed() => Assert.That(Observe(1, "c", null), Is.Zero);
        [Test] public void DeathOrDisconnectRevokesParkedCart() { Observe(1, "a"); levels.Remove("a"); Assert.That(Observe(1), Is.Zero); }
        [Test] public void SecondAndThirdConsumptionUpdateParkedCart() { Observe(1, "a"); levels["a"] = 2; Assert.That(Observe(1), Is.EqualTo(2)); levels["a"] = 3; Assert.That(Observe(1), Is.EqualTo(3)); }
        [Test] public void AnotherUpgradedPlayerCanTakeOver() { Observe(1, "c"); Assert.That(Observe(1, "a"), Is.EqualTo(1)); }
        [Test] public void OldSaveWithHighLevelIsCapped() { levels["a"] = 40; Assert.That(Observe(1, "a"), Is.EqualTo(3)); }
        [Test] public void DestroyedCartDoesNotLeakState() { Observe(1, "a"); carts.Remove(1); Assert.That(Observe(1), Is.Zero); }
        [Test] public void ResetClearsAllCarts() { Observe(1, "a"); Observe(2, "b"); carts.Clear(); Assert.That(Observe(1), Is.Zero); Assert.That(Observe(2), Is.Zero); }
    }
}
