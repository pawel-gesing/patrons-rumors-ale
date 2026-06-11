using System;
using System.Collections.Generic;

namespace PatronsRumorsAle.Content
{
    public enum FactionId
    {
        Sarmatians,
        Moonshiners,
        Revolutionaries,
        Neutrals
    }

    [Serializable]
    public sealed class FactionDefinition
    {
        public string id;
        public string displayName;
        public string color;
    }

    [Serializable]
    public sealed class FactionCollection
    {
        public List<FactionDefinition> factions = new List<FactionDefinition>();
    }

    [Serializable]
    public sealed class ArchetypeDefinition
    {
        public string id;
        public string displayName;
        public string factionId;
        public float patienceSeconds;
        public float staySeconds;
        public int baseSpend;
    }

    [Serializable]
    public sealed class ArchetypeCollection
    {
        public List<ArchetypeDefinition> archetypes = new List<ArchetypeDefinition>();
    }

    [Serializable]
    public sealed class TraitDefinition
    {
        public string id;
        public string displayName;
    }

    [Serializable]
    public sealed class TraitCollection
    {
        public List<TraitDefinition> traits = new List<TraitDefinition>();
    }

    [Serializable]
    public sealed class TableDefinition
    {
        public string id;
        public int seats;
    }

    [Serializable]
    public sealed class FactionWeightDefinition
    {
        public string factionId;
        public float weight;
    }

    [Serializable]
    public sealed class GroupSizeWeightDefinition
    {
        public int size;
        public float weight;
    }

    [Serializable]
    public sealed class DayDefinition
    {
        public string id;
        public string displayName;
        public float durationSeconds;
        public float arrivalIntervalSeconds;
        public int startingCustomers;
        public int moneyGoal;
        public List<TableDefinition> tables = new List<TableDefinition>();
        public List<FactionWeightDefinition> factionArrivalWeights = new List<FactionWeightDefinition>();
        public List<GroupSizeWeightDefinition> groupSizeWeights = new List<GroupSizeWeightDefinition>();
    }

    [Serializable]
    public sealed class CampaignDayCollection
    {
        public List<DayDefinition> days = new List<DayDefinition>();
    }

    [Serializable]
    public sealed class BalanceDefinition
    {
        public float reputationDriftPerSecond;
        public float impatientReputationPenalty;
        public float rejectionReputationPenalty;
        public float goodSeatingReputationReward;
        public float longStayReputationReward;
        public float sarmatianCompanionStayBonus;
        public float sarmatianCompanionSpendBonus;
        public float revolutionaryAudienceStayBonus;
        public float moonshinerGlobalSpendBonus;
    }

    public sealed class ContentDatabase
    {
        public IReadOnlyList<FactionDefinition> Factions { get; }
        public IReadOnlyList<ArchetypeDefinition> Archetypes { get; }
        public IReadOnlyList<TraitDefinition> Traits { get; }
        public IReadOnlyList<DayDefinition> Days { get; }
        public BalanceDefinition Balance { get; }

        public ContentDatabase(
            IReadOnlyList<FactionDefinition> factions,
            IReadOnlyList<ArchetypeDefinition> archetypes,
            IReadOnlyList<TraitDefinition> traits,
            IReadOnlyList<DayDefinition> days,
            BalanceDefinition balance)
        {
            Factions = factions;
            Archetypes = archetypes;
            Traits = traits;
            Days = days;
            Balance = balance;
        }

        public ArchetypeDefinition GetArchetype(string id)
        {
            for (var i = 0; i < Archetypes.Count; i++)
            {
                if (Archetypes[i].id == id)
                    return Archetypes[i];
            }

            throw new InvalidOperationException($"Unknown archetype '{id}'.");
        }

        public DayDefinition GetDay(string id)
        {
            for (var i = 0; i < Days.Count; i++)
            {
                if (Days[i].id == id)
                    return Days[i];
            }

            throw new InvalidOperationException($"Unknown day '{id}'.");
        }
    }
}
