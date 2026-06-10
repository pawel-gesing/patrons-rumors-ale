using System;
using System.Collections.Generic;
using System.IO;
using PatronsRumorsAle.Simulation;
using UnityEngine;

namespace PatronsRumorsAle.Telemetry
{
    [Serializable]
    internal sealed class TelemetryRecord
    {
        public string type;
        public float time;
        public int customerId;
        public string detail;
        public float value;
    }

    [Serializable]
    internal sealed class TelemetryFile
    {
        public List<TelemetryRecord> events = new List<TelemetryRecord>();
    }

    public sealed class LocalTelemetry
    {
        private readonly TelemetryFile data = new TelemetryFile();
        public int Count => data.events.Count;

        public void Record(SimulationEvent simulationEvent)
        {
            data.events.Add(new TelemetryRecord
            {
                type = simulationEvent.Type,
                time = simulationEvent.Time,
                customerId = simulationEvent.CustomerId ?? -1,
                detail = simulationEvent.Detail,
                value = simulationEvent.Value
            });
        }

        public string SaveToJson()
        {
            var path = Path.Combine(Application.persistentDataPath, "patrons-rumors-ale-telemetry.json");
            File.WriteAllText(path, JsonUtility.ToJson(data, true));
            return path;
        }
    }
}

