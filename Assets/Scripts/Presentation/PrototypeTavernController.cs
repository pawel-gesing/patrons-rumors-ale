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
        private string startupError;
        private string savedTelemetryPath;
        private Vector2 queueScroll;
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
            DrawTables();
            DrawDetails();
            DrawDebugControls();
            DrawEndOverlay();
        }

        private void StartDay()
        {
            simulation = new DaySimulation();
            telemetry = new LocalTelemetry();
            simulation.EventLog.EventAdded += telemetry.Record;
            simulation.Initialize(content.Days[0], content, Environment.TickCount);
            selectedCustomerId = null;
            detailsCustomerId = null;
            paused = false;
            speed = 1f;
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
            GUI.Label(new Rect(area.x + 10, area.y + 8, area.width - 20, 32), table.Id.ToUpperInvariant(), headingStyle);

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
                    GUI.backgroundColor = selectedCustomerId.HasValue ? new Color(0.55f, 0.85f, 0.55f) : Color.gray;
                    if (GUI.Button(new Rect(x, y, seatWidth, seatHeight), "FREE\nSEAT") && selectedCustomerId.HasValue)
                    {
                        if (simulation.SeatCustomer(selectedCustomerId.Value, table.Id, i))
                            selectedCustomerId = null;
                    }
                    GUI.backgroundColor = Color.white;
                }
            }
        }

        private void DrawDetails()
        {
            GUI.Label(new Rect(1320, 105, 240, 40), "PATRON DETAILS", headingStyle);
            if (!detailsCustomerId.HasValue ||
                !simulation.State.Customers.TryGetValue(detailsCustomerId.Value, out var customer))
            {
                GUI.Label(new Rect(1320, 165, 240, 100), "Select a patron to inspect them.", centeredStyle);
                DrawReputation(1320, 470);
                return;
            }

            GUI.backgroundColor = FactionColor(customer.Faction);
            GUI.Box(new Rect(1320, 160, 240, 210), GUIContent.none);
            GUI.backgroundColor = Color.white;
            GUI.Label(new Rect(1335, 175, 210, 35), $"#{customer.Id} {customer.DisplayName}", headingStyle);
            GUI.Label(new Rect(1335, 220, 210, 26), customer.Faction.ToString(), centeredStyle);
            GUI.Label(new Rect(1335, 255, 210, 26), $"Location: {customer.Location}", centeredStyle);
            GUI.Label(new Rect(1335, 290, 210, 26),
                customer.Location == CustomerLocation.Queue
                    ? $"Patience: {customer.PatienceRemaining:0.0}s"
                    : $"Stay: {customer.StayRemaining:0.0}s",
                centeredStyle);
            GUI.Label(new Rect(1335, 325, 210, 26), $"Base spend: {customer.BaseSpend}", centeredStyle);

            if (customer.Location == CustomerLocation.Queue &&
                GUI.Button(new Rect(1340, 390, 200, 55), "REJECT", buttonStyle))
            {
                simulation.RejectCustomer(customer.Id);
                if (selectedCustomerId == customer.Id)
                    selectedCustomerId = null;
            }

            DrawReputation(1320, 470);
        }

        private void DrawReputation(float x, float y)
        {
            GUI.Label(new Rect(x, y, 240, 35), "REPUTATION", headingStyle);
            var row = 0;
            foreach (FactionId faction in Enum.GetValues(typeof(FactionId)))
            {
                var value = simulation.State.Reputation.Get(faction);
                GUI.backgroundColor = FactionColor(faction);
                GUI.Box(new Rect(x, y + 45 + row * 42, 240, 34), GUIContent.none);
                GUI.backgroundColor = Color.white;
                GUI.Label(new Rect(x + 8, y + 49 + row * 42, 165, 26), faction.ToString(), smallStyle);
                GUI.Label(new Rect(x + 175, y + 49 + row * 42, 55, 26), value.ToString("+0.0;-0.0;0.0"), centeredStyle);
                row++;
            }
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
            if (GUI.Button(new Rect(915, 775, 190, 45), "RESTART DAY", buttonStyle))
                StartDay();
            if (GUI.Button(new Rect(1120, 775, 220, 45), "SAVE TELEMETRY", buttonStyle))
                savedTelemetryPath = telemetry.SaveToJson();

            GUI.Label(new Rect(210, 835, 420, 28), $"Speed: x{speed:0} | Events: {telemetry.Count}", smallStyle);
            if (!string.IsNullOrEmpty(savedTelemetryPath))
                GUI.Label(new Rect(650, 835, 890, 28), savedTelemetryPath, smallStyle);
        }

        private void DrawEndOverlay()
        {
            if (simulation.State.Status == DayStatus.Running)
                return;
            GUI.Box(new Rect(475, 280, 650, 260), GUIContent.none);
            GUI.Label(new Rect(500, 310, 600, 60),
                simulation.State.Status == DayStatus.Completed ? "DAY COMPLETED" : "DAY FAILED",
                titleStyle);
            GUI.Label(new Rect(500, 390, 600, 40),
                $"Earned {simulation.State.Economy.Money} / {simulation.State.MoneyGoal}",
                headingStyle);
            if (GUI.Button(new Rect(650, 465, 300, 55), "PLAY AGAIN", buttonStyle))
                StartDay();
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

