using System;

namespace KitchenPlateupAP
{
    // Represents the connection and seed used to scope local run state.
    [Serializable]
    public class RunIdentity
    {
        public string Address;
        public int Port;
        public string Player;
        public string Seed;

        /// <summary>
        /// Stable namespace for local run state. The endpoint is deliberately
        /// excluded so reconnecting through a different host or port preserves
        /// the same seed's progress.
        /// </summary>
        public string PersistenceKey => $"{Seed}_{Player}";

        public override string ToString() => $"{Address}_{Port}_{Player}_{Seed}";
    }
}
