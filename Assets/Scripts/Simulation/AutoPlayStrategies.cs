using System;
using PatronsRumorsAle.Content;

namespace PatronsRumorsAle.Simulation
{
    public enum AutoPlayStrategy
    {
        FirstAvailable,
        MatchFaction,
        SarmatianGreed,
        RevolutionaryAgitation
    }

    public static class AutoPlayRunner
    {
        public static bool TrySeatNext(DaySimulation simulation, AutoPlayStrategy strategy)
        {
            if (simulation == null)
                throw new ArgumentNullException(nameof(simulation));
            if (simulation.State.Status != DayStatus.Running || simulation.State.Queue.CustomerIds.Count == 0)
                return false;

            var bestScore = int.MinValue;
            var bestCustomerId = -1;
            TableState bestTable = null;
            var bestSeatIndex = -1;

            foreach (var customerId in simulation.State.Queue.CustomerIds)
            {
                var customer = simulation.State.Customers[customerId];
                foreach (var table in simulation.State.Tables)
                {
                    var score = Score(customer, table, simulation.State, strategy);
                    for (var seatIndex = 0; seatIndex < table.Seats.Count; seatIndex++)
                    {
                        if (table.Seats[seatIndex].IsOccupied || score <= bestScore)
                            continue;
                        bestScore = score;
                        bestCustomerId = customerId;
                        bestTable = table;
                        bestSeatIndex = seatIndex;
                    }
                }
            }

            return bestTable != null &&
                simulation.SeatCustomer(bestCustomerId, bestTable.Id, bestSeatIndex);
        }

        private static int Score(
            CustomerInstance customer,
            TableState table,
            GameState state,
            AutoPlayStrategy strategy)
        {
            var sameFaction = 0;
            var neutrals = 0;
            foreach (var seat in table.Seats)
            {
                if (!seat.CustomerId.HasValue)
                    continue;
                var occupant = state.Customers[seat.CustomerId.Value];
                if (occupant.Faction == customer.Faction)
                    sameFaction++;
                if (occupant.Faction == FactionId.Neutrals)
                    neutrals++;
            }

            switch (strategy)
            {
                case AutoPlayStrategy.MatchFaction:
                    return sameFaction * 10;
                case AutoPlayStrategy.SarmatianGreed:
                    return customer.Faction == FactionId.Sarmatians ? 100 + sameFaction * 20 : sameFaction;
                case AutoPlayStrategy.RevolutionaryAgitation:
                    return customer.Faction == FactionId.Revolutionaries ? 100 + neutrals * 20 : sameFaction;
                default:
                    return 0;
            }
        }
    }
}
