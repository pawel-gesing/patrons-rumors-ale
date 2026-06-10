# Patrons, Rumors & Ale - prototype v0.1

Pierwszy grywalny prototyp desktopowy gry mobilnej w Unity 6. Sprawdza petle:
kolejka -> wybor klienta -> sadzanie -> pobyt -> pieniadze -> reputacja frakcyjna.

## Uruchomienie

1. Otworz katalog projektu w Unity Hub przy uzyciu Unity `6000.4.10f1`.
2. Otworz scene `Assets/Scenes/PrototypeTavern.unity`.
3. Ustaw okno Game na proporcje `16:9` i nacisnij Play.

Interfejs jest obslugiwany mysza. Kliknij klienta w kolejce, a potem wolne miejsce.
Klikniecie klienta pokazuje szczegoly. Dolny panel zawiera pauze, predkosci
`x1/x2/x5`, przejscie do nastepnego zdarzenia, restart dnia i zapis telemetrii.

## Testy

W Unity wybierz `Window > General > Test Runner`, zakladke EditMode i `Run All`.
Testy obejmuja kolejke, zajmowanie miejsc, platnosci, zasady trzech frakcji,
ograniczenia reputacji, dryf reputacji oraz cel dnia.

Z linii polecen:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.10f1\Editor\Unity.exe" `
  -batchmode -projectPath . -runTests -testPlatform EditMode `
  -testResults TestResults.xml -quit
```

## Architektura

- `Assets/Scripts/Simulation` zawiera czysty C# bez `UnityEngine`. `DaySimulation`
  jest jedynym punktem wykonywania komend i przesuwania czasu.
- `Assets/Scripts/Content/ContentModels.cs` definiuje kontrakt danych używany
  przez symulacje. JSON-y w `Assets/StreamingAssets/content` sa zrodlem prawdy.
- `ContentLoader` jest adapterem Unity: czyta JSON przez `JsonUtility` i odrzuca
  brakujace pliki, duplikaty, nieznane frakcje oraz niepoprawne wartosci.
- `PrototypeTavernController` tylko wyswietla stan i przekazuje komendy. Ekran
  jest budowany kodem, aby prototyp nie wymagal prefabow ani finalnych assetow.
- `SimulationEventLog` emituje zdarzenia domenowe. `LocalTelemetry` przechowuje
  je w pamieci i opcjonalnie zapisuje JSON do `Application.persistentDataPath`.
- Losowosc jest deterministyczna dla podanego ziarna. Pozwala to odtwarzac
  przebieg dnia w testach i przyszlych narzedziach balansujacych.

## Zakres v0.1

Projekt celowo nie zawiera kampanii, VIP-ow, rozwoju karczmy, kont, backendu,
zakupow, finalnej grafiki ani zapisu postepu miedzy kampaniami.
