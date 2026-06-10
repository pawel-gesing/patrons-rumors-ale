using System;
using System.Collections.Generic;
using PatronsRumorsAle.Content;

namespace PatronsRumorsAle.Simulation
{
    public sealed class DaySimulation
    {
        private ContentDatabase content;
        private DayDefinition day;
        private BalanceDefinition balance;
        private Random random;
        private ArrivalScheduler arrivals;
        private RuleEvaluator rules;
        private GameState state;
        private int nextCustomerId = 1;
        private readonly Dictionary<int, float> spendMultipliers = new Dictionary<int, float>();

        public SimulationEventLog EventLog { get; } = new SimulationEventLog();
        public GameState State => state;

        public void Initialize(DayDefinition dayDefinition, ContentDatabase contentDatabase, int seed)
        {
            day = dayDefinition ?? throw new ArgumentNullException(nameof(dayDefinition));
            content = contentDatabase ?? throw new ArgumentNullException(nameof(contentDatabase));
            balance = content.Balance ?? throw new InvalidOperationException("Balance configuration is missing.");
            random = new Random(seed);
            arrivals = new ArrivalScheduler(balance.arrivalIntervalSeconds);
            rules = new RuleEvaluator(balance);
            nextCustomerId = 1;
            spendMultipliers.Clear();

            state = new GameState
            {
                DayDurationSeconds = day.durationSeconds,
                MoneyGoal = day.moneyGoal,
                Status = DayStatus.Running
            };

            foreach (var table in day.tables)
                state.Tables.Add(new TableState(table.id, table.seats));

            for (var i = 0; i < day.startingCustomers; i++)
                AddRandomCustomer();

            Log("day_started", detail: day.id);
        }

        public void Advance(float seconds)
        {
            EnsureInitialized();
            if (seconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            if (state.Status != DayStatus.Running || seconds == 0f)
                return;

            var remaining = seconds;
            while (remaining > 0f && state.Status == DayStatus.Running)
            {
                var step = Math.Min(remaining, 0.1f);
                state.ElapsedSeconds += step;
                AdvanceQueue(step);
                AdvanceTables(step);
                state.Reputation.Drift(balance.reputationDriftPerSecond * step);

                while (arrivals.IsDue(state.ElapsedSeconds) && state.ElapsedSeconds < state.DayDurationSeconds)
                {
                    AddArrivalGroup();
                    arrivals.Consume();
                }

                if (state.ElapsedSeconds >= state.DayDurationSeconds)
                    CompleteDay();
                remaining -= step;
            }
        }

        public bool SeatCustomer(int customerId, string tableId, int seatIndex)
        {
            EnsureRunning();
            if (!state.Customers.TryGetValue(customerId, out var customer) ||
                customer.Location != CustomerLocation.Queue)
                return false;

            var table = FindTable(tableId);
            var seat = table.GetSeat(seatIndex);
            if (seat.IsOccupied)
                return false;

            var outcome = rules.Evaluate(customer, table, state);
            state.Queue.Remove(customerId);
            seat.CustomerId = customerId;
            customer.Location = CustomerLocation.Table;
            customer.TableId = tableId;
            customer.SeatIndex = seatIndex;
            customer.StayRemaining *= outcome.StayMultiplier;
            customer.InitialStaySeconds = customer.StayRemaining;
            spendMultipliers[customerId] = outcome.SpendMultiplier;
            Log("customer_seated", customerId, $"{tableId}:{seatIndex}");

            if (outcome.IsGoodSeating)
                ChangeReputation(customer.Faction, balance.goodSeatingReputationReward, customerId, "good_seating");
            return true;
        }

        public bool RejectCustomer(int customerId)
        {
            EnsureRunning();
            if (!state.Customers.TryGetValue(customerId, out var customer) ||
                customer.Location != CustomerLocation.Queue)
                return false;

            state.Queue.Remove(customerId);
            customer.Location = CustomerLocation.Left;
            ChangeReputation(customer.Faction, -balance.rejectionReputationPenalty, customerId, "rejected");
            Log("customer_rejected", customerId);
            return true;
        }

        public void SkipToNextEvent()
        {
            EnsureRunning();
            var delta = state.RemainingSeconds;
            delta = Math.Min(delta, Math.Max(0.01f, arrivals.NextArrivalTime - state.ElapsedSeconds));

            foreach (var id in state.Queue.CustomerIds)
                delta = Math.Min(delta, state.Customers[id].PatienceRemaining);
            foreach (var customer in state.Customers.Values)
            {
                if (customer.Location == CustomerLocation.Table)
                    delta = Math.Min(delta, customer.StayRemaining);
            }

            Advance(Math.Max(0.01f, delta));
        }

        public GameStateSnapshot GetStateSnapshot()
        {
            EnsureInitialized();
            return new GameStateSnapshot(state);
        }

        public void LogSelection(int customerId) => Log("customer_selected", customerId);

        private void AdvanceQueue(float seconds)
        {
            var leaving = new List<int>();
            foreach (var id in state.Queue.CustomerIds)
            {
                var customer = state.Customers[id];
                customer.PatienceRemaining -= seconds;
                if (customer.PatienceRemaining <= 0f)
                    leaving.Add(id);
            }

            foreach (var id in leaving)
            {
                var customer = state.Customers[id];
                state.Queue.Remove(id);
                customer.Location = CustomerLocation.Left;
                ChangeReputation(customer.Faction, -balance.impatientReputationPenalty, id, "impatient");
                Log("customer_left_queue_impatient", id);
            }
        }

        private void AdvanceTables(float seconds)
        {
            var leaving = new List<CustomerInstance>();
            foreach (var customer in state.Customers.Values)
            {
                if (customer.Location != CustomerLocation.Table)
                    continue;
                customer.StayRemaining -= seconds;
                if (customer.StayRemaining <= 0f)
                    leaving.Add(customer);
            }

            foreach (var customer in leaving)
                CustomerLeavesTable(customer);
        }

        private void CustomerLeavesTable(CustomerInstance customer)
        {
            var table = FindTable(customer.TableId);
            table.GetSeat(customer.SeatIndex).CustomerId = null;
            customer.Location = CustomerLocation.Left;

            var individualMultiplier = spendMultipliers.TryGetValue(customer.Id, out var multiplier) ? multiplier : 1f;
            var moonshinerMultiplier = rules.GetMoonshinerSpendMultiplier(state);
            var earned = (int)Math.Round(customer.BaseSpend * individualMultiplier * moonshinerMultiplier);
            state.Economy.Money += earned;
            Log("customer_left_table", customer.Id, customer.TableId);
            Log("money_earned", customer.Id, value: earned);

            if (customer.InitialStaySeconds > content.GetArchetype(customer.ArchetypeId).staySeconds)
                ChangeReputation(customer.Faction, balance.longStayReputationReward, customer.Id, "long_stay");
        }

        private void AddArrivalGroup()
        {
            var count = random.Next(balance.arrivalGroupMin, balance.arrivalGroupMax + 1);
            for (var i = 0; i < count; i++)
                AddRandomCustomer();
        }

        private CustomerInstance AddRandomCustomer()
        {
            var archetype = content.Archetypes[random.Next(0, content.Archetypes.Count)];
            var customer = new CustomerInstance(nextCustomerId++, archetype);
            state.Customers.Add(customer.Id, customer);
            state.Queue.Add(customer.Id);
            Log("customer_arrived", customer.Id, archetype.id);
            return customer;
        }

        private void CompleteDay()
        {
            state.ElapsedSeconds = state.DayDurationSeconds;
            state.Status = state.Economy.Money >= state.MoneyGoal ? DayStatus.Completed : DayStatus.Failed;
            if (state.Status == DayStatus.Completed)
            {
                Log("goal_completed", value: state.Economy.Money);
                Log("day_completed", value: state.Economy.Money);
            }
            else
            {
                Log("day_failed", value: state.Economy.Money);
            }
        }

        private void ChangeReputation(FactionId faction, float delta, int customerId, string reason)
        {
            var value = state.Reputation.Change(faction, delta);
            Log("reputation_changed", customerId, $"{faction}:{reason}", value);
        }

        private TableState FindTable(string id)
        {
            for (var i = 0; i < state.Tables.Count; i++)
            {
                if (state.Tables[i].Id == id)
                    return state.Tables[i];
            }
            throw new InvalidOperationException($"Unknown table '{id}'.");
        }

        private void Log(string type, int? customerId = null, string detail = "", float value = 0f)
            => EventLog.Add(new SimulationEvent(type, state?.ElapsedSeconds ?? 0f, customerId, detail, value));

        private void EnsureInitialized()
        {
            if (state == null)
                throw new InvalidOperationException("Simulation is not initialized.");
        }

        private void EnsureRunning()
        {
            EnsureInitialized();
            if (state.Status != DayStatus.Running)
                throw new InvalidOperationException("The day is no longer running.");
        }
    }
}
