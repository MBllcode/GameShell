# GameShell - Project State

Aktualne, robocze podsumowanie stanu projektu.

## Stack i architektura

- .NET 9 + WPF (`GameShell.Desktop`) jako host UI.
- React + TypeScript + Vite (`GameShell.Ui`) renderowane w WebView2.
- Logika domenowa i workflow w `GameShell.Core`.
- Integracje Windows i DisplayConfig API w `GameShell.Windows`.

## Tryby aplikacji

- `Console Mode`: aktywuje jeden wybrany ekran konsolowy.
- `PC Gaming`: stosuje zapisany profil ekranow (fallback do Console Mode).
- `Work Mode`: przywraca `WorkReturnLayout` zapisany przed sesja gamingowa.

## Stan UI

- Po pierwszej konfiguracji aplikacja otwiera dashboard z profilem.
- Dodawanie profilu prowadzi przez flow profilowe: `Ekrany -> Launcher -> Zachowanie profilu -> Skroty profilu -> Zakonczenie`.
- Krok `Ekrany`:
  - wymaga nazwy profilu,
  - wymaga wyboru co najmniej jednego ekranu,
  - pokazuje te bledy lokalnie i niezaleznie (osobne komunikaty),
  - ogranicza profil do maksymalnie 2 ekranow (inline warning LIMIT).
- Draft konfiguracji ekranow jest commitowany deterministycznie przed zapisem i przy kluczowej nawigacji.
- Przy tworzeniu nowego profilu hotkey profilu startuje zawsze jako pusty.
- Cofanie `Wroc` w flow tworzenia profilu nie pokazuje modala o niezapisanych zmianach.
- Modal niezapisanych zmian przy tworzeniu profilu pojawia sie dopiero przy probie wyjscia do `Panel glowny` po co najmniej jednym przejsciu `Dalej`.

## Hotkeye profili

- Kazdy profil ma wlasny hotkey wejscia.
- Kombinacje sa normalizowane do kanonicznej postaci, bez polskich znakow diakrytycznych.
- Ta sama kombinacja nie moze byc przypisana do dwoch roznych profili.
- Podczas `NASLUCHIWANIE` globalne hotkeye aplikacji sa chwilowo wyrejestrowane, aby UI moglo przechwycic klawisze i pokazac lokalna walidacje duplikatu.

## Zachowanie zapisu

- Brak `window.confirm` dla standardowego `Zapisz zmiany`.
- Modal nadpisania pojawia sie tylko przy rzeczywistym konflikcie nazwy z innym profilem.
- Launcher i ustawienia profilu nie sa nadpisywane starym snapshotem po edycji ekranow.

## Desktop host i runtime

- Tray menu: Open, Enter Console, Enter PC Gaming, Enter Work, Restore previous display layout, Exit.
- CLI: `--gaming`, `--pcgaming`, `--work`.
- Single-instance forwarding: druga instancja przekazuje komende do dzialajacej.
- Domyslny rozmiar okna: 1360x860, minimum 1040x720, z zapamietywaniem i bezpiecznym clampem pozycji.
- `Startuj z Windows` ustawia wpis autostartu Windows dla aktualnej formy uruchomienia aplikacji.
- `Startuj zminimalizowany do tray` jest stosowane przy zwyklym starcie aplikacji, nie tylko w fallback scenariuszach.

## Build, test, package

- Build calosci: `./build.ps1`
- Testy: `dotnet test GameShell.sln`
- Pakowanie EXE: `./package.ps1`
- Finalny artefakt: `artifacts/installer/GameShell.exe`
- Jesli EXE jest zablokowany, `package.ps1` tworzy fallback `GameShell-<timestamp>.exe`.

## Utrzymanie repo

- Trzymamy aktualne bundlowane assety UI w `src/GameShell.Desktop/wwwroot`.
- Fallback EXE z timestampem (`artifacts/installer/GameShell-*.exe`) sa traktowane jako tymczasowe artefakty i sa ignorowane przez git.
- Aktualny screenshot dashboardu trzymamy jako `docs/panel-glowny.png`.
