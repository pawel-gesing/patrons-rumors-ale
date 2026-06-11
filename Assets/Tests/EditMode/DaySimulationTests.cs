using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PatronsRumorsAle.Content;
using PatronsRumorsAle.Simulation;

namespace PatronsRumorsAle.Tests
{
    public sealed class DaySimulationTests
    {
        [Test]
        public void ContentDays_UseV03TempoAndQueueCapacity()
        {
            var content = ContentLoader.LoadFromStreamingAssets();

            Assert.That(content.Days, Has.Count.GreaterThanOrEqualTo(3));
            Assert.That(content.Days.All(day => day.durationSeconds == 180f), Is.True);
            Assert.That(content.Days.All(day => day.arrivalIntervalSeconds == 7f), Is.True);
            Assert.That(content.Days.All(day => day.startingQueue == 5), Is.True);
            Assert.That(content.Days.All(day => day.visibleQueueCapacity == 5), Is.True);
            Assert.That(content.Days.SelectMany(day => day.groupSizeWeights)
                .All(weight => weight.size >= 1 && weight.size <= 3), Is.True);
        }

        [Test]
        public void ArrivalInterval_AddsConfiguredGroupAfterSevenSeconds()
        {
            var simulation = CreateSimulation(startingQueue: 0, arrivalInterval: 7f, groupSize: 3);

            simulation.Advance(6.9f);
            Assert.That(simulation.State.Queue.CustomerIds, Is.Empty);

            simulation.Advance(0.2f);
            Assert.That(simulation.State.Queue.CustomerIds, Has.Count.EqualTo(3));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void ArrivalGroup_SupportsOneToThreeCustomers(int groupSize)
        {
            var simulation = CreateSimulation(
                startingQueue: 0,
                arrivalInterval: 1f,
                groupSize: groupSize);

            simulation.Advance(1.1f);

            Assert.That(simulation.State.Queue.CustomerIds, Has.Count.EqualTo(groupSize));
        }

        [Test]
        public void QueueOverflow_NeverExceedsCapacityAndRecordsMissedCustomers()
        {
            var simulation = CreateSimulation(
                startingQueue: 4,
                queueCapacity: 5,
                arrivalInterval: 1f,
                groupSize: 3);

            simulation.Advance(1.1f);

            Assert.That(simulation.State.Queue.CustomerIds, Has.Count.EqualTo(5));
            Assert.That(simulation.Metrics.MissedCustomers, Is.EqualTo(2));
            Assert.That(simulation.GetDaySummary().MissedCustomers, Is.EqualTo(2));
            Assert.That(simulation.EventLog.Events.Count(item => item.Type == "missed_customer"), Is.EqualTo(2));
        }

        [TestCase(0f, 0f)]
        [TestCase(2f, -4f)]
        public void QueueOverflow_ReputationPenaltyIsConfigurable(float penalty, float expected)
        {
            var simulation = CreateSimulation(
                startingQueue: 5,
                queueCapacity: 5,
                arrivalInterval: 1f,
                groupSize: 2,
                queueOverflowPenalty: penalty);

            simulation.Advance(1.1f);

            Assert.That(simulation.State.Reputation.Get(FactionId.Neutrals), Is.EqualTo(expected).Within(0.01f));
        }

        [Test]
        public void SeatCustomerAtTable_UsesFirstFreeTechnicalSeat()
        {
            var simulation = CreateSimulation(startingQueue: 2, tableSeats: 2);
            var first = simulation.State.Queue.CustomerIds[0];
            var second = simulation.State.Queue.CustomerIds[1];

            Assert.That(simulation.SeatCustomerAtTable(first, "table"), Is.True);
            Assert.That(simulation.SeatCustomerAtTable(second, "table"), Is.True);

            Assert.That(simulation.State.Tables[0].Seats[0].CustomerId, Is.EqualTo(first));
            Assert.That(simulation.State.Tables[0].Seats[1].CustomerId, Is.EqualTo(second));
        }

        [Test]
        public void SeatCustomerAtTable_RejectsFullTable()
        {
            var simulation = CreateSimulation(startingQueue: 2, tableSeats: 1);
            var first = simulation.State.Queue.CustomerIds[0];
            var second = simulation.State.Queue.CustomerIds[1];

            Assert.That(simulation.SeatCustomerAtTable(first, "table"), Is.True);
            Assert.That(simulation.SeatCustomerAtTable(second, "table"), Is.False);
            CollectionAssert.Contains(simulation.State.Queue.CustomerIds, second);
        }

        [Test]
        public void PlacementPreview_DescribesWholeTable()
        {
            var simulation = CreateSimulation(startingQueue: 2, tableSeats: 4);
            simulation.SeatCustomerAtTable(simulation.State.Queue.CustomerIds[0], "table");

            var preview = simulation.PreviewPlacement(simulation.State.Queue.CustomerIds[0], "table");

            Assert.That(preview.TableId, Is.EqualTo("table"));
            Assert.That(preview.CurrentCustomerCount, Is.EqualTo(1));
            Assert.That(preview.FreeSeats, Is.EqualTo(3));
            CollectionAssert.Contains(preview.PresentFactions, FactionId.Neutrals);
            Assert.That(preview.IsNeutralPlacement, Is.True);
        }

        [Test]
        public void Sarmatian_GetsCompanionBonus()
        {
            var simulation = CreateSimulation(
                factions: new[] { Faction("Sarmatians", stay: 10f, spend: 20) },
                startingQueue: 2);
            simulation.SeatCustomerAtTable(simulation.State.Queue.CustomerIds[0], "table");

            var preview = simulation.PreviewPlacement(simulation.State.Queue.CustomerIds[0], "table");
            simulation.SeatCustomerAtTable(simulation.State.Queue.CustomerIds[0], "table");

            Assert.That(preview.IsGoodSeating, Is.True);
            Assert.That(preview.SpendMultiplier, Is.EqualTo(1.25f).Within(0.001f));
            Assert.That(preview.StayMultiplier, Is.EqualTo(1.2f).Within(0.001f));
        }

        [Test]
        public void SarmatianWithNeutral_IsNeutralWithoutFullBonusOrMajorPenalty()
        {
            var simulation = CreateMixedSimulation(startingQueue: 20);
            var neutral = FindQueued(simulation, FactionId.Neutrals);
            simulation.SeatCustomerAtTable(neutral, "table");
            var sarmatian = FindQueued(simulation, FactionId.Sarmatians);

            var preview = simulation.PreviewPlacement(sarmatian, "table");

            Assert.That(preview.IsGoodSeating, Is.False);
            Assert.That(preview.IsNeutralPlacement, Is.True);
            Assert.That(preview.SpendMultiplier, Is.EqualTo(1f).Within(0.001f));
            Assert.That(preview.ReputationDelta, Is.EqualTo(-0.5f).Within(0.001f));
        }

        [Test]
        public void Revolutionary_GetsLargerBonusWithNeutralThanRevolutionary()
        {
            var mixed = CreateMixedSimulation(startingQueue: 20);
            mixed.SeatCustomerAtTable(FindQueued(mixed, FactionId.Neutrals), "table");
            var neutralAudience = mixed.PreviewPlacement(
                FindQueued(mixed, FactionId.Revolutionaries),
                "table");

            var revolutionaries = CreateSimulation(
                factions: new[] { Faction("Revolutionaries") },
                startingQueue: 2);
            revolutionaries.SeatCustomerAtTable(revolutionaries.State.Queue.CustomerIds[0], "table");
            var companionAudience = revolutionaries.PreviewPlacement(
                revolutionaries.State.Queue.CustomerIds[0],
                "table");

            Assert.That(neutralAudience.StayMultiplier, Is.EqualTo(1.2f).Within(0.001f));
            Assert.That(companionAudience.StayMultiplier, Is.EqualTo(1.08f).Within(0.001f));
        }

        [Test]
        public void ActiveMoonshiners_IncreaseRevenueOnlyUpToConfiguredCap()
        {
            var simulation = CreateMixedSimulation(
                startingQueue: 30,
                moonshinerBonus: 0.1f,
                moonshinerCap: 0.2f,
                neutralStay: 1f,
                neutralSpend: 100);
            for (var i = 0; i < 3; i++)
                simulation.SeatCustomerAtTable(FindQueued(simulation, FactionId.Moonshiners), "table");
            simulation.SeatCustomerAtTable(FindQueued(simulation, FactionId.Neutrals), "table");

            simulation.Advance(1.1f);

            Assert.That(simulation.State.Economy.Money, Is.EqualTo(120));
        }

        [Test]
        public void Reputation_IsClampedAndDriftsTowardZero()
        {
            var simulation = CreateSimulation(
                startingQueue: 5,
                rejectionPenalty: 25f,
                reputationDrift: 0.05f);
            foreach (var id in simulation.State.Queue.CustomerIds.ToArray())
                simulation.RejectCustomer(id);
            Assert.That(simulation.State.Reputation.Get(FactionId.Neutrals), Is.EqualTo(-100f));

            simulation.Advance(10f);

            Assert.That(simulation.State.Reputation.Get(FactionId.Neutrals), Is.GreaterThan(-100f));
            Assert.That(simulation.State.Reputation.Get(FactionId.Neutrals), Is.LessThanOrEqualTo(0f));
        }

        [Test]
        public void SeatingAlone_DoesNotRewardNeutralReputation()
        {
            var simulation = CreateSimulation(startingQueue: 1);

            simulation.SeatCustomerAtTable(simulation.State.Queue.CustomerIds[0], "table");

            Assert.That(simulation.State.Reputation.Get(FactionId.Neutrals), Is.EqualTo(0f));
        }

        [TestCase(AutoPlayStrategy.FirstAvailable)]
        [TestCase(AutoPlayStrategy.MatchFaction)]
        [TestCase(AutoPlayStrategy.SarmatianGreed)]
        [TestCase(AutoPlayStrategy.RevolutionaryAgitation)]
        public void AutoPlayStrategies_SeatAtTable(AutoPlayStrategy strategy)
        {
            var simulation = CreateMixedSimulation(startingQueue: 20);
            var before = simulation.State.Queue.CustomerIds.Count;

            Assert.That(AutoPlayRunner.TrySeatNext(simulation, strategy), Is.True);
            Assert.That(simulation.State.Queue.CustomerIds.Count, Is.EqualTo(before - 1));
            Assert.That(simulation.State.Tables.Sum(table => table.OccupiedSeatCount), Is.EqualTo(1));
        }

        [Test]
        public void Metrics_ReportServiceRejectionImpatienceAndTableMoney()
        {
            var simulation = CreateSimulation(
                startingQueue: 3,
                neutralStay: 1f,
                neutralSpend: 17,
                neutralPatience: 1f);
            var served = simulation.State.Queue.CustomerIds[0];
            var rejected = simulation.State.Queue.CustomerIds[1];
            simulation.Advance(0.5f);
            simulation.SeatCustomerAtTable(served, "table");
            simulation.RejectCustomer(rejected);
            simulation.Advance(1.1f);

            var summary = simulation.GetDaySummary();

            Assert.That(summary.ServedCustomers, Is.EqualTo(1));
            Assert.That(summary.RejectedCustomers, Is.EqualTo(1));
            Assert.That(summary.ImpatientDepartures, Is.EqualTo(1));
            Assert.That(summary.AverageWaitSeconds, Is.EqualTo(0.5f).Within(0.11f));
            Assert.That(summary.AverageStaySeconds, Is.EqualTo(1f).Within(0.11f));
            Assert.That(summary.BestEarningTableMoney, Is.EqualTo(17));
        }

        [Test]
        public void DayEndsAtConfiguredDuration()
        {
            var simulation = CreateSimulation(duration: 180f, moneyGoal: 9999);

            simulation.Advance(180f);

            Assert.That(simulation.State.ElapsedSeconds, Is.EqualTo(180f));
            Assert.That(simulation.State.Status, Is.EqualTo(DayStatus.Failed));
        }

        private static DaySimulation CreateMixedSimulation(
            int startingQueue,
            float moonshinerBonus = 0.1f,
            float moonshinerCap = 0.3f,
            float neutralStay = 30f,
            int neutralSpend = 14)
        {
            return CreateSimulation(
                factions: new[]
                {
                    Faction("Sarmatians", spend: 20),
                    Faction("Moonshiners", stay: 100f),
                    Faction("Revolutionaries"),
                    Faction("Neutrals", stay: neutralStay, spend: neutralSpend)
                },
                startingQueue: startingQueue,
                queueCapacity: startingQueue,
                tableSeats: 40,
                moonshinerBonus: moonshinerBonus,
                moonshinerCap: moonshinerCap);
        }

        private static DaySimulation CreateSimulation(
            IReadOnlyList<FactionDefinition> factions = null,
            int startingQueue = 1,
            int queueCapacity = 40,
            int tableSeats = 20,
            float duration = 200f,
            float arrivalInterval = 1000f,
            int groupSize = 1,
            int moneyGoal = 9999,
            float queueOverflowPenalty = 0f,
            float rejectionPenalty = 7f,
            float reputationDrift = 0.05f,
            float moonshinerBonus = 0.1f,
            float moonshinerCap = 0.3f,
            float neutralStay = 30f,
            int neutralSpend = 14,
            float neutralPatience = 100f)
        {
            factions = factions ?? new[]
            {
                Faction("Neutrals", neutralPatience, neutralStay, neutralSpend)
            };
            var balance = new BalanceDefinition
            {
                reputationDriftPerSecond = reputationDrift,
                impatientReputationPenalty = 5f,
                rejectionReputationPenalty = rejectionPenalty,
                goodSeatingReputationReward = 2f,
                longStayReputationReward = 3f,
                unmetFactionExpectationPenalty = 0.5f,
                queueOverflowReputationPenalty = queueOverflowPenalty,
                sarmatianCompanionStayBonus = 0.2f,
                sarmatianCompanionSpendBonus = 0.25f,
                revolutionaryNeutralAudienceStayBonus = 0.2f,
                revolutionaryCompanionStayBonus = 0.08f,
                moonshinerGlobalSpendBonus = moonshinerBonus,
                moonshinerGlobalSpendBonusCap = moonshinerCap
            };
            var day = new DayDefinition
            {
                id = "test",
                durationSeconds = duration,
                arrivalIntervalSeconds = arrivalInterval,
                startingQueue = startingQueue,
                visibleQueueCapacity = queueCapacity,
                moneyGoal = moneyGoal,
                tables = new List<TableDefinition>
                {
                    new TableDefinition { id = "table", seats = tableSeats }
                },
                groupSizeWeights = new List<GroupSizeWeightDefinition>
                {
                    new GroupSizeWeightDefinition { size = groupSize, weight = 1f }
                }
            };
            foreach (var faction in factions)
            {
                day.factionArrivalWeights.Add(new FactionWeightDefinition
                {
                    factionId = faction.id,
                    weight = 1f
                });
            }

            var archetypes = new List<ArchetypeDefinition>
            {
                new ArchetypeDefinition
                {
                    id = "guest",
                    displayName = "Guest",
                    factionId = "",
                    description = "Test guest.",
                    spriteId = "placeholder_guest",
                    encyclopediaEntryId = "guest"
                }
            };
            var content = new ContentDatabase(
                factions,
                archetypes,
                new List<TraitDefinition>(),
                new List<DayDefinition> { day },
                balance);
            var simulation = new DaySimulation();
            simulation.Initialize(day, content, 12345);
            return simulation;
        }

        private static FactionDefinition Faction(
            string id,
            float patience = 100f,
            float stay = 30f,
            int spend = 14)
        {
            return new FactionDefinition
            {
                id = id,
                displayName = id,
                color = "#FFFFFF",
                patienceSeconds = patience,
                baseStayTimeSeconds = stay,
                baseSpend = spend
            };
        }

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
