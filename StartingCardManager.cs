using System;
using System.Collections.Generic;

namespace KitchenPlateupAP
{
    /// <summary>
    /// Manages the pool of permanent starting cards based on slot_data settings.
    /// Cards are removed deterministically using a seeded RNG so the state can be
    /// reconstructed from just the remove-card count.
    /// </summary>
    public static class StartingCardManager
    {
        // slot_data values
        private static int _startingCardsMode = 0;   // 0=none, 1=easy, 2=hard, 3=both
        private static int _startingCardsAmount = 0;  // 1–8 (ignored when mode=0)
        private static int _removeCardCount = 0;
        private static int _seed = 0;

        // Extra always-on cards from slot_data extra_starting_cards
        private static readonly List<int> _extraStartingCards = new List<int>();

        // Queue of card GDO IDs that were removed mid-run and need entity cleanup
        private static readonly Queue<int> _pendingRemovals = new Queue<int>();

        /// <summary>
        /// Initialise from slot_data. Call once after connecting.
        /// </summary>
        public static void Initialise(int mode, int amount, int seed)
        {
            _startingCardsMode = mode;
            _startingCardsAmount = Math.Max(0, Math.Min(amount, 8));
            _removeCardCount = 0;
            _seed = seed;
            _pendingRemovals.Clear();

            Mod.Logger?.LogInfo($"[StartingCardManager] Initialised: mode={mode}, amount={amount}, seed={seed}");
        }

        /// <summary>
        /// Sets the extra always-on cards sourced from slot_data extra_starting_cards.
        /// Call after Initialise, once the slot_data list has been resolved to GDO IDs.
        /// </summary>
        public static void SetExtraStartingCards(IEnumerable<int> unlockCardGDOs)
        {
            _extraStartingCards.Clear();
            foreach (int id in unlockCardGDOs)
            {
                if (id != 0 && !_extraStartingCards.Contains(id))
                    _extraStartingCards.Add(id);
            }
            Mod.Logger?.LogInfo($"[StartingCardManager] Extra starting cards set: [{string.Join(", ", _extraStartingCards)}]");
        }

        /// <summary>
        /// Call each time a "Remove Card" item (ID 21) is received.
        /// Computes which card is removed and queues it for immediate entity cleanup.
        /// </summary>
        public static void ApplyRemoveCard()
        {
            // Compute the card list BEFORE this removal to find which card gets removed
            List<int> cardsBefore = ComputeActiveCards(_removeCardCount);

            _removeCardCount++;

            // Compute the card list AFTER this removal
            List<int> cardsAfter = ComputeActiveCards(_removeCardCount);

            // The removed card is in cardsBefore but not in cardsAfter
            var afterSet = new HashSet<int>(cardsAfter);
            foreach (int gdoId in cardsBefore)
            {
                if (!afterSet.Contains(gdoId))
                {
                    _pendingRemovals.Enqueue(gdoId);
                    Mod.Logger?.LogInfo($"[StartingCardManager] Remove Card received. Queued GDO {gdoId} ({GetCardName(gdoId)}) for immediate removal.");
                    break;
                }
            }

            Mod.Logger?.LogInfo($"[StartingCardManager] Total removals: {_removeCardCount}");
        }

        /// <summary>
        /// Reconstruct the remove count from item history (e.g. on reconnect).
        /// Does NOT queue pending removals since cards haven't been spawned yet.
        /// </summary>
        public static void SetRemoveCount(int count)
        {
            _removeCardCount = Math.Max(0, count);
            _pendingRemovals.Clear();
            Mod.Logger?.LogInfo($"[StartingCardManager] Remove count set to {_removeCardCount}");
        }

        /// <summary>
        /// Returns true if there are card GDO IDs waiting to be destroyed mid-run.
        /// </summary>
        public static bool HasPendingRemovals => _pendingRemovals.Count > 0;

        /// <summary>
        /// Dequeues the next card GDO ID that needs its entity destroyed.
        /// </summary>
        public static int DequeuePendingRemoval() => _pendingRemovals.Dequeue();

        /// <summary>
        /// Returns the GDO IDs of the cards that should be active this run.
        /// Fully deterministic — safe to call multiple times, across runs/reconnects.
        /// Includes extra always-on cards from slot_data extra_starting_cards.
        /// </summary>
        public static List<int> GetActiveStartingCards()
        {
            var cards = ComputeActiveCards(_removeCardCount);

            // Append extra always-on cards (deduped), they are not subject to removal
            foreach (int id in _extraStartingCards)
            {
                if (!cards.Contains(id))
                    cards.Add(id);
            }

            return cards;
        }

        /// <summary>
        /// How many starting cards are still active (after removals).
        /// </summary>
        public static int ActiveCount => GetActiveStartingCards().Count;

        /// <summary>
        /// Whether the feature is active at all.
        /// </summary>
        public static bool IsEnabled => (_startingCardsMode != 0 && _startingCardsAmount > 0) || _extraStartingCards.Count > 0;

        /// <summary>
        /// Computes the active card list for a given number of removals.
        /// Does NOT include extra starting cards — call GetActiveStartingCards() for the full list.
        /// </summary>
        private static List<int> ComputeActiveCards(int removeCount)
        {
            if (_startingCardsMode == 0 || _startingCardsAmount <= 0)
                return new List<int>();

            List<int> pool = BuildCardPool();
            if (pool.Count == 0)
                return new List<int>();

            List<int> dealt = DeterministicPick(pool, _startingCardsAmount, _seed);

            int removals = Math.Min(removeCount, dealt.Count);
            for (int i = 0; i < removals; i++)
            {
                var rng = new System.Random(_seed ^ (0x7F3A + i));
                int idx = rng.Next(dealt.Count);
                dealt.RemoveAt(idx);
            }

            return dealt;
        }

        private static List<int> BuildCardPool()
        {
            var pool = new List<int>();

            if (_startingCardsMode == 1 || _startingCardsMode == 3)
            {
                foreach (var kv in ProgressionMapping.easydifficultCardDictionary)
                    pool.Add(kv.Value);
            }

            if (_startingCardsMode == 2 || _startingCardsMode == 3)
            {
                foreach (var kv in ProgressionMapping.difficultCardDictionary)
                    pool.Add(kv.Value);
            }

            return pool;
        }

        private static List<int> DeterministicPick(List<int> source, int count, int seed)
        {
            var shuffled = new List<int>(source);
            var rng = new System.Random(seed ^ 0xCAFE);

            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int tmp = shuffled[i];
                shuffled[i] = shuffled[j];
                shuffled[j] = tmp;
            }

            int take = Math.Min(count, shuffled.Count);
            return shuffled.GetRange(0, take);
        }

        private static string GetCardName(int gdoId)
        {
            foreach (var kv in ProgressionMapping.easydifficultCardDictionary)
                if (kv.Value == gdoId) return $"Easy#{kv.Key}";
            foreach (var kv in ProgressionMapping.difficultCardDictionary)
                if (kv.Value == gdoId) return $"Hard#{kv.Key}";
            foreach (var kv in ProgressionMapping.allCustomerCards)
                if (kv.Value == gdoId) return $"Extra#{kv.Key}";
            return gdoId.ToString();
        }
    }
}