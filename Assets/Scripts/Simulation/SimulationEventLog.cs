using System;
using System.Collections.Generic;

namespace PatronsRumorsAle.Simulation
{
    [Serializable]
    public sealed class SimulationEvent
    {
        public string Type { get; }
        public float Time { get; }
        public int? CustomerId { get; }
        public string Detail { get; }
        public float Value { get; }

        public SimulationEvent(string type, float time, int? customerId = null, string detail = "", float value = 0f)
        {
            Type = type;
            Time = time;
            CustomerId = customerId;
            Detail = detail;
            Value = value;
        }
    }

    public sealed class SimulationEventLog
    {
        private readonly List<SimulationEvent> events = new List<SimulationEvent>();
        public IReadOnlyList<SimulationEvent> Events => events;
        public event Action<SimulationEvent> EventAdded;

        public void Add(SimulationEvent simulationEvent)
        {
            events.Add(simulationEvent);
            EventAdded?.Invoke(simulationEvent);
        }
    }
}

