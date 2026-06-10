using PatronsRumorsAle.Content;

namespace PatronsRumorsAle.Simulation
{
    public sealed class SeatingOutcome
    {
        public float StayMultiplier { get; set; } = 1f;
        public float SpendMultiplier { get; set; } = 1f;
        public bool IsGoodSeating { get; set; }
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
                outcome.SpendMultiplier += balance.sarmatianCompanionSpendBonus * sameFaction;
                outcome.IsGoodSeating = true;
            }

            if (customer.Faction == FactionId.Revolutionaries && neutralCount + revolutionaryCount > 0)
            {
                outcome.StayMultiplier += balance.revolutionaryAudienceStayBonus;
                outcome.IsGoodSeating = true;
            }

            return outcome;
        }

        public float GetMoonshinerSpendMultiplier(GameState state)
        {
            var activeMoonshiners = 0;
            foreach (var customer in state.Customers.Values)
            {
                if (customer.Location == CustomerLocation.Table && customer.Faction == FactionId.Moonshiners)
                    activeMoonshiners++;
            }

            return 1f + activeMoonshiners * balance.moonshinerGlobalSpendBonus;
        }
    }
}

