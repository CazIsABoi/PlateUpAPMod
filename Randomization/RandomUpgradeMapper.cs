using System;
using System.Collections.Generic;
using System.Linq;
using KitchenData;

namespace KitchenPlateupAP
{
    // Experimental randomization of appliance upgrades.
    // - Fully random: any appliance can upgrade into any other appliance (within allowed tags)
    // - Seeded: deterministic per seed
    // - Weighted: prefer lower-tier targets unless overridden
    public static class RandomUpgradeMapper
    {
        // Optional: override weights per appliance ID (target). Higher = more likely
        private static readonly Dictionary<int, float> TargetWeightsOverride = new Dictionary<int, float>()
        {
            // Example: make Teleporter rare and Table common
            // { ApplianceReferences.Teleporter, 0.1f },
            // { ApplianceReferences.TableLarge, 2.0f },
        };

        public static void Apply(GameData data, int seed)
        {
            if (data == null)
            {
                Mod.Logger?.LogWarning("[Randomizer] GameData is null; aborting.");
                return;
            }

            // Collect all appliances
            List<Appliance> all = data.Get<Appliance>().ToList();
            if (all.Count == 0)
            {
                Mod.Logger?.LogWarning("[Randomizer] No appliances found in GameData; nothing to randomize.");
                return;
            }

            Mod.Logger?.LogInfo($"[Randomizer] Starting upgrade randomization. appliances={all.Count}, seed={seed}");

            var rng = new Random(seed);

            // Build weighted pool of upgrade targets (restricted to allowed tags)
            var weightedTargets = BuildWeightedTargets(all);
            if (weightedTargets.Count == 0)
            {
                Mod.Logger?.LogWarning("[Randomizer] Weighted target pool is empty after filtering; aborting.");
                return;
            }
            Mod.Logger?.LogInfo($"[Randomizer] Built weighted pool size={weightedTargets.Count} (unique={weightedTargets.GroupBy(a=>a.ID).Count()})");

            int changedCount = 0;
            int totalAssigned = 0;

            // Reassign upgrades for each appliance
            foreach (var src in all)
            {
                int originalCount = src.Upgrades?.Count ?? 0;
                // Decide number of upgrades to assign (keep original count or at least 1 if it had any)
                int count = Math.Max(1, originalCount);

                // Sample unique targets, avoiding self
                var targets = SampleUnique(weightedTargets, count, rng, t => t.ID != src.ID);

                // Write back
                src.Upgrades = targets;
                foreach (var t in targets)
                    t.IsAnUpgrade = true;

                changedCount++;
                totalAssigned += targets.Count;

                // Log a concise line per appliance (ID and first few target IDs)
                if (changedCount <= 25) // avoid log spam; cap
                {
                    string sample = string.Join(", ", targets.Take(3).Select(x => x.ID));
                    Mod.Logger?.LogInfo($"[Randomizer] {src.ID} -> {targets.Count} upgrade(s) [{sample}{(targets.Count>3?", ...":"")}] ");
                }
            }

            Mod.Logger?.LogInfo($"[Randomizer] Completed. Appliances changed={changedCount}, upgrades assigned total={totalAssigned}");
        }

        private static List<Appliance> BuildWeightedTargets(List<Appliance> all)
        {
            int filteredOut = 0;
            var pool = new List<Appliance>();
            foreach (var a in all)
            {
                if (!IsAllowedTarget(a))
                {
                    filteredOut++;
                    continue;
                }

                float w = GetBaseWeight(a);
                if (w <= 0f) continue;
                int copies = Math.Max(1, (int)Math.Round(w));
                for (int i = 0; i < copies; i++)
                    pool.Add(a);
            }
            Mod.Logger?.LogInfo($"[Randomizer] Allowed target uniques={pool.GroupBy(p => p.ID).Count()} (filtered out {filteredOut}).");
            return pool;
        }

        // Allow-list for “normal” or “decoration” appliances.
        private static bool IsAllowedTarget(Appliance a)
        {
            try
            {
                var tags = a.ShoppingTags;

                // Allowed categories: normal/basic/decoration
                bool allowed =
                    tags.HasFlag(ShoppingTags.None) ||
                    tags.HasFlag(ShoppingTags.Basic) ||
                    tags.HasFlag(ShoppingTags.Misc) ||
                    tags.HasFlag(ShoppingTags.Automation) ||
                    tags.HasFlag(ShoppingTags.BlueprintStore) ||
                    tags.HasFlag(ShoppingTags.Office) ||
                    tags.HasFlag(ShoppingTags.Technology) ||
                    tags.HasFlag(ShoppingTags.Plumbing) ||
                    tags.HasFlag(ShoppingTags.Decoration);

                // Exclude event/seasonal/blueprint/special
                if (tags.HasFlag(ShoppingTags.Halloween) ||
                    tags.HasFlag(ShoppingTags.Christmas) ||
                    tags.HasFlag(ShoppingTags.SpecialEvent) ||
                    tags.HasFlag(ShoppingTags.None))
                {
                    return false;
                }

                return allowed;
            }
            catch
            {
                // If tags differ across versions, default to allowing basic/decoration via name heuristics
                var name = (a?.Name ?? "").ToLowerInvariant();
                if (name.Contains("table") || name.Contains("counter") || name.Contains("decor"))
                    return true;
                return false;
            }
        }

        private static float GetBaseWeight(Appliance a)
        {
            float tierWeight = PriceTierToWeight(a.PriceTier);
            float rarityWeight = RarityToWeight(a.RarityTier);
            float baseW = tierWeight * rarityWeight;

            if (TargetWeightsOverride.TryGetValue(a.ID, out var overrideW))
                baseW = overrideW;

            if (!a.IsPurchasable && !a.IsPurchasableAsUpgrade)
                baseW *= 0.25f;

            try
            {
                if (a.ShoppingTags.HasFlag(ShoppingTags.SpecialEvent))
                    baseW *= 0.5f;
                if (a.ShoppingTags.HasFlag(ShoppingTags.BlueprintStore))
                    baseW *= 0.6f;
            }
            catch { }

            return Math.Max(0.01f, baseW);
        }

        private static float PriceTierToWeight(PriceTier tier)
        {
            switch (tier)
            {
                case PriceTier.Free: return 5f;
                case PriceTier.VeryCheap: return 4f;
                case PriceTier.Cheap: return 3f;
                case PriceTier.MediumCheap: return 2.5f;
                case PriceTier.Medium: return 2f;
                case PriceTier.DecoCheap: return 2.5f;
                case PriceTier.DecoMediumCheap: return 2f;
                case PriceTier.DecoMedium: return 1.5f;
                case PriceTier.DecoExpensive: return 1.25f;
                case PriceTier.Expensive: return 1f;
                case PriceTier.VeryExpensive: return 0.6f;
                case PriceTier.ExtremelyExpensive: return 0.3f;
                default: return 1f;
            }
        }

        private static float RarityToWeight(RarityTier rarity)
        {
            switch (rarity)
            {
                case RarityTier.Common: return 2f;
                case RarityTier.Uncommon: return 1.5f;
                case RarityTier.Rare: return 1f;
                case RarityTier.Special: return 0.5f;
                default: return 1f;
            }
        }

        private static List<Appliance> SampleUnique(List<Appliance> pool, int count, Random rng, Func<Appliance, bool> predicate)
        {
            var result = new List<Appliance>(count);
            if (pool.Count == 0 || count <= 0) return result;

            // Combine caller predicate with allow-list constraint
            bool CombinedPredicate(Appliance a) => predicate(a) && IsAllowedTarget(a);

            var filtered = pool.Where(CombinedPredicate).ToList();
            if (filtered.Count == 0) return result;

            // Fisher-Yates shuffle
            for (int i = filtered.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var tmp = filtered[i];
                filtered[i] = filtered[j];
                filtered[j] = tmp;
            }

            // Add unique by ID
            foreach (var a in filtered)
            {
                if (result.Count >= count) break;
                if (!result.Any(x => x.ID == a.ID))
                    result.Add(a);
            }

            // Top-up from unique set if needed (group by ID)
            if (result.Count < count)
            {
                var uniques = pool
                    .Where(CombinedPredicate)
                    .GroupBy(p => p.ID)
                    .Select(g => g.First())
                    .ToList();

                // Shuffle uniques deterministically
                for (int i = uniques.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    var tmp = uniques[i];
                    uniques[i] = uniques[j];
                    uniques[j] = tmp;
                }

                foreach (var u in uniques)
                {
                    if (result.Count >= count) break;
                    if (!result.Any(x => x.ID == u.ID))
                        result.Add(u);
                }
            }

            return result;
        }
    }
}
