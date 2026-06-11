using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PatronsRumorsAle.Content
{
    public static class ContentLoader
    {
        public static ContentDatabase LoadFromStreamingAssets()
            => LoadFromDirectory(Path.Combine(Application.streamingAssetsPath, "content"));

        public static ContentDatabase LoadFromDirectory(string directory)
        {
            var factions = Read<FactionCollection>(directory, "factions.json");
            var archetypes = Read<ArchetypeCollection>(directory, "archetypes.json");
            var traits = Read<TraitCollection>(directory, "traits.json");
            var days = Read<CampaignDayCollection>(directory, "campaign_days.json");
            var balance = Read<BalanceDefinition>(directory, "balance.json");

            var errors = Validate(factions, archetypes, days, balance);
            if (errors.Count > 0)
                throw new InvalidDataException("Invalid game content:\n- " + string.Join("\n- ", errors));

            return new ContentDatabase(
                factions.factions,
                archetypes.archetypes,
                traits.traits,
                days.days,
                balance);
        }

        private static T Read<T>(string directory, string fileName)
        {
            var path = Path.Combine(directory, fileName);
            if (!File.Exists(path))
                throw new FileNotFoundException($"Required content file is missing: {path}", path);

            var result = JsonUtility.FromJson<T>(File.ReadAllText(path));
            if (result == null)
                throw new InvalidDataException($"Could not parse JSON file: {path}");
            return result;
        }

        private static List<string> Validate(
            FactionCollection factions,
            ArchetypeCollection archetypes,
            CampaignDayCollection days,
            BalanceDefinition balance)
        {
            var errors = new List<string>();
            var factionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var faction in factions.factions)
            {
                if (string.IsNullOrWhiteSpace(faction.id))
                    errors.Add("Faction id cannot be empty.");
                else if (!factionIds.Add(faction.id))
                    errors.Add($"Duplicate faction id '{faction.id}'.");
            }

            if (factionIds.Count != 4)
                errors.Add("Exactly four factions are required in v0.1.");

            var archetypeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var archetype in archetypes.archetypes)
            {
                if (string.IsNullOrWhiteSpace(archetype.id) || !archetypeIds.Add(archetype.id))
                    errors.Add($"Invalid or duplicate archetype id '{archetype.id}'.");
                if (!factionIds.Contains(archetype.factionId))
                    errors.Add($"Archetype '{archetype.id}' references unknown faction '{archetype.factionId}'.");
                if (archetype.patienceSeconds <= 0f || archetype.staySeconds <= 0f || archetype.baseSpend < 0)
                    errors.Add($"Archetype '{archetype.id}' has invalid patience, stay time, or spend.");
            }

            if (archetypes.archetypes.Count == 0)
                errors.Add("At least one archetype is required.");

            var dayIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var day in days.days)
            {
                if (string.IsNullOrWhiteSpace(day.id) || !dayIds.Add(day.id))
                    errors.Add($"Invalid or duplicate day id '{day.id}'.");
                if (day.durationSeconds <= 0f || day.arrivalIntervalSeconds <= 0f ||
                    day.startingCustomers < 0 || day.moneyGoal < 0)
                    errors.Add($"Day '{day.id}' has invalid duration, starting queue, or goal.");
                if (day.tables == null || day.tables.Count == 0)
                    errors.Add($"Day '{day.id}' must define at least one table.");
                else
                {
                    var tableIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var table in day.tables)
                    {
                        if (string.IsNullOrWhiteSpace(table.id) || !tableIds.Add(table.id) || table.seats <= 0)
                            errors.Add($"Day '{day.id}' contains an invalid table.");
                    }
                }

                var weightedFactions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var totalFactionWeight = 0f;
                foreach (var factionWeight in day.factionArrivalWeights)
                {
                    if (!factionIds.Contains(factionWeight.factionId) ||
                        !weightedFactions.Add(factionWeight.factionId) ||
                        factionWeight.weight <= 0f)
                        errors.Add($"Day '{day.id}' contains an invalid faction arrival weight.");
                    totalFactionWeight += factionWeight.weight;
                }
                if (totalFactionWeight <= 0f)
                    errors.Add($"Day '{day.id}' must define faction arrival weights.");

                var groupSizes = new HashSet<int>();
                var totalGroupWeight = 0f;
                foreach (var groupWeight in day.groupSizeWeights)
                {
                    if (groupWeight.size <= 0 || !groupSizes.Add(groupWeight.size) || groupWeight.weight <= 0f)
                        errors.Add($"Day '{day.id}' contains an invalid group size weight.");
                    totalGroupWeight += groupWeight.weight;
                }
                if (totalGroupWeight <= 0f)
                    errors.Add($"Day '{day.id}' must define group size weights.");
            }

            if (balance.reputationDriftPerSecond < 0f ||
                balance.impatientReputationPenalty < 0f ||
                balance.rejectionReputationPenalty < 0f ||
                balance.goodSeatingReputationReward < 0f ||
                balance.longStayReputationReward < 0f ||
                balance.sarmatianCompanionStayBonus < 0f ||
                balance.sarmatianCompanionSpendBonus < 0f ||
                balance.revolutionaryAudienceStayBonus < 0f ||
                balance.moonshinerGlobalSpendBonus < 0f)
                errors.Add("Balance values cannot be negative.");

            return errors;
        }
    }
}
