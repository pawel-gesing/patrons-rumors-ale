using System.Collections.Generic;
using PatronsRumorsAle.Content;

namespace PatronsRumorsAle.Simulation
{
    public sealed class SeatingOutcome
    {
        public float StayMultiplier { get; set; } = 1f;
        public float SpendMultiplier { get; set; } = 1f;
        public float IndividualSpendMultiplier { get; set; } = 1f;
        public bool IsGoodSeating { get; set; }
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
            var outcome = new SeatingOutcome();
            var sameFaction = 0;
            var neutralCount = 0;
            var revolutionaryCount = 0;

            for (var i = 0; i < table.Seats.Count; i++)
            {
                var occupantId = table.Seats[i].CustomerId;
                if (!occupantId.HasValue)
                    continue;

                var occupant = state.Customers[occupantId.Value];
                if (occupant.Faction == customer.Faction)
                    sameFaction++;
                if (occupant.Faction == FactionId.Neutrals)
                    neutralCount++;
                if (occupant.Faction == FactionId.Revolutionaries)
                    revolutionaryCount++;
            }

            if (customer.Faction == FactionId.Sarmatians && sameFaction > 0)
            {
                outcome.StayMultiplier += balance.sarmatianCompanionStayBonus * sameFaction;
                outcome.IndividualSpendMultiplier += balance.sarmatianCompanionSpendBonus * sameFaction;
                outcome.IsGoodSeating = true;
                outcome.ActiveBonuses.Add("Sarmatian companions");
            }

            if (customer.Faction == FactionId.Revolutionaries && neutralCount + revolutionaryCount > 0)
            {
                outcome.StayMultiplier += balance.revolutionaryAudienceStayBonus;
                outcome.IsGoodSeating = true;
                outcome.ActiveBonuses.Add("Revolutionary audience");
            }

            var moonshinersAfterSeating = CountActiveMoonshiners(state);
            if (customer.Faction == FactionId.Moonshiners)
                moonshinersAfterSeating++;
            if (moonshinersAfterSeating > 0)
            {
                outcome.ActiveBonuses.Add("Moonshiner trade");
            }

            outcome.SpendMultiplier = outcome.IndividualSpendMultiplier *
                (1f + moonshinersAfterSeating * balance.moonshinerGlobalSpendBonus);
            if (outcome.IsGoodSeating)
                outcome.ReputationDelta = balance.goodSeatingReputationReward;
            return outcome;
        }

        public float GetMoonshinerSpendMultiplier(GameState state)
        {
            return 1f + CountActiveMoonshiners(state) * balance.moonshinerGlobalSpendBonus;
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
