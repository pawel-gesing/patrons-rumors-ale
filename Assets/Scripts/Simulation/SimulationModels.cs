using System;
using System.Collections.Generic;
using PatronsRumorsAle.Content;

namespace PatronsRumorsAle.Simulation
{
    public enum CustomerLocation
    {
        Queue,
        Table,
        Left
    }

    public enum DayStatus
    {
        Running,
        Completed,
        Failed
    }

    public sealed class CustomerInstance
    {
        public int Id { get; }
        public string ArchetypeId { get; }
        public string DisplayName { get; }
        public FactionId Faction { get; }
        public CustomerLocation Location { get; internal set; }
        public float PatienceRemaining { get; internal set; }
        public float StayRemaining { get; internal set; }
        public float InitialStaySeconds { get; internal set; }
        public float ArrivalTime { get; internal set; }
        public float SeatedTime { get; internal set; }
        public int BaseSpend { get; }
        public string TableId { get; internal set; }
        public int SeatIndex { get; internal set; } = -1;

        internal CustomerInstance(int id, ArchetypeDefinition archetype, FactionDefinition faction)
        {
            Id = id;
            ArchetypeId = archetype.id;
            DisplayName = archetype.displayName;
            Faction = ParseFaction(faction.id);
            Location = CustomerLocation.Queue;
            PatienceRemaining = faction.patienceSeconds;
            StayRemaining = faction.baseStayTimeSeconds;
            InitialStaySeconds = faction.baseStayTimeSeconds;
            BaseSpend = faction.baseSpend;
        }

        private static FactionId ParseFaction(string value)
        {
            if (Enum.TryParse(value, true, out FactionId result))
                return result;
            throw new InvalidOperationException($"Unknown faction id '{value}'.");
        }
    }

    public sealed class QueueState
    {
        private readonly List<int> customerIds = new List<int>();
        public IReadOnlyList<int> CustomerIds => customerIds;
        internal void Add(int id) => customerIds.Add(id);
        internal bool Remove(int id) => customerIds.Remove(id);
        internal bool Contains(int id) => customerIds.Contains(id);
    }

    public sealed class SeatState
    {
        public int Index { get; }
        public int? CustomerId { get; internal set; }
        public bool IsOccupied => CustomerId.HasValue;
        internal SeatState(int index) => Index = index;
    }

    public sealed class TableState
    {
        public string Id { get; }
        public IReadOnlyList<SeatState> Seats => seats;
        private readonly List<SeatState> seats = new List<SeatState>();

        internal TableState(string id, int seatCount)
        {
            Id = id;
            for (var i = 0; i < seatCount; i++)
                seats.Add(new SeatState(i));
        }

        internal SeatState GetSeat(int index)
        {
            if (index < 0 || index >= seats.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return seats[index];
        }

        public int OccupiedSeatCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < seats.Count; i++)
                {
                    if (seats[i].IsOccupied)
                        count++;
                }
                return count;
            }
        }

        public int FreeSeatCount => seats.Count - OccupiedSeatCount;

        internal SeatState GetFirstFreeSeat()
        {
            for (var i = 0; i < seats.Count; i++)
            {
                if (!seats[i].IsOccupied)
                    return seats[i];
            }
            return null;
        }
    }

    public sealed class ReputationState
    {
        private readonly Dictionary<FactionId, float> values = new Dictionary<FactionId, float>();
        public IReadOnlyDictionary<FactionId, float> Values => values;

        public ReputationState()
        {
            foreach (FactionId faction in Enum.GetValues(typeof(FactionId)))
                values[faction] = 0f;
        }

        public float Get(FactionId faction) => values[faction];

        internal float Change(FactionId faction, float delta)
        {
            values[faction] = Clamp(values[faction] + delta, -100f, 100f);
            return values[faction];
        }

        internal float Drift(FactionId faction, float amount)
        {
            var before = values[faction];
            if (before > 0f)
                values[faction] = Math.Max(0f, before - amount);
            else if (before < 0f)
                values[faction] = Math.Min(0f, before + amount);
            return values[faction] - before;
        }

        private static float Clamp(float value, float min, float max)
            => Math.Max(min, Math.Min(max, value));
    }

    public sealed class EconomyState
    {
        public int Money { get; internal set; }
    }

    public sealed class GameState
    {
        public float ElapsedSeconds { get; internal set; }
        public float DayDurationSeconds { get; internal set; }
        public DayStatus Status { get; internal set; }
        public QueueState Queue { get; } = new QueueState();
        public List<TableState> Tables { get; } = new List<TableState>();
        public Dictionary<int, CustomerInstance> Customers { get; } = new Dictionary<int, CustomerInstance>();
        public ReputationState Reputation { get; } = new ReputationState();
        public EconomyState Economy { get; } = new EconomyState();
        public int MoneyGoal { get; internal set; }
        public float RemainingSeconds => Math.Max(0f, DayDurationSeconds - ElapsedSeconds);
    }

    public sealed class GameStateSnapshot
    {
        public GameState State { get; }
        internal GameStateSnapshot(GameState state) => State = state;
    }
}
