# Patrons, Rumors & Ale - prototype v0.3

Grywalny prototyp desktopowy gry mobilnej w Unity 6. Sprawdza petle:
kolejka -> wybor klienta -> wybor stolika -> pobyt -> pieniadze -> reputacja
frakcyjna.

## Uruchomienie

1. Otworz katalog projektu w Unity Hub przy uzyciu Unity `6000.4.10f1`.
2. Otworz scene `Assets/Scenes/PrototypeTavern.unity`.
3. Ustaw okno Game na proporcje `16:9` i nacisnij Play.

Interfejs jest obslugiwany mysza. Kliknij dowolnego klienta w widocznej kolejce,
a potem naglowek stolika z wolnym miejscem. Symulacja automatycznie wybierze
pierwszy wolny slot techniczny. Sloty nadal pokazuja rozmieszczenie klientow,
ale gracz nie musi wybierac konkretnego krzesla.

Dzien trwa 180 sekund. Grupy 1-3 klientow przychodza co 7 sekund. Widoczna
kolejka zaczyna z 5 klientami i ma twardy limit 5 osob. Klienci, dla ktorych
brakuje miejsca, sa raportowani jako `missed_customer`; licznik
`missedCustomers` znajduje sie w Day Summary. Kara reputacji za przepelnienie
jest konfigurowana w `balance.json` i domyslnie wynosi zero.

Po wybraniu klienta najedz kursorem na stolik, aby zobaczyc jego zajetosc,
wolne miejsca, obecne frakcje, bonus frakcyjny, mnoznik wydatkow i pobytu oraz
przewidywana zmiane reputacji. Brak bonusu jest opisany jako neutralne
usadzenie, a nie konflikt stolika.

Dolny panel zawiera pauze, predkosci `x1/x2/x5`, przejscie do nastepnego
zdarzenia, restart dnia, wybor dnia, auto-play i zapis lokalnej telemetrii.

## Testy

W Unity wybierz `Window > General > Test Runner`, zakladke EditMode i `Run All`.
Testy obejmuja tempo dnia, grupy przyjsc, limit kolejki, overflow, sadzanie przy
stoliku, Placement Preview, zasady frakcji, reputacje, metryki oraz auto-play.

Z linii polecen:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.10f1\Editor\Unity.exe" `
  -batchmode -projectPath . -runTests -testPlatform EditMode `
  -testResults TestResults.xml
```

## Architektura

- `Assets/Scripts/Simulation` zawiera czysty C# bez `UnityEngine`. `DaySimulation`
  jest jedynym punktem wykonywania komend i przesuwania czasu.
- `Assets/Scripts/Content/ContentModels.cs` definiuje kontrakt danych uzywany
  przez symulacje. JSON-y w `Assets/StreamingAssets/content` sa zrodlem prawdy.
- Frakcje definiuja bazowa cierpliwosc, czas pobytu i wydatek. Archetypy sa
  metadanymi klimatycznymi: nazwa, opis, placeholder sprite'a i przyszly wpis
  encyklopedii.
- `ContentLoader` czyta JSON przez `JsonUtility` i odrzuca brakujace pliki,
  duplikaty, nieznane frakcje, grupy spoza zakresu 1-3 oraz niepoprawne wartosci.
- `PrototypeTavernController` wyswietla stan i przekazuje komendy wyboru stolika.
- Losowosc jest deterministyczna dla podanego ziarna.

## Reczne testowanie

1. Uruchom `day_001_intro`. Potwierdz czas `03:00`, startowa kolejke 5/5 i nowe
   przyjscia co 7 sekund. Zaczekaj z sadzaniem, aby zobaczyc `missed_customer`.
2. Wybierz dowolna osobe z kolejki i kliknij naglowek stolika. Sprawdz, czy
   zajetosc zmienia sie np. z `1/4` na `2/4`, a pelny stolik nie przyjmuje osoby.
3. Grupuj Sarmatow. Preview powinien pokazac bonus wydatkow i pobytu. Sarmata
   z Neutralnym pozostaje poprawnie usadzony, ale nie dostaje pelnego bonusu.
4. Sadzaj Rewolucjonistow z Neutralnymi. Bonus pobytu powinien byc wiekszy niz
   przy stole z samymi Rewolucjonistami.
5. Posadz Bimbrownika i porownaj pozniejsze przychody. Globalny bonus rosnie
   tylko do limitu skonfigurowanego w `balance.json`.
6. Zakoncz dzien i sprawdz pieniadze, reputacje, served/rejected/impatient,
   `missedCustomers`, srednie czasy, najlepszy stolik i aktywowane bonusy.

## Auto-play

Przycisk `AUTO` wlacza automatyczne sadzanie. Sasiedni przycisk zmienia strategie:

- `FirstAvailable`: pierwszy klient i pierwszy stolik z wolnym miejscem.
- `MatchFaction`: preferuje stolik z ta sama frakcja.
- `SarmatianGreed`: grupuje Sarmatow.
- `RevolutionaryAgitation`: laczy Rewolucjonistow z Neutralnymi.

Predkosc `x5` pozwala szybko porownac Day Summary dla trzech dni. Projekt nie
zawiera jeszcze osobnego eksportu batch; dane potrzebne do przyszlego eksportu
sa dostepne w `DaySummary` oraz stanie dnia.

## Changelog v0.3

- Skrocono wszystkie dni testowe do 180 sekund i ustawiono przyjscia co 7 sekund.
- Dodano grupy 1-3, startowa kolejke 5 osob i twardy limit widocznej kolejki 5.
- Dodano `missedCustomers`, zdarzenie `missed_customer` i konfigurowalna kare
  reputacji za overflow.
- Zmieniono interakcje z wyboru miejsca na wybor stolika.
- Rozszerzono Placement Preview o stan calego stolika i neutralne usadzenie.
- Uporzadkowano zasady Sarmatow, Rewolucjonistow, Bimbrownikow i Neutralnych.
- Oddzielono klimatyczne archetypy od bazowych parametrow mechanicznych frakcji.
- Dostosowano cztery strategie auto-play i zestaw testow EditMode.

## Poza zakresem

Projekt celowo nie zawiera VIP-ow, encyklopedii, tutoriala fabularnego, finalnych
sprite'ow, online telemetry, mobile release pipeline, rozwoju karczmy, zapisu
kampanii, osobnego systemu konfliktow stolikow ani indywidualnych potrzeb klienta.
