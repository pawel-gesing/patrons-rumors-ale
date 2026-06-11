using System;
using System.Collections.Generic;
using PatronsRumorsAle.Content;
using PatronsRumorsAle.Simulation;
using PatronsRumorsAle.Telemetry;
using UnityEngine;

namespace PatronsRumorsAle.Presentation
{
    public sealed class PrototypeTavernController : MonoBehaviour
    {
        private DaySimulation simulation;
        private ContentDatabase content;
        private LocalTelemetry telemetry;
        private int? selectedCustomerId;
        private int? detailsCustomerId;
        private bool paused;
        private float speed = 1f;
        private int selectedDayIndex;
        private bool autoPlay;
        private AutoPlayStrategy autoPlayStrategy;
        private float autoPlayCooldown;
        private string startupError;
        private string savedTelemetryPath;
        private Vector2 queueScroll;
        private Vector2 summaryScroll;
        private SeatingOutcome hoveredPreview;
        private string hoveredPreviewTarget;
        private readonly List<string> recentEvents = new List<string>();
        private GUIStyle titleStyle;
        private GUIStyle headingStyle;
        private GUIStyle centeredStyle;
        private GUIStyle smallStyle;
        private GUIStyle buttonStyle;
        private Texture2D panelTexture;
        private Texture2D darkTexture;
        private readonly Dictionary<FactionId, Texture2D> factionTextures = new Dictionary<FactionId, Texture2D>();

        private void Awake()
        {
            Application.targetFrameRate = 60;
            try
            {
                content = ContentLoader.LoadFromStreamingAssets();
                StartDay();
            }
            catch (Exception exception)
            {
                startupError = exception.ToString();
                Debug.LogException(exception);
            }
        }

        private void Update()
        {
            if (simulation == null || paused || simulation.State.Status != DayStatus.Running)
                return;
            simulation.Advance(Time.deltaTime * speed);
            if (autoPlay)
            {
                autoPlayCooldown -= Time.deltaTime;
                if (autoPlayCooldown <= 0f)
                {
                    AutoPlayRunner.TrySeatNext(simulation, autoPlayStrategy);
                    autoPlayCooldown = 0.35f;
                }
            }
        }

        private void OnGUI()
        {
            EnsureStyles();
            GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width / 1600f, Screen.height / 900f, 1f));

            if (!string.IsNullOrEmpty(startupError))
            {
                GUI.Box(new Rect(100, 100, 1400, 700), "CONTENT ERROR\n\n" + startupError, headingStyle);
                return;
            }

            DrawBackground();
            DrawHeader();
            DrawQueue();
            hoveredPreview = null;
            hoveredPreviewTarget = "";
            DrawTables();
            DrawDetails();
            DrawRecentEvents();
            DrawDebugControls();
            DrawEndOverlay();
        }

        private void StartDay()
        {
            simulation = new DaySimulation();
            telemetry = new LocalTelemetry();
            simulation.EventLog.EventAdded += telemetry.Record;
            simulation.EventLog.EventAdded += OnSimulationEvent;
            simulation.Initialize(content.Days[selectedDayIndex], content, Environment.TickCount);
            selectedCustomerId = null;
            detailsCustomerId = null;
            paused = false;
            speed = 1f;
            autoPlay = false;
            autoPlayCooldown = 0f;
            recentEvents.Clear();
            savedTelemetryPath = null;
        }

        private void DrawBackground()
        {
            GUI.DrawTexture(new Rect(0, 0, 1600, 900), darkTexture);
            GUI.Box(new Rect(20, 90, 430, 650), GUIContent.none);
            GUI.Box(new Rect(470, 90, 810, 650), GUIContent.none);
            GUI.Box(new Rect(1300, 90, 280, 650), GUIContent.none);
        }

        private void DrawHeader()
        {
            var state = simulation.State;
            GUI.Label(new Rect(25, 15, 650, 55), "PATRONS, RUMORS & ALE", titleStyle);
            GUI.Label(new Rect(700, 20, 260, 40), $"Time  {FormatTime(state.RemainingSeconds)}", headingStyle);
            GUI.Label(new Rect(970, 20, 260, 40), $"Money  {state.Economy.Money} / {state.MoneyGoal}", headingStyle);
            GUI.Label(new Rect(1240, 20, 330, 40), $"Status  {state.Status}", headingStyle);
            GUI.Label(new Rect(520, 60, 1040, 25),
                $"{simulation.Day.displayName}  |  REP  " +
                $"S {state.Reputation.Get(FactionId.Sarmatians):+0;-0;0}  " +
                $"B {state.Reputation.Get(FactionId.Moonshiners):+0;-0;0}  " +
                $"R {state.Reputation.Get(FactionId.Revolutionaries):+0;-0;0}  " +
                $"N {state.Reputation.Get(FactionId.Neutrals):+0;-0;0}",
                smallStyle);
        }

        private void DrawQueue()
        {
            GUI.Label(new Rect(40, 105, 390, 40), $"QUEUE ({simulation.State.Queue.CustomerIds.Count})", headingStyle);
            queueScroll = GUI.BeginScrollView(
                new Rect(35, 150, 400, 570),
                queueScroll,
                new Rect(0, 0, 370, Math.Max(555, simulation.State.Queue.CustomerIds.Count * 88)));

            var index = 0;
            foreach (var id in simulation.State.Queue.CustomerIds)
            {
                var customer = simulation.State.Customers[id];
                var y = index * 88;
                var previousColor = GUI.backgroundColor;
                GUI.backgroundColor = selectedCustomerId == id ? Color.white : FactionColor(customer.Faction);
                if (GUI.Button(new Rect(0, y, 360, 76), GUIContent.none))
                {
                    selectedCustomerId = id;
                    detailsCustomerId = id;
                    simulation.LogSelection(id);
                }
                GUI.backgroundColor = previousColor;

                GUI.Label(new Rect(14, y + 8, 230, 28), $"#{id}  {customer.DisplayName}", headingStyle);
                GUI.Label(new Rect(14, y + 40, 230, 24), customer.Faction.ToString(), smallStyle);
                GUI.Label(new Rect(245, y + 10, 100, 24), Mood(customer), centeredStyle);
                GUI.Label(new Rect(235, y + 40, 115, 24), $"{customer.PatienceRemaining:0}s", centeredStyle);
                index++;
            }
            GUI.EndScrollView();
        }

        private void DrawTables()
        {
            GUI.Label(new Rect(490, 105, 760, 40), "TAVERN TABLES", headingStyle);
            var positions = new[]
            {
                new Rect(505, 165, 350, 245),
                new Rect(885, 165, 350, 245),
                new Rect(505, 440, 350, 260),
                new Rect(885, 440, 350, 260)
            };

            for (var tableIndex = 0; tableIndex < simulation.State.Tables.Count; tableIndex++)
                DrawTable(simulation.State.Tables[tableIndex], positions[tableIndex]);
        }

        private void DrawTable(TableState table, Rect area)
        {
            GUI.DrawTexture(area, panelTexture);
            var tableButton = new Rect(area.x + 8, area.y + 6, area.width - 16, 38);
            var tableColor = GUI.backgroundColor;
            GUI.backgroundColor = table.FreeSeatCount > 0 && selectedCustomerId.HasValue
                ? new Color(0.55f, 0.85f, 0.55f)
                : Color.white;
            if (GUI.Button(
                    tableButton,
                    $"{table.Id.ToUpperInvariant()}  {table.OccupiedSeatCount}/{table.Seats.Count}",
                    buttonStyle) &&
                selectedCustomerId.HasValue &&
                table.FreeSeatCount > 0)
            {
                if (simulation.SeatCustomerAtTable(selectedCustomerId.Value, table.Id))
                    selectedCustomerId = null;
            }
            GUI.backgroundColor = tableColor;

            if (selectedCustomerId.HasValue &&
                table.FreeSeatCount > 0 &&
                area.Contains(Event.current.mousePosition))
            {
                hoveredPreview = simulation.PreviewPlacement(selectedCustomerId.Value, table.Id);
                hoveredPreviewTarget = table.Id;
            }

            var columns = table.Seats.Count <= 2 ? 2 : 3;
            const float seatWidth = 100f;
            const float seatHeight = 78f;
            var gapX = (area.width - columns * seatWidth) / (columns + 1);

            for (var i = 0; i < table.Seats.Count; i++)
            {
                var row = i / columns;
                var column = i % columns;
                var x = area.x + gapX + column * (seatWidth + gapX);
                var y = area.y + 55 + row * 92;
                var seat = table.Seats[i];

                if (seat.CustomerId.HasValue)
                {
                    var customer = simulation.State.Customers[seat.CustomerId.Value];
                    var oldColor = GUI.backgroundColor;
                    GUI.backgroundColor = FactionColor(customer.Faction);
                    if (GUI.Button(new Rect(x, y, seatWidth, seatHeight), $"#{customer.Id}\n{customer.DisplayName}\n{customer.StayRemaining:0}s"))
                        detailsCustomerId = customer.Id;
                    GUI.backgroundColor = oldColor;
                }
                else
                {
                    GUI.Box(new Rect(x, y, seatWidth, seatHeight), $"FREE\nSLOT {i + 1}", centeredStyle);
                }
            }
        }

        private void DrawDetails()
        {
            GUI.Label(new Rect(1320, 105, 240, 40), "PATRON DETAILS", headingStyle);
            if (!detailsCustomerId.HasValue ||
                !simulation.State.Customers.TryGetValue(detailsCustomerId.Value, out var customer))
            {
                GUI.Label(new Rect(1320, 155, 240, 80), "Select a patron to inspect them.", centeredStyle);
                DrawPlacementPreview();
                return;
            }

            GUI.backgroundColor = FactionColor(customer.Faction);
            GUI.Box(new Rect(1320, 150, 240, 145), GUIContent.none);
            GUI.backgroundColor = Color.white;
            GUI.Label(new Rect(1335, 160, 210, 30), $"#{customer.Id} {customer.DisplayName}", headingStyle);
            GUI.Label(new Rect(1335, 193, 210, 22), customer.Faction.ToString(), centeredStyle);
            GUI.Label(new Rect(1335, 218, 210, 22), $"Location: {customer.Location}", centeredStyle);
            GUI.Label(new Rect(1335, 243, 210, 22),
                customer.Location == CustomerLocation.Queue
                    ? $"Patience: {customer.PatienceRemaining:0.0}s"
                    : $"Stay: {customer.StayRemaining:0.0}s",
                centeredStyle);
            GUI.Label(new Rect(1335, 268, 210, 22), $"Base spend: {customer.BaseSpend}", centeredStyle);

            if (customer.Location == CustomerLocation.Queue &&
                GUI.Button(new Rect(1340, 302, 200, 38), "REJECT", buttonStyle))
            {
                simulation.RejectCustomer(customer.Id);
                if (selectedCustomerId == customer.Id)
                    selectedCustomerId = null;
            }

            DrawPlacementPreview();
        }

        private void DrawPlacementPreview()
        {
            GUI.Label(new Rect(1320, 350, 240, 30), "PLACEMENT PREVIEW", headingStyle);
            if (!selectedCustomerId.HasValue)
            {
                GUI.Label(new Rect(1320, 385, 240, 100),
                    "Select a queued patron, then point at a table.", centeredStyle);
                return;
            }

            if (hoveredPreview == null)
            {
                GUI.Label(new Rect(1320, 385, 240, 100), "Point at a table with a free place.", centeredStyle);
                return;
            }

            var bonuses = hoveredPreview.ActiveBonuses.Count == 0
                ? "none"
                : string.Join(", ", hoveredPreview.ActiveBonuses);
            var factions = hoveredPreview.PresentFactions.Count == 0
                ? "empty table"
                : string.Join(", ", hoveredPreview.PresentFactions);
            var placement = hoveredPreview.IsGoodSeating
                ? "faction bonus active"
                : "neutral seating, no faction bonus";
            GUI.Label(new Rect(1320, 382, 240, 125),
                $"{hoveredPreviewTarget}\n" +
                $"At table {hoveredPreview.CurrentCustomerCount}, free {hoveredPreview.FreeSeats}\n" +
                $"Factions: {factions}\n" +
                $"{placement}\n" +
                $"Spend x{hoveredPreview.SpendMultiplier:0.00} | Stay x{hoveredPreview.StayMultiplier:0.00}\n" +
                $"Reputation {hoveredPreview.ReputationDelta:+0.0;-0.0;0.0}\n" +
                $"Bonuses: {bonuses}",
                centeredStyle);
        }

        private void DrawRecentEvents()
        {
            GUI.Label(new Rect(1320, 510, 240, 30), "RECENT EVENTS", headingStyle);
            var first = Math.Max(0, recentEvents.Count - 7);
            for (var i = first; i < recentEvents.Count; i++)
                GUI.Label(new Rect(1320, 545 + (i - first) * 25, 240, 24), recentEvents[i], smallStyle);
        }

        private void DrawDebugControls()
        {
            GUI.Box(new Rect(20, 760, 1560, 120), GUIContent.none);
            GUI.Label(new Rect(35, 772, 180, 35), "DEBUG", headingStyle);

            if (GUI.Button(new Rect(210, 775, 150, 45), paused ? "RESUME" : "PAUSE", buttonStyle))
                paused = !paused;
            if (GUI.Button(new Rect(375, 775, 90, 45), "x1", buttonStyle))
                speed = 1f;
            if (GUI.Button(new Rect(475, 775, 90, 45), "x2", buttonStyle))
                speed = 2f;
            if (GUI.Button(new Rect(575, 775, 90, 45), "x5", buttonStyle))
                speed = 5f;
            if (GUI.Button(new Rect(680, 775, 220, 45), "SKIP NEXT EVENT", buttonStyle) &&
                simulation.State.Status == DayStatus.Running)
                simulation.SkipToNextEvent();
            if (GUI.Button(new Rect(915, 775, 150, 45), "RESTART", buttonStyle))
                StartDay();
            if (GUI.Button(new Rect(1075, 775, 170, 45), "SAVE LOG", buttonStyle))
                savedTelemetryPath = telemetry.SaveToJson();
            if (GUI.Button(new Rect(1255, 775, 145, 45), autoPlay ? "AUTO: ON" : "AUTO: OFF", buttonStyle))
                autoPlay = !autoPlay;
            if (GUI.Button(new Rect(1410, 775, 150, 45), autoPlayStrategy.ToString(), buttonStyle))
                autoPlayStrategy = (AutoPlayStrategy)(((int)autoPlayStrategy + 1) %
                    Enum.GetValues(typeof(AutoPlayStrategy)).Length);

            GUI.Label(new Rect(35, 835, 150, 28), "DAY:", smallStyle);
            for (var i = 0; i < content.Days.Count; i++)
            {
                var oldColor = GUI.backgroundColor;
                if (i == selectedDayIndex)
                    GUI.backgroundColor = new Color(0.65f, 0.85f, 0.65f);
                if (GUI.Button(new Rect(90 + i * 285, 830, 275, 35), content.Days[i].id))
                {
                    selectedDayIndex = i;
                    StartDay();
                }
                GUI.backgroundColor = oldColor;
            }
            GUI.Label(new Rect(960, 835, 580, 28),
                !string.IsNullOrEmpty(savedTelemetryPath)
                    ? savedTelemetryPath
                    : $"Speed x{speed:0} | Events {telemetry.Count}",
                smallStyle);
        }

        private void DrawEndOverlay()
        {
            if (simulation.State.Status == DayStatus.Running)
                return;
            var summary = simulation.GetDaySummary();
            GUI.Box(new Rect(250, 90, 1100, 700), GUIContent.none);
            GUI.Label(new Rect(280, 110, 1040, 55),
                summary.Status == DayStatus.Completed ? "DAY COMPLETED" : "DAY FAILED", titleStyle);

            summaryScroll = GUI.BeginScrollView(
                new Rect(290, 175, 1020, 500), summaryScroll, new Rect(0, 0, 990, 700));
            GUI.Label(new Rect(0, 0, 990, 35),
                $"Money {summary.MoneyEarned} / {summary.MoneyGoal} | Served {summary.ServedCustomers} | " +
                $"Impatient {summary.ImpatientDepartures} | Rejected {summary.RejectedCustomers} | " +
                $"Missed {summary.MissedCustomers}", headingStyle);
            GUI.Label(new Rect(0, 45, 990, 30),
                $"Avg wait {summary.AverageWaitSeconds:0.0}s | Avg stay {summary.AverageStaySeconds:0.0}s | " +
                $"Best table {(string.IsNullOrEmpty(summary.BestEarningTableId) ? "none" : summary.BestEarningTableId)} " +
                $"({summary.BestEarningTableMoney}) | Bonuses {summary.ActivatedFactionBonuses}", centeredStyle);

            GUI.Label(new Rect(0, 90, 480, 30), "FINAL REPUTATION", headingStyle);
            var row = 0;
            foreach (FactionId faction in Enum.GetValues(typeof(FactionId)))
            {
                GUI.Label(new Rect(30, 125 + row * 30, 420, 26),
                    $"{faction}: {summary.FinalReputation[faction]:+0.0;-0.0;0.0}", smallStyle);
                row++;
            }

            GUI.Label(new Rect(500, 90, 460, 30), "REPUTATION CHANGES BY REASON", headingStyle);
            row = 0;
            foreach (var metric in summary.ReputationChanges)
            {
                GUI.Label(new Rect(520, 125 + row * 28, 430, 25),
                    $"{metric.Faction} / {metric.Reason}: {metric.Delta:+0.0;-0.0;0.0}", smallStyle);
                row++;
            }
            GUI.EndScrollView();

            if (GUI.Button(new Rect(650, 700, 300, 55), "PLAY AGAIN", buttonStyle))
                StartDay();
        }

        private void OnSimulationEvent(SimulationEvent simulationEvent)
        {
            var message = FormatEvent(simulationEvent);
            if (message.Length == 0)
                return;
            recentEvents.Add($"{FormatTime(simulationEvent.Time)} {message}");
            if (recentEvents.Count > 30)
                recentEvents.RemoveAt(0);
        }

        private static string FormatEvent(SimulationEvent simulationEvent)
        {
            switch (simulationEvent.Type)
            {
                case "customer_seated": return $"Customer #{simulationEvent.CustomerId} seated.";
                case "customer_left_queue_impatient": return $"Customer #{simulationEvent.CustomerId} left queue.";
                case "missed_customer": return $"Queue full: missed {simulationEvent.Detail}.";
                case "customer_left_table": return $"Customer #{simulationEvent.CustomerId} left table.";
                case "money_earned": return $"+{simulationEvent.Value:0} money at {simulationEvent.Detail}.";
                case "reputation_changed":
                    return $"Reputation {simulationEvent.Detail} {simulationEvent.Value:+0.0;-0.0;0.0}.";
                case "faction_bonus_activated": return $"Bonus ON: {simulationEvent.Detail}.";
                case "faction_bonus_deactivated": return $"Bonus OFF: {simulationEvent.Detail}.";
                case "goal_completed": return "Day goal completed.";
                case "day_failed": return "Day goal failed.";
                default: return "";
            }
        }

        private void EnsureStyles()
        {
            if (titleStyle != null)
                return;

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.86f, 0.48f) }
            };
            headingStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            centeredStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                wordWrap = true
            };
            smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold
            };
            panelTexture = MakeTexture(new Color(0.24f, 0.15f, 0.09f));
            darkTexture = MakeTexture(new Color(0.08f, 0.055f, 0.04f));
            factionTextures[FactionId.Sarmatians] = MakeTexture(FactionColor(FactionId.Sarmatians));
            factionTextures[FactionId.Moonshiners] = MakeTexture(FactionColor(FactionId.Moonshiners));
            factionTextures[FactionId.Revolutionaries] = MakeTexture(FactionColor(FactionId.Revolutionaries));
            factionTextures[FactionId.Neutrals] = MakeTexture(FactionColor(FactionId.Neutrals));
        }

        private static Texture2D MakeTexture(Color color)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private static Color FactionColor(FactionId faction)
        {
            switch (faction)
            {
                case FactionId.Sarmatians: return new Color(0.72f, 0.48f, 0.14f);
                case FactionId.Moonshiners: return new Color(0.25f, 0.55f, 0.28f);
                case FactionId.Revolutionaries: return new Color(0.68f, 0.18f, 0.18f);
                default: return new Color(0.35f, 0.42f, 0.52f);
            }
        }

        private static string Mood(CustomerInstance customer)
        {
            if (customer.Location == CustomerLocation.Table)
                return ":)";
            if (customer.PatienceRemaining < 10f)
                return ">:(";
            if (customer.PatienceRemaining < 25f)
                return ":|";
            return ":)";
        }

        private static string FormatTime(float seconds)
            => $"{(int)seconds / 60:00}:{(int)seconds % 60:00}";
    }
}
