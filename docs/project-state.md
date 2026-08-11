# GameShell - Project State

Aktualne, robocze podsumowanie stanu projektu.

## Stack i architektura

- .NET 9 + WPF (`GameShell.Desktop`) jako host UI.
- React + TypeScript + Vite (`GameShell.Ui`) renderowane w WebView2.
- Logika domenowa i workflow w `GameShell.Core`.
- Integracje Windows i DisplayConfig API w `GameShell.Windows`.

## Stabilnosc DisplayConfig

- Display subsystem zostal przywrocony do ostatniego znanego stabilnego stanu z commita `3f9cfb6` (`Initial GameShell snapshot with dynamic profile behaviour`).
- Runtime uzywa klasycznych flag `QDC_ONLY_ACTIVE_PATHS`, `QDC_ALL_PATHS` oraz `QDC_DATABASE_CURRENT`; nie uzywa `QDC_VIRTUAL_MODE_AWARE`.
- Apply uzywa klasycznych `DISPLAYCONFIG_PATH_INFO` i `DISPLAYCONFIG_MODE_INFO` z legacy `ModeInfoIdx` oraz flag `SDC_USE_SUPPLIED_DISPLAY_CONFIG`, `SDC_ALLOW_CHANGES`, `SDC_VALIDATE`, `SDC_APPLY` i `SDC_SAVE_TO_DATABASE`.
- Eksperymentalne virtual-aware flags, packed virtual indices, runtime/discovery merge i dodatkowa walidacja virtual indices nie sa aktywne.
- ABI native struktur DisplayConfig jest chronione testem rozmiarow: source `20`, target `48`, path `72` bajty.
- Problem identyfikacji Samsung S90F pozostaje odroczony i nie jest czescia stabilizacji display switching.

## Profile ekranow i topology

- Profile topology przechowuje jawna `RefreshPolicy`: `AutoHighest` jest domyslne, a `Fixed` istnieje tylko dla explicit wyboru stalego trybu w nowym UI.
- Legacy topology bez `refreshPolicy` jest migrowane do `AutoHighest`; stare pola exact refresh nie prowadza juz wyboru w UI i sa czyszczone przy zapisie.
- Edytor `Profile Setup > Ekrany` ma osobna warstwe geometrii w `src/GameShell.Ui/src/utils/displayTopologyGeometry.ts`. Canvas skaluje tylko prezentacje, a zapis zachowuje rzeczywiste `PositionX`/`PositionY` topology.
- Canvas obsluguje ujemne wspolrzedne, proporcjonalny rozmiar monitorow, snap krawedzi i alignment oraz deterministyczne usuwanie overlapu. Ekrany disabled nadal uczestnicza w geometrii.
- Przed apply snapshot jest normalizowany względem aktywnego primary: primary trafia do `0,0`, a pozostale pozycje zachowuja relacje wzajemne.
- Przed budowa konfiguracji natywnej wykonywana jest walidacja jednego primary, rozmiarow, overlapu i duplikatow targetow. Niepoprawna topologia nie wywoluje `SetDisplayConfig`.
- Log `[DISPLAY TOPOLOGY APPLY]` pokazuje dokładne wspolrzedne snapshotu przekazane do buildera natywnego.

## Frontend po refaktorze

- `App.tsx` jest wyłącznie kompozycją stanu, feature coordinatorów i `AppView`.
- `features/dashboard` posiada dashboardowy model i akcje, w tym rozpoczęcie oraz edycję flow profilu.
- `features/profiles` posiada edycję profili, mutacje i stan konfiguracji ekranów.
- `features/settings` posiada formularz ustawień, hydratację, budowanie payloadu i persystencję.
- `features/setup` posiada `setupFlow.ts`, stan kreatora, nawigację, zapis oraz model strony setupu.
- Przeniesienie plików nie zmienia publicznych API hooków, UI, bridge ani kompozycji `App.tsx`.

## Encoding UI

- Źródłowy problem mojibake był fizycznie w dwóch plikach wydzielonych po refaktorze: `useDashboardSetupActions.ts` i `setupFlow.ts`.
- Przywrócono historyczne polskie stringi bez zmiany copy ani znaczenia.
- Wszystkie źródła frontendowe `.ts`, `.tsx`, `.css`, `.html` i `.json` przechodzą ścisłe dekodowanie UTF-8.
- `src/GameShell.Ui/index.html` zawiera pojedynczy charset UTF-8.
- WebView2 ładuje statyczny build przez virtual host, a bridge używa natywnego JSON Unicode; nie ma ręcznych konwersji ANSI/ASCII.
- Regression check: `npm run test:encoding`.

## Tryby aplikacji

- `Console Mode`: aktywuje jeden wybrany ekran konsolowy.
- `PC Gaming`: stosuje zapisany profil ekranow (fallback do Console Mode).
- `Work Mode`: przywraca `WorkReturnLayout` zapisany przed sesja gamingowa.

## Console Session i launcher lifecycle

- `ConsoleSessionManager` jest warstwa aplikacyjna nad `ModeOrchestrator`; nie kopiuje logiki DisplayConfig, launchera ani Work Mode.
- Wszystkie wejscia profilu (dashboard, tray, hotkey, kontroler i single-instance) nadal przechodza przez `IModeCommandService`, ktory deleguje operacje sesji do jednego managera.
- Stany sesji: `Idle`, `EnteringProfile`, `LauncherActive`, `LaunchingGame`, `GameActive`, `ReturningToLauncher`, `ExitingToWorkMode`, `Faulted`.
- Udana aktywacja profilu wykonuje istniejacy workflow i ustawia `LauncherActive`. Playnite zachowuje normalny lifecycle gry; GameShell nie przywraca ani nie restartuje launchera po kazdym zamknieciu gry.
- Jawne Work Mode zatrzymuje monitor, wykonuje istniejacy workflow przywracania i konczy sesje stanem `Idle`.
- Work Mode ma jeden `TransitionId` na request i loguje poczatek, granice krokow, cancellation source oraz wynik kazdego etapu.
- Token zycia monitora gamingowego jest niezalezny od tokena requestu aktywacji profilu. Wlasne anulowanie monitora podczas `StopAsync` jest oczekiwanym zakonczeniem, a nie bledem sesji.
- Przywracanie layoutu ekranow i glowna zmiana stanu sa krytyczne. Zamkniecie launchera, taskbary i ikony pulpitu sa best-effort; bounded timeout launchera jest ostrzezeniem i nie przerywa Work Mode.
- Duplikaty aktywacji sa odrzucane, przejscia sa serializowane, a bledy sa logowane i koncza sie stanem `Faulted`, z mozliwoscia ponowienia komendy.
- Runtime snapshot Work Mode jest skladany atomowo przez `ModeCommandService`: `CurrentMode=Work`, `SessionState=Idle`, `ActiveProfileName=null` i `ActiveLauncher=null`. Udany `enterWorkMode` emituje `statusChanged`, a frontend renderuje stan przede wszystkim z tego snapshotu.
- `ConsoleSessionManager` uniewaznia `SessionGeneration` przy wyjsciu z sesji gamingowej. Opoznione callbacki monitora i recovery sa ignorowane, gdy stan nie jest juz `GameActive`/`ReturningToLauncher`, wiec nie moga przywrocic `LauncherActive` po Work Mode.
- Wejscie do Work Mode z dashboardu i z pada korzysta z tego samego `IModeCommandService`. Skrot SDL3 uruchamia komendę z watku pollingu, dlatego `WebViewBridge.NotifyStatusChanged` musi publikowac event przez WPF Dispatcher; bez tego backend zmienial stan, ale WebView2 nie odswiezal sidebara.
- Przy diagnostyce przeplywu uzywamy logow `[WORKMODE REQUEST]`, `[WORKMODE SESSION COMMIT]`, `[WORKMODE BACKEND COMMIT]`, `[STATUSCHANGED EMIT]`, `[STATUSCHANGED RECEIVED]`, `[GETSTATUS RESPONSE]`, `[UI STATUS RECEIVED]` i `[DASHBOARD RUNTIME STATE]`. Log aplikacji jest w `%LOCALAPPDATA%\GameShell\Logs\gameshell-yyyy-MM-dd.log`.
- `IGameSessionMonitor` jest osobnym kontraktem. Implementacja Windows sprawdza tylko jawnie skonfigurowane `PcGaming.GameProcessNames`; pusta lista oznacza brak heurystycznego wykrywania gry.
- Po `GameDetected` GameShell zmienia wyłącznie stan sesji na `GameActive` i monitoruje zakończenie skonfigurowanego procesu. Nie modyfikuje HWND gry, jej stylu, rozmiaru, pozycji ani focusu.
- GameShell celowo nie wymusza fullscreen/windowed/borderless, aspect ratio, wewnętrznej rozdzielczości, `Alt+Enter` ani syntetycznego inputu. Te ustawienia pozostają odpowiedzialnością gry.
- `ILauncherRecoveryStrategy` dziala tylko po `GameExited`: czeka 1500 ms na wlasny powrot launchera, ponownie wyszukuje widoczne nietoolowe okno Playnite, a potem wykonuje najwyzej dwie proby `ShowWindow(SW_RESTORE)`, `BringWindowToTop` i `SetForegroundWindow`, sprawdzajac PID foregroundu po kazdej probie.
- Recovery po game exit ma bounded graceful close przed ewentualnym restartem, loguje stan okna i foregroundu bez deklarowania naprawy inputu kontrolera, nie restartuje zdrowego Playnite i nie uruchamia Work Mode automatycznie.
- `SetFocus` nie jest wywolywane z watku GameShell, bo nie jest to watek UI Playnite. Uzywane sa tylko `ShowWindow(SW_RESTORE)`, `BringWindowToTop` i `SetForegroundWindow`.
- W kodzie GameShell nie ma restartu SDL/controller service ani konfiguracji Steam Input. Dla launchera Steam logowany jest typ launchera i lista procesow, ale Steam Input nie jest zmieniany.
- Recovery ma implementacje Windows z obsluga Playnite oraz bezpiecznym fallbackiem opartym o skonfigurowany executable launchera. Nie jest czescia normalnego workflow `NativeModeWorkflowService`.
- Manager i monitor sa singletonami DI, wiec dzialaja w tray/background bez MainWindow i WebView.
- Sidebar dashboardu pokazuje niepusty stan sesji (`launcher aktywny`, `gra aktywna`, `powrot do launchera` lub `blad sesji`) odczytany z tego samego `getStatus`, ktory odswieza pozostale dane trybu.

## Stan UI

- Theme UI to GameShell Green v2: ciemne neutralne tla i surfaces z zielonym akcentem, bez Steel Blue ani neonowego glow.
- UI ma centralny motion system: 120/180/220 ms, standard/out easing, subtelne wejscie stron i dialogow oraz transitions dla sidebaru, profili, kart, inputow i statusow.
- Motion nie zmienia layoutu ani wymiarow buttonow. Przy reduced motion ruch transformacji jest wylaczany.
- Sidebar navigation wypelnia dostepna szerokosc kontenera. Tekst ma flexowe miejsce, a chevron pozostaje pionowo wycentrowany i przesuwa sie tylko poziomo.
- Dashboardowe i ustawieniowe chevrony nie uzywaja `translateY` jako efektu hoveru.
- Dropdown `Odświeżanie` w panelu właściwości ekranu ma własny chevron, `appearance: none` i `38px` prawego paddingu. Strzałka jest subtelna, wycentrowana pionowo i odsunięta o `13px` od prawej ramki bez zmiany szerokości pola ani logiki `RefreshPolicy`.

- Po pierwszej konfiguracji aplikacja otwiera dashboard z profilem.
- Opcja minimalizowania pozostałych okien enumeruje wszystkie widoczne okna top-level przez Win32 `EnumWindows`, więc obejmuje aplikacje i okna na wszystkich ekranach. Minimalizacja jest wykonywana także po przełączeniu layoutu ekranów, bezpośrednio przed uruchomieniem launchera, aby Windows nie przywrócił okien podczas zmiany konfiguracji monitorów.
- Dashboardowy sidebar pokazuje w module `STAN` realtime status kontrolera: `DualSense`, `Xbox Controller` albo `Kontroler` oraz `POLACZONY`/`Brak kontrolera`.
- Sidebar korzysta z tego samego frontendowego `controllerStatus`, ktory obsluguje skroty kontrolera; nie ma osobnego pollingu.
- Gdy wszystkie profile gamingowe zostana usuniete, lista profili pozostaje pusta. Nie jest tworzony profil `Domyslny`.
- Boxy `Profile gamingowe` i `USTAWIENIA PROFILU` pozostaja widoczne przy pustej liscie, ale akcje profilu sa wyszarzone. Dostepne pozostaja `+ Dodaj profil` i ustawienia globalne.
- Dodawanie profilu prowadzi przez flow profilowe: `Ekrany -> Launcher -> Zachowanie profilu -> Skroty profilu -> Zakonczenie`.
- Nazwy profili gamingowych sa uppercase-only: UI konwertuje wpis podczas edycji, a `AppSettingsValidator` normalizuje takze starsze ustawienia i konfiguracje przy zapisie/odczycie.
- Krok `Ekrany`:
  - wymaga nazwy profilu,
  - wymaga wyboru co najmniej jednego ekranu,
  - pokazuje te bledy lokalnie i niezaleznie (osobne komunikaty),
  - nie ma sztucznego limitu liczby ekranow w profilu; walidowane sa tylko wymagania topology i poprawnosc danych.
- Draft konfiguracji ekranow jest commitowany deterministycznie przed zapisem i przy kluczowej nawigacji.
- Przy tworzeniu nowego profilu hotkey profilu startuje zawsze jako pusty.
- Cofanie `Wroc` w flow tworzenia profilu nie pokazuje modala o niezapisanych zmianach.
- Modal niezapisanych zmian przy tworzeniu profilu pojawia sie dopiero przy probie wyjscia do `Panel glowny` po co najmniej jednym przejsciu `Dalej`.

## Hotkeye profili

- Kazdy profil ma wlasny hotkey wejscia.
- Przycisk `Usun` dla zapisanego skrotu klawiaturowego jest umieszczony po prawej stronie inputa i ma kompaktowy rozmiar zgodny ze skrotem kontrolera.
- Kombinacje sa normalizowane do kanonicznej postaci, bez polskich znakow diakrytycznych.
- Ta sama kombinacja nie moze byc przypisana do dwoch roznych profili.
- Podczas `NASLUCHIWANIE` globalne hotkeye aplikacji sa chwilowo wyrejestrowane, aby UI moglo przechwycic klawisze i pokazac lokalna walidacje duplikatu.

## Zachowanie zapisu

- Brak `window.confirm` dla standardowego `Zapisz zmiany`.
- Modal nadpisania pojawia sie tylko przy rzeczywistym konflikcie nazwy z innym profilem.
- Launcher i ustawienia profilu nie sa nadpisywane starym snapshotem po edycji ekranow.
- Jawne `null` dla pustego `pcGamingLayout` i `activePcGamingProfileName` jest zachowywane podczas zapisu, aby walidator nie odtwarzal profilu `Domyslny`.
- `SettingsService` zapisuje ustawienia atomowo: serializacja i walidacja odbywaja sie przed otwarciem pliku docelowego, nastepnie tworzony jest plik `.tmp`, flush do dysku i `File.Move` na miejsce `settings.json`.
- Backup `.bak` jest aktualizowany w bezpiecznej sekwencji; przy niepoprawnym albo uszkodzonym `settings.json` loader preferuje poprawny backup zamiast resetu do pierwszego uruchomienia.
- Pliki z uszkodzeniem sa zachowywane jako kopie `.corrupt-<timestamp>` z zachowaniem diagnostyki, a w przypadku braku poprawnego glownego pliku i backupu system tworzy domyslne ustawienia dopiero po potwierdzeniu, ze brak bezpiecznego odzyskania.
- Weryfikacja: `SettingsServiceTests` 25/25, `dotnet test GameShell.sln` 118/118, 0 failed.

## Desktop host i runtime

- Tray menu: Open, Enter Console, Enter PC Gaming, Enter Work, Restore previous display layout, Exit.
- CLI: `--gaming`, `--pcgaming`, `--work`.
- Single-instance forwarding: druga instancja przekazuje komende do dzialajacej.
- Domyslny rozmiar okna: 1360x860, minimum 1040x720, z zapamietywaniem i bezpiecznym clampem pozycji.
- `Startuj z Windows` ustawia wpis autostartu Windows dla aktualnej formy uruchomienia aplikacji.
- `Startuj zminimalizowany do tray` jest stosowane przy zwyklym starcie aplikacji, nie tylko w fallback scenariuszach.

## Build, test, package

- Build calosci: `./build.ps1`
- Build UI: `Set-Location .\src\GameShell.Ui; npm run build`
- TypeScript: `Set-Location .\src\GameShell.Ui; npx tsc -b --pretty false`
- Regression check encodingu: `Set-Location .\src\GameShell.Ui; npm run test:encoding`
- Testy: `dotnet test GameShell.sln`
- Pakowanie EXE: `./package.ps1`
- Finalny artefakt: `artifacts/installer/GameShell.exe`
- Jesli EXE jest zablokowany, `package.ps1` tworzy fallback `GameShell-<timestamp>.exe`.
- Ostatnia walidacja tego etapu: `dotnet test GameShell.sln` -> `118/118` passed, `SettingsServiceTests` -> `25/25` passed, `npm run build` oraz build .NET wykonane bez bledow; pakowanie EXE ostatnio odpalane po finalnej poprawce stanu repo.

## Utrzymanie repo

- Trzymamy aktualne bundlowane assety UI w `src/GameShell.Desktop/wwwroot`.
- Fallback EXE z timestampem (`artifacts/installer/GameShell-*.exe`) sa traktowane jako tymczasowe artefakty i sa ignorowane przez git.
- Aktualny screenshot dashboardu trzymamy jako `docs/panel-glowny.png`.

## Controller stack

The controller stack is physical controller -> SDL3 -> GameShell shortcuts and realtime status. GameShell does not intercept controller input for games and does not display an overlay over the game.

## Controller logging i retention

- `SdlControllerShortcutService` nie loguje pustego wyniku `SDL_GetGamepads` w normalnym polling hot-path.
- Status kontrolera jest porownywany jako snapshot `Connected`, `ControllerId`, `Name` i `Family`; marker `[CONTROLLER STATUS CHANGED]` pojawia sie tylko przy realnej zmianie. Licznik sekwencji nie jest zmiana sam w sobie.
- Diagnostyka pollingu capture jest na poziomie `Debug`, a realne bledy SDL sa logowane jako `Warning` z deduplikacja tego samego klucza przez 60 sekund oraz raportem liczby suppressed errors.
- File logger uzywa wspoldzielonego dostepu `FileShare.ReadWrite`, rotacji dziennej i po rozmiarze 10 MB oraz retencji 7 plikow, czyli okolo 70 MB lacznie. Wyjatki sinka nie propagują sie do pollingu ani logiki aplikacji.
- Ostateczna akceptacja wymaga recznego uruchomienia GameShell bez pada przez 10-15 minut, a potem sprawdzenia logu pod katem braku powtarzajacych sie wpisow.

## Logging audit

- Pojedyncze mapowania `DisplayConfig path mapped` oraz pelne listy trybow sa na poziomie `Debug`; Information pokazuje tylko zwarte `[DISPLAY INVENTORY]` i `[DISPLAY MODE INVENTORY]` z licznikami oraz refresh rates dla aktualnej rozdzielczosci.
- Rutynowe `[REFRESH POLICY HYDRATION]` i `FIRST RUN COMPLETION LOADED` sa na poziomie `Debug`; Information zachowuje zapisy ustawien, przejscia, apply topology, recovery oraz realne ostrzezenia i bledy.
- `SettingsService` przechowuje snapshot w pamieci. Kolejne `LoadAsync` nie otwieraja ani nie deserializuja `settings.json`, dopoki nie nastapi `SaveAsync` albo zmiana `LastWriteTimeUtc` pliku, co obsluguje takze reczna zewnetrzna edycje.
- Test cache pokrywa ponowne uzycie snapshotu i reload po zmianie pliku. Pelna akceptacja 10-minutowego idle runtime pozostaje testem manualnym na docelowym komputerze.

## Repository map

- `GameShell.Core`: modele, kontrakty i orkiestracja trybow/sesji.
- `GameShell.Windows`: integracje Win32, DisplayConfig, monitoring procesow i recovery launchera.
- `GameShell.Desktop`: host WPF, tray, hotkeye, SDL3 i bridge WebView2.
- `GameShell.Ui`: zrodla React/TypeScript, testy UI i bundlowane assety kopiowane do desktopu.
- `tests/GameShell.Core.Tests`: testy xUnit dla workflow, sesji, display i recovery.
- `docs`: stan projektu i dokumentacja UI.
- `docs/repository-hash-map.sha256`: mapa SHA-256 plikow repozytorium z pominieciem `.git`, katalogow `bin`, `obj`, `node_modules`, `dist` oraz samego manifestu hashy.

Manifest hashy jest regenerowany po zmianach z repo root. Hash manifestu nie moze zawierac samego siebie, bo tworzyloby to cykliczna wartosc.
