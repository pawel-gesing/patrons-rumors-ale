using System;
using System.Collections.Generic;
using PatronsRumorsAle.Content;

namespace PatronsRumorsAle.Simulation
{
    public sealed class ReputationReasonMetric
    {
        public FactionId Faction { get; }
        public string Reason { get; }
        public float Delta { get; internal set; }

        internal ReputationReasonMetric(FactionId faction, string reason)
        {
            Faction = faction;
            Reason = reason;
        }
    }

    public sealed class DaySummary
    {
        public DayStatus Status { get; internal set; }
        public int MoneyEarned { get; internal set; }
        public int MoneyGoal { get; internal set; }
        public IReadOnlyDictionary<FactionId, float> FinalReputation { get; internal set; }
        public IReadOnlyList<ReputationReasonMetric> ReputationChanges { get; internal set; }
        public int ServedCustomers { get; internal set; }
        public int ImpatientDepartures { get; internal set; }
        public int RejectedCustomers { get; internal set; }
        public int MissedCustomers { get; internal set; }
        public float AverageWaitSeconds { get; internal set; }
        public float AverageStaySeconds { get; internal set; }
        public string BestEarningTableId { get; internal set; }
        public int BestEarningTableMoney { get; internal set; }
        public int ActivatedFactionBonuses { get; internal set; }
        public IReadOnlyDictionary<string, int> MoneyByTable { get; internal set; }
    }

    public sealed class DayMetricsCollector
    {
        private readonly Dictionary<string, int> moneyByTable = new Dictionary<string, int>();
        private readonly Dictionary<string, ReputationReasonMetric> reputationChanges =
            new Dictionary<string, ReputationReasonMetric>();
        private float totalWaitSeconds;
        private int seatedCustomers;
        private float totalStaySeconds;

        public int ServedCustomers { get; private set; }
        public int RejectedCustomers { get; private set; }
        public int ImpatientDepartures { get; private set; }
        public int MissedCustomers { get; private set; }
        public int ActivatedFactionBonuses { get; private set; }

        internal void RecordSeated(float waitSeconds)
        {
            seatedCustomers++;
            totalWaitSeconds += Math.Max(0f, waitSeconds);
        }

        internal void RecordServed(string tableId, int money, float staySeconds)
        {
            ServedCustomers++;
            totalStaySeconds += Math.Max(0f, staySeconds);
            if (!moneyByTable.ContainsKey(tableId))
                moneyByTable[tableId] = 0;
            moneyByTable[tableId] += money;
        }

        internal void RecordRejected() => RejectedCustomers++;
        internal void RecordImpatientDeparture() => ImpatientDepartures++;
        internal void RecordMissedCustomer() => MissedCustomers++;
        internal void RecordBonusActivated() => ActivatedFactionBonuses++;

        internal void RecordReputation(FactionId faction, string reason, float delta)
        {
            if (Math.Abs(delta) < 0.0001f)
                return;

            var key = faction + ":" + reason;
            if (!reputationChanges.TryGetValue(key, out var metric))
            {
                metric = new ReputationReasonMetric(faction, reason);
                reputationChanges.Add(key, metric);
            }
            metric.Delta += delta;
        }

        public DaySummary CreateSummary(GameState state)
        {
            var finalReputation = new Dictionary<FactionId, float>();
            foreach (var pair in state.Reputation.Values)
                finalReputation[pair.Key] = pair.Value;

            var reasonMetrics = new List<ReputationReasonMetric>(reputationChanges.Values);
            reasonMetrics.Sort((left, right) =>
            {
                var factionComparison = left.Faction.CompareTo(right.Faction);
                return factionComparison != 0
                    ? factionComparison
                    : string.CompareOrdinal(left.Reason, right.Reason);
            });

            var bestTableId = "";
            var bestTableMoney = 0;
            foreach (var pair in moneyByTable)
            {
                if (bestTableId.Length == 0 || pair.Value > bestTableMoney)
                {
                    bestTableId = pair.Key;
                    bestTableMoney = pair.Value;
                }
            }

            return new DaySummary
            {
                Status = state.Status,
                MoneyEarned = state.Economy.Money,
                MoneyGoal = state.MoneyGoal,
                FinalReputation = finalReputation,
                ReputationChanges = reasonMetrics,
                ServedCustomers = ServedCustomers,
                ImpatientDepartures = ImpatientDepartures,
                RejectedCustomers = RejectedCustomers,
                MissedCustomers = MissedCustomers,
                AverageWaitSeconds = seatedCustomers == 0 ? 0f : totalWaitSeconds / seatedCustomers,
                AverageStaySeconds = ServedCustomers == 0 ? 0f : totalStaySeconds / ServedCustomers,
                BestEarningTableId = bestTableId,
                BestEarningTableMoney = bestTableMoney,
                ActivatedFactionBonuses = ActivatedFactionBonuses,
                MoneyByTable = new Dictionary<string, int>(moneyByTable)
            };
        }
    }
}
