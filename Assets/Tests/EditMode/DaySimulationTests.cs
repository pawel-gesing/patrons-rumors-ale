using System.Collections.Generic;
using NUnit.Framework;
using PatronsRumorsAle.Content;
using PatronsRumorsAle.Simulation;

namespace PatronsRumorsAle.Tests
{
    public sealed class DaySimulationTests
    {
        [Test]
        public void QueuedCustomer_LosesPatienceAndLeaves()
        {
            var simulation = CreateSimulation(new[] { Archetype("guest", "Neutrals", 1f, 20f, 10) });
            var id = simulation.State.Queue.CustomerIds[0];

            simulation.Advance(1.1f);

            Assert.That(simulation.State.Customers[id].Location, Is.EqualTo(CustomerLocation.Left));
            CollectionAssert.DoesNotContain(simulation.State.Queue.CustomerIds, id);
            Assert.That(simulation.State.Reputation.Get(FactionId.Neutrals), Is.LessThan(0f));
        }

        [Test]
        public void SeatingCustomer_OccupiesSeat()
        {
            var simulation = CreateSimulation(new[] { Archetype("guest", "Neutrals") });
            var id = simulation.State.Queue.CustomerIds[0];

            Assert.That(simulation.SeatCustomer(id, "table", 0), Is.True);
            Assert.That(simulation.State.Tables[0].Seats[0].CustomerId, Is.EqualTo(id));
            Assert.That(simulation.State.Customers[id].Location, Is.EqualTo(CustomerLocation.Table));
        }

        [Test]
        public void OccupiedSeat_CannotReceiveAnotherCustomer()
        {
            var simulation = CreateSimulation(new[] { Archetype("guest", "Neutrals") }, startingCustomers: 2);
            var first = simulation.State.Queue.CustomerIds[0];
            var second = simulation.State.Queue.CustomerIds[1];

            Assert.That(simulation.SeatCustomer(first, "table", 0), Is.True);
            Assert.That(simulation.SeatCustomer(second, "table", 0), Is.False);
            CollectionAssert.Contains(simulation.State.Queue.CustomerIds, second);
        }

        [Test]
        public void CustomerAfterStay_PaysAndFreesSeat()
        {
            var simulation = CreateSimulation(new[] { Archetype("guest", "Neutrals", stay: 1f, spend: 17) });
            var id = simulation.State.Queue.CustomerIds[0];
            simulation.SeatCustomer(id, "table", 0);

            simulation.Advance(1.1f);

            Assert.That(simulation.State.Tables[0].Seats[0].IsOccupied, Is.False);
            Assert.That(simulation.State.Economy.Money, Is.EqualTo(17));
        }

        [Test]
        public void Sarmatian_GetsBonusWithAnotherSarmatian()
        {
            var simulation = CreateSimulation(new[] { Archetype("noble", "Sarmatians", stay: 10f, spend: 20) }, startingCustomers: 2);
            var first = simulation.State.Queue.CustomerIds[0];
            var second = simulation.State.Queue.CustomerIds[1];
            simulation.SeatCustomer(first, "table", 0);

            simulation.SeatCustomer(second, "table", 1);

            Assert.That(simulation.State.Customers[second].StayRemaining, Is.EqualTo(12f).Within(0.01f));
            simulation.Advance(12.1f);
            Assert.That(simulation.State.Economy.Money, Is.GreaterThanOrEqualTo(45));
        }

        [Test]
        public void Revolutionary_GetsBonusWithNeutralAudience()
        {
            var archetypes = new[]
            {
                Archetype("worker", "Neutrals", stay: 10f),
                Archetype("agitator", "Revolutionaries", stay: 10f)
            };
            var simulation = CreateSimulation(archetypes, startingCustomers: 8);
            var neutral = FindQueued(simulation, FactionId.Neutrals);
            var revolutionary = FindQueued(simulation, FactionId.Revolutionaries);
            simulation.SeatCustomer(neutral, "table", 0);

            simulation.SeatCustomer(revolutionary, "table", 1);

            Assert.That(simulation.State.Customers[revolutionary].StayRemaining, Is.EqualTo(11.5f).Within(0.01f));
        }

        [Test]
        public void ActiveMoonshiner_IncreasesAnotherCustomersRevenue()
        {
            var archetypes = new[]
            {
                Archetype("distiller", "Moonshiners", stay: 100f, spend: 10),
                Archetype("worker", "Neutrals", stay: 1f, spend: 100)
            };
            var simulation = CreateSimulation(archetypes, startingCustomers: 8);
            var moonshiner = FindQueued(simulation, FactionId.Moonshiners);
            var neutral = FindQueued(simulation, FactionId.Neutrals);
            simulation.SeatCustomer(moonshiner, "table", 0);
            simulation.SeatCustomer(neutral, "table", 1);

            simulation.Advance(1.1f);

            Assert.That(simulation.State.Economy.Money, Is.EqualTo(110));
        }

        [Test]
        public void Reputation_IsClampedToMinusOneHundred()
        {
            var simulation = CreateSimulation(new[] { Archetype("guest", "Neutrals") }, startingCustomers: 20);
            var ids = new List<int>(simulation.State.Queue.CustomerIds);
            foreach (var id in ids)
                simulation.RejectCustomer(id);

            Assert.That(simulation.State.Reputation.Get(FactionId.Neutrals), Is.EqualTo(-100f));
        }

        [Test]
        public void Reputation_DriftsTowardZero()
        {
            var simulation = CreateSimulation(new[] { Archetype("guest", "Neutrals") });
            var id = simulation.State.Queue.CustomerIds[0];
            simulation.RejectCustomer(id);
            var before = simulation.State.Reputation.Get(FactionId.Neutrals);

            simulation.Advance(10f);

            Assert.That(simulation.State.Reputation.Get(FactionId.Neutrals), Is.GreaterThan(before));
            Assert.That(simulation.State.Reputation.Get(FactionId.Neutrals), Is.LessThanOrEqualTo(0f));
        }

        [Test]
        public void DayCompletes_WhenMoneyGoalIsMet()
        {
            var simulation = CreateSimulation(
                new[] { Archetype("guest", "Neutrals", stay: 0.5f, spend: 10) },
                duration: 2f,
                moneyGoal: 10);
            simulation.SeatCustomer(simulation.State.Queue.CustomerIds[0], "table", 0);

            simulation.Advance(2f);

            Assert.That(simulation.State.Status, Is.EqualTo(DayStatus.Completed));
        }

        private static DaySimulation CreateSimulation(
            IReadOnlyList<ArchetypeDefinition> archetypes,
            int startingCustomers = 1,
            float duration = 200f,
            int moneyGoal = 9999)
        {
            var balance = new BalanceDefinition
            {
                arrivalIntervalSeconds = 1000f,
                arrivalGroupMin = 1,
                arrivalGroupMax = 1,
                reputationDriftPerSecond = 0.05f,
                impatientReputationPenalty = 5f,
                rejectionReputationPenalty = 7f,
                goodSeatingReputationReward = 2f,
                longStayReputationReward = 3f,
                sarmatianCompanionStayBonus = 0.2f,
                sarmatianCompanionSpendBonus = 0.25f,
                revolutionaryAudienceStayBonus = 0.15f,
                moonshinerGlobalSpendBonus = 0.1f
            };
            var day = new DayDefinition
            {
                id = "test",
                durationSeconds = duration,
                startingCustomers = startingCustomers,
                moneyGoal = moneyGoal,
                tables = new List<TableDefinition> { new TableDefinition { id = "table", seats = 20 } }
            };
            var content = new ContentDatabase(
                new List<FactionDefinition>(),
                archetypes,
                new List<TraitDefinition>(),
                new List<DayDefinition> { day },
                balance);
            var simulation = new DaySimulation();
            simulation.Initialize(day, content, 12345);
            return simulation;
        }

        private static ArchetypeDefinition Archetype(
            string id,
            string faction,
            float patience = 100f,
            float stay = 20f,
            int spend = 10)
            => new ArchetypeDefinition
            {
                id = id,
                displayName = id,
                factionId = faction,
                patienceSeconds = patience,
                staySeconds = stay,
                baseSpend = spend
            };

        private static int FindQueued(DaySimulation simulation, FactionId faction)
        {
            foreach (var id in simulation.State.Queue.CustomerIds)
            {
                if (simulation.State.Customers[id].Faction == faction)
                    return id;
            }
            Assert.Fail($"No queued customer from faction {faction}.");
            return -1;
        }
    }
}
