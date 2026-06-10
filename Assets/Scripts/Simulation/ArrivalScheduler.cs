using System;

namespace PatronsRumorsAle.Simulation
{
    public sealed class ArrivalScheduler
    {
        private readonly float intervalSeconds;
        private float nextArrivalTime;

        public ArrivalScheduler(float intervalSeconds)
        {
            if (intervalSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(intervalSeconds));
            this.intervalSeconds = intervalSeconds;
            nextArrivalTime = intervalSeconds;
        }

        public float NextArrivalTime => nextArrivalTime;

        public bool IsDue(float elapsedSeconds) => elapsedSeconds >= nextArrivalTime;
        public void Consume() => nextArrivalTime += intervalSeconds;
    }
}

