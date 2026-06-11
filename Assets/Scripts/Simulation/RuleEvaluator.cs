using System.Collections.Generic;
using PatronsRumorsAle.Content;

namespace PatronsRumorsAle.Simulation
{
    public sealed class SeatingOutcome
    {
        public string TableId { get; set; }
        public int CurrentCustomerCount { get; set; }
        public int FreeSeats { get; set; }
        public List<FactionId> PresentFactions { get; } = new List<FactionId>();
        public float StayMultiplier { get; set; } = 1f;
        public float SpendMultiplier { get; set; } = 1f;
        public float IndividualSpendMultiplier { get; set; } = 1f;
        public bool IsGoodSeating { get; set; }
        public bool IsNeutralPlacement { get; set; }
        public float ReputationDelta { get; set; }
        public List<string> ActiveBonuses { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
    }

    public sealed class RuleEvaluator
    {
        private readonly BalanceDefinition balance;

        public RuleEvaluator(BalanceDefinition balance) => this.balance = balance;

        public SeatingOutcome Evaluate(CustomerInstance customer, TableState table, GameState state)
        {
            var outcome = new SeatingOutcome
            {
                TableId = table.Id,
                CurrentCustomerCount = table.OccupiedSeatCount,
                FreeSeats = table.FreeSeatCount
            };
            var sameFaction = 0;
            var neutralCount = 0;
            var revolutionaryCount = 0;
            var factions = new HashSet<FactionId>();

            for (var i = 0; i < table.Seats.Count; i++)
            {
                var occupantId = table.Seats[i].CustomerId;
                if (!occupantId.HasValue)
                    continue;

                var occupant = state.Customers[occupantId.Value];
                factions.Add(occupant.Faction);
                if (occupant.Faction == customer.Faction)
                    sameFaction++;
                if (occupant.Faction == FactionId.Neutrals)
                    neutralCount++;
                if (occupant.Faction == FactionId.Revolutionaries)
                    revolutionaryCount++;
            }
            outcome.PresentFactions.AddRange(factions);

            if (customer.Faction == FactionId.Sarmatians && sameFaction > 0)
            {
                outcome.StayMultiplier += balance.sarmatianCompanionStayBonus * sameFaction;
                outcome.IndividualSpendMultiplier += balance.sarmatianCompanionSpendBonus * sameFaction;
                outcome.IsGoodSeating = true;
                outcome.ActiveBonuses.Add("Sarmatian companions");
            }

            if (customer.Faction == FactionId.Revolutionaries && neutralCount > 0)
            {
                outcome.StayMultiplier += balance.revolutionaryNeutralAudienceStayBonus;
                outcome.IsGoodSeating = true;
                outcome.ActiveBonuses.Add("Neutral audience");
            }
            else if (customer.Faction == FactionId.Revolutionaries && revolutionaryCount > 0)
            {
                outcome.StayMultiplier += balance.revolutionaryCompanionStayBonus;
                outcome.IsGoodSeating = true;
                outcome.ActiveBonuses.Add("Revolutionary companions");
            }

            var moonshinersAfterSeating = CountActiveMoonshiners(state);
            if (customer.Faction == FactionId.Moonshiners)
                moonshinersAfterSeating++;
            if (moonshinersAfterSeating > 0)
            {
                outcome.ActiveBonuses.Add("Moonshiner trade");
            }

            var moonshinerBonus = System.Math.Min(
                moonshinersAfterSeating * balance.moonshinerGlobalSpendBonus,
                balance.moonshinerGlobalSpendBonusCap);
            outcome.SpendMultiplier = outcome.IndividualSpendMultiplier * (1f + moonshinerBonus);
            if (outcome.IsGoodSeating)
                outcome.ReputationDelta = balance.goodSeatingReputationReward;
            else
            {
                outcome.IsNeutralPlacement = true;
                if (customer.Faction == FactionId.Sarmatians ||
                    customer.Faction == FactionId.Revolutionaries)
                    outcome.ReputationDelta = -balance.unmetFactionExpectationPenalty;
            }
            return outcome;
        }

        public float GetMoonshinerSpendMultiplier(GameState state)
        {
            var bonus = CountActiveMoonshiners(state) * balance.moonshinerGlobalSpendBonus;
            return 1f + System.Math.Min(bonus, balance.moonshinerGlobalSpendBonusCap);
        }

        public int CountActiveMoonshiners(GameState state)
        {
            var activeMoonshiners = 0;
            foreach (var customer in state.Customers.Values)
            {
                if (customer.Location == CustomerLocation.Table && customer.Faction == FactionId.Moonshiners)
                    activeMoonshiners++;
            }
            return activeMoonshiners;
        }
    }
}
