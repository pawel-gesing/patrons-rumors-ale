# Patrons, Rumors & Ale - prototype v0.2

Pierwszy grywalny prototyp desktopowy gry mobilnej w Unity 6. Sprawdza petle:
kolejka -> wybor klienta -> sadzanie -> pobyt -> pieniadze -> reputacja frakcyjna.

## Uruchomienie

1. Otworz katalog projektu w Unity Hub przy uzyciu Unity `6000.4.10f1`.
2. Otworz scene `Assets/Scenes/PrototypeTavern.unity`.
3. Ustaw okno Game na proporcje `16:9` i nacisnij Play.

Interfejs jest obslugiwany mysza. Kliknij klienta w kolejce, a potem wolne miejsce.
Klikniecie klienta pokazuje szczegoly. Dolny panel zawiera pauze, predkosci
`x1/x2/x5`, przejscie do nastepnego zdarzenia, restart dnia, wybor dnia,
auto-play i zapis telemetrii.

Po wybraniu klienta najedz kursorem na wolne miejsce, aby zobaczyc przewidywany
mnoznik wydatkow i pobytu, reputacje oraz bonusy. Panel `Recent Events` wyjasnia
zmiany pieniedzy, reputacji i bonusow. Po zakonczeniu dnia pojawia sie pelne
podsumowanie metryk.

## Testy

W Unity wybierz `Window > General > Test Runner`, zakladke EditMode i `Run All`.
Testy obejmuja kolejke, zajmowanie miejsc, platnosci, zasady frakcji, preview,
metryki i summary dnia, grupowanie zmian reputacji, ladowanie contentu,
niezaleznosc od ID dnia oraz duze kroki czasu.

Z linii polecen:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.10f1\Editor\Unity.exe" `
  -batchmode -projectPath . -runTests -testPlatform EditMode `
  -testResults TestResults.xml
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

## Reczne testowanie trzech dni

Dzien wybiera sie przyciskami na dole ekranu. Zmiana dnia od razu restartuje
symulacje z wybranym contentem.

1. `day_001_intro`: obsluz mieszana kolejke i sprawdz, czy niski cel jest
   osiagalny bez idealnego grupowania. Uzyj preview dla kazdej frakcji.
2. `day_002_sarmatian_evening`: grupuj Sarmatow przy stole 4- lub 6-osobowym.
   Porownaj mnozniki i wynik z rozrzucaniem ich po malych stolikach.
3. `day_003_crowded_agitation`: lacz Rewolucjonistow z Neutralnymi i obserwuj
   odejscia z kolejki. Sprawdz metryki oczekiwania i impatient departures.

Kazdy dzien mozna uruchomic w trybie `AUTO`. Kolejny przycisk zmienia strategie:
`FirstAvailable`, `MatchFaction`, `SarmatianGreed` lub
`RevolutionaryAgitation`. Predkosc `x5` pozwala szybko porownac summary.

## Changelog v0.2

- Dodano Day Summary z wynikiem, reputacja, przyczynami zmian i metrykami ruchu.
- Dodano Placement Preview oraz tekstowy panel ostatnich zdarzen.
- Dodano trzy dni JSON z wagami frakcji, grup i osobnym tempem przyjsc.
- Przeniesiono parametry gameplayowe do JSON i rozszerzono walidacje contentu.
- Dodano `DayMetricsCollector` i cztery debugowe strategie auto-play.
- Rozszerzono zestaw testow EditMode dla funkcji v0.2.

## Poza zakresem

Projekt celowo nie zawiera kampanii, VIP-ow, rozwoju karczmy, kont, backendu,
zakupow, finalnej grafiki ani zapisu postepu miedzy kampaniami.
