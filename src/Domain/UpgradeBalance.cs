using System;
using System.Collections.Generic;

namespace CartCompressor.Domain
{
    public static class UpgradeBalance
    {
        public const int MaxLevel = 3;
        public static float Factor(int level)
        {
            if (level <= 0) return 1f;
            if (level == 1) return 1f - .35f / 1.5f;
            if (level == 2) return 1f - (.35f / 1.5f + .35f) / 2f;
            return .65f;
        }
    }

    public sealed class CartAttunement
    {
        private readonly Dictionary<int, string> owners = new Dictionary<int, string>();
        public int Observe(int cart, IEnumerable<string> handlers, Func<string, int> level)
        {
            string chosen = null;
            int minimum = UpgradeBalance.MaxLevel;
            bool invalid = false;
            foreach (string id in handlers)
            {
                int rank = string.IsNullOrEmpty(id) ? 0 : Math.Min(UpgradeBalance.MaxLevel, level(id));
                if (rank <= 0) { invalid = true; continue; }
                if (chosen == null || rank < minimum || (rank == minimum && StringComparer.Ordinal.Compare(id, chosen) < 0))
                { chosen = id; minimum = rank; }
            }
            if (invalid) { owners.Remove(cart); return 0; }
            if (chosen != null) owners[cart] = chosen;
            if (!owners.TryGetValue(cart, out string owner)) return 0;
            int result = Math.Min(UpgradeBalance.MaxLevel, level(owner));
            if (result <= 0) owners.Remove(cart);
            return Math.Max(0, result);
        }
        public void Remove(int cart) => owners.Remove(cart);
        public void Clear() => owners.Clear();
    }
}
