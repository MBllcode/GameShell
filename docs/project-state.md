# GameShell - Project State

Aktualne, robocze podsumowanie stanu projektu.

## Aktualny status runtime

- Naprawiono inicjalizację stanu po pierwszym uruchomieniu: dashboard nie pozostaje w nieustalonym stanie po skończeniu setupu.
- Zabezpieczono zapis ustawień przez atomic write + backup recovery; `settings.json` nie jest już modyfikowany bezpośrednio w miejscu.
- Monitor sesji Windows jest teraz bardziej ostrożny: filtruje procesy launchera/systemowe, czeka na stabilizację kandydata przed `GameActive`, i loguje realne przejścia `candidate observed -> stabilized -> exited`.
- Recovery Playnite nie jest zbyt agresywnie rozbudowywany: loguje stan aktywacji i działa tylko po potwierdzeniu prawdziwego cyklu `GameExited`/`ReturningToLauncher`, bez dodatkowych mechanizmów focus bez dowodu runtime.

## Aktualny model ekranów

- `KnownPhysicalDisplay` jest jedyną trwałą logical identity ekranu. `CanonicalName` jest ustawiana przy pierwszym świadomym zapisaniu i nie zmienia się przy kolejnych nazwach z Windows.
- Obserwacje Windows są chwilowe. Zmiana friendly name, adapter LUID, aktywności lub listy trybów nie tworzy drugiego ekranu.
- Jeden znany ekran może zachować wiele historycznych aliasów trasy, np. różne `AdapterLuid + TargetId` po restarcie sterownika lub replug HDMI. Resolver oraz reconciler katalogu działają wspólnie dla discovery, readiness i migracji.
- `BestKnownCapability` i `LastVerifiedActiveCapability` należą do `KnownPhysicalDisplay`, a nie do trasy ani profilu. Słabsza obserwacja nie obniża zapamiętanego maksimum.
- Profile przechowują referencję do znanego ekranu oraz własne ustawienia: enabled, primary, pozycję, rozdzielczość i `RefreshPolicy`/`FixedRefresh`.
- Ostatnia walidacja na realnym TV potwierdziła po replug: `KnownCount=3`, `ObservationCount=3`, `LogicalCount=3`, dopasowanie istniejącego S90F oraz `BestKnown=144` z akcją `Kept`. Profil CONSOLE osiągnął `Ready` i `LauncherActive` z AutoHighest 144 Hz.

## Capability i recovery HDMI

- `AutoHighest` po aktywacji wykonuje świeże active mode inventory i wybiera najwyższy aktualnie enumerowany tryb. `BestKnownCapability` jest wiedzą historyczną/diagnostyczną, nie wymaganym minimum.
- Gdy aktywny ekran wystawia tylko niższy tryb, profil może pozostać `Ready` i użyć bieżącego trybu; logowana jest informacja `[DISPLAY CAPABILITY DEGRADATION]` z akcją `UseCurrentAvailableMode`.
- `Fixed` pozostaje ścisłe: żądany tryb musi być obecny w świeżym inventory po aktywacji.
- Nie ma automatycznego monitor disable/enable, reenumeracji devnode, restartu GPU, poleceń DDC/CI, CEC ani sterowania TV w normalnym przejściu profilu. Eksperymentalne recovery HDMI nie jest częścią produkcyjnego flow.

## Ostatnia iteracja zmian

- Rozpoczeto etapowy refactor architektoniczny: audyt zapisano w `docs/architecture-audit.md`; wydzielono resolver profili gamingowych, koordynator readiness, router komend WebView, migrator starego JSON, parser hotkeyow oraz serwisy Windows integration/cursor. Zachowanie DisplayConfig/CCD pozostalo bez zmian.
- Uruchamianie launcherow ma wspolny adapter standard-user: `NativeModeWorkflowService` i `WindowsLauncherRecoveryStrategy` korzystaja z `ILauncherProcessService`, a token/process creation pozostaje w `GameShell.Windows`. Elevated GameShell nie przekazuje juz swojego tokena do Playnite; blad startu zwracany jest kontrolowanie bez silent elevated fallbacku.
- Po kolejnych slice'ach `ModeOrchestrator` ma 477 linii zamiast 808, `WebViewBridge` 915 zamiast 1154, `WindowsDisplayConfigurationService` 871 zamiast 1249, `App` 795 zamiast 864, a `SettingsService` 290 zamiast 333; aktualny zestaw testów .NET obejmuje `191/191` przypadków.
- `WindowsDisplayTopologyReader` odpowiada za odczyt/retry/mapowanie topologii DisplayConfig, a `WebViewPayloadReader` za stateless parsing payloadow WebView. Zadna produkcyjna klasa C# nie przekracza obecnie 1000 linii.
- `SingleInstanceCommandHost` przejal rejestracje named events, marshalowanie callbackow do WPF Dispatcher oraz cleanup z `App`; `App` nadal wybiera komendy i zachowuje polityke lifecycle.
- `WebViewDisplayCommandHandler` przejal inventory ekranow, capture layoutu, zapisy profili ekranow, display testy i walidacje wyboru ekranow z `WebViewBridge`; bridge pozostaje granica transportu WebView2.
- `GlobalHotkeyHost` przejal rejestracje Win32 hotkeyow, mapowanie skrotow profili, reload po zapisie, stan nasluchiwania i cleanup z `App`; `App` pozostaje kompozycja lifecycle i runnerem tray.
- `TrayHost` przejal lifetime `NotifyIcon`, budowanie menu, odswiezanie stanu i callbacki akcji z `App`; `App` pozostaje koordynatorem lifecycle oraz polityki shutdown.
- Wykonano cleanup statyczny: brak nieuzywanych `using`ow wedlug `IDE0005`; usunieto potwierdzony, nieosiagalny prywatny helper `SelectBestCandidate` z DisplayConfig. Publiczne entry pointy zachowano, gdy sa uzywane przez DI, bridge lub testy.
- Przejrzano sugestie `LibraryImport` dla P/Invoke. `DllImport` pozostaje celowo w aktualnej warstwie DisplayConfig/Win32: struktury maja wrazliwy layout ABI, fixed native buffers i tablice o rozmiarze runtime. Migracja nie jest bledem do naprawy; ewentualna migracja prostych wywolan wymaga osobnego testowanego slice'a.
- `ShortcutConflictNormalizer` przejal normalizacje i usuwanie konfliktow skrotow z `AppSettingsValidator`, a `ControllerShortcutMatcher` przejal czyste dopasowanie kombinacji i hold duration z pollingu SDL.
- Audio nie jest obecnie obslugiwane przez produkt i zostalo usuniete z kontraktow, persistence, DI, WebView oraz UI (`IAudioDeviceService`, `WindowsAudioDeviceService`, audio DTO/commands). Windows domyslnie zarzadza audio niezaleznie od GameShell; stare pola audio w JSON sa ignorowane przy odczycie.
- Po tych slice'ach `App` ma 459 linii, `TrayHost` 213, `WebViewBridge` 485, `WebViewDisplayCommandHandler` 449, `SdlControllerShortcutService` 655, a najwieksza produkcyjna klasa ma 871 linii. Aktualny zestaw testów .NET: `191/191`.
- `WindowsWindowManagementService` przejal operacje Win32 dla ikon pulpitu, taskbarow, okien Explorera, minimalizacji i foreground launchera. `NativeModeWorkflowService` zachowuje kolejnosc krokow, cancellation, warning aggregation i polityke procesow launchera; ma teraz 506 linii zamiast 808.
- Uzupełniono flow setupu i walidację kolejnych kroków: `WelcomeStep`, `DisplaysStep`, `LauncherStep`, `ProfileBehaviourStep`, `WorkBehaviourStep`, `AppBehaviourStep` oraz `ControllerStep` są spójne pod względem lokalnych błędów, deterministycznego zapisu draftu oraz bezpieczniejszej nawigacji po pierwszej konfiguracji.
- Draft konfiguracji ekranów jest teraz commitowany przed przejściem dalej i przed zapisaniem profilu, co zapobiega sytuacjom, w których UI zapisuje stale snapshot bez pełnej, znormalizowanej geometrii.
- Hotkeyi profili są normalizowane i weryfikowane lokalnie; podczas `NASLUCHIWANIE` aplikacja chwilowo wyrejestrowuje globalne skróty, aby poprawnie przechwycić i zwalidować kombinację bez konfliktów.
- Zajęta kombinacja klawiatury lub kontrolera pozostaje tymczasowo widoczna na czerwono wraz z komunikatem walidacyjnym przez `2,8 s`; następnie pole wraca do poprzednio zapisanego skrótu albo pozostaje puste. Błąd blokuje zapis oraz wyjście do dashboardu i może pojawić się ponownie przy kolejnej próbie.
- Końcowy przycisk flow dodawania profilu ma etykietę `Zapisz zmiany`; jego działanie zapisuje nowy profil i dopiero potem wraca do dashboardu.
- Readiness profilu rozdziela obecność fizycznego route od bieżącej capability handshake. Dopasowanie nie wymaga `FriendlyName`: preferowane są `AdapterLuid + TargetId`, stabilna ścieżka monitora oraz poprawne EDID/connector fields; nazwy pozostają diagnostyczne i nie ma aliasów Samsung/S90F.
- `BestKnownCapability` należy do globalnego `KnownPhysicalDisplay` i jest wyłącznie historyczną wiedzą diagnostyczną. Nie jest minimalnym kontraktem dla `AutoHighest`, a chwilowa obserwacja inactive nie obniża tej wartości.
- `AutoHighest` najpierw aktywuje target, potem odczytuje świeże active mode inventory i wybiera najwyższy aktualnie enumerowany tryb dla zapisanej rozdzielczości. Jeśli bieżące active capability jest niższe od historycznego maksimum, profil pozostaje `Ready`, a runtime zapisuje tylko `[DISPLAY CAPABILITY DEGRADATION]` z akcją `UseCurrentAvailableMode`.
- GameShell nie wykonuje automatycznej renegocjacji HDMI ani recovery devnode/GPU/TV. Jeśli Windows wystawi zredukowany zestaw trybów, aplikacja nie wymusza niedostępnego trybu; hardware/link renegotiation może wymagać działania poza GameShell.
- Diagnostyka readiness zapisuje `[DISPLAY PHYSICAL MATCH]` oraz `[DISPLAY CAPABILITY READINESS]` z nazwami, route, EDID validity, capability i powodem wyniku.
- Zachowanie startu i autostartu jest spójne w zwykłym starcie i scenariuszach fallbackowych: `Start with Windows` i `Start minimized to tray` są zastosowane w ścieżce runtime, a nie tylko w dekoracji UI.
- Aktualna dokumentacja UI jest utrzymywana w `docs/ui.md` jako aktywny standard zgodny z obecnym green-v2 theme, motion systemem i zasadami dla sidebaru, profili i walidacji.

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

- `Console Mode`: kompatybilnosciowa sciezka aktywujaca jeden wybrany ekran.
- `Gaming profiles`: stosuja konfigurowalne przez uzytkownika profile ekranow i zachowania, niezaleznie od tego, czy docelowym setupem jest PC, projektor, TV czy inna kombinacja ekranow.
- `Work Mode`: przywraca `WorkReturnLayout` zapisany przed sesja gamingowa.
- `Work Mode` ma opcjonalny, trwaly `DefaultDisplayLayout` konfigurowany w osobnym kroku `work-display`. Gdy go nie ma w starszych ustawieniach, runtime zachowuje fallback do `WorkReturnLayout`/`PreviousDisplayLayout`.

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
- `ConsoleSessionManager` uniewaznia `SessionGeneration` przy wyjsciu z sesji gamingowej. Opoznione callbacki monitora sa ignorowane, gdy stan nie jest juz `GameActive`/`ReturningToLauncher`, wiec nie moga przywrocic `LauncherActive` po Work Mode.
- Wejscie do Work Mode z dashboardu i z pada korzysta z tego samego `IModeCommandService`. Skrot SDL3 uruchamia komendę z watku pollingu, dlatego `WebViewBridge.NotifyStatusChanged` musi publikowac event przez WPF Dispatcher; bez tego backend zmienial stan, ale WebView2 nie odswiezal sidebara.
- Przy diagnostyce przeplywu uzywamy logow `[WORKMODE REQUEST]`, `[WORKMODE SESSION COMMIT]`, `[WORKMODE BACKEND COMMIT]`, `[STATUSCHANGED EMIT]`, `[STATUSCHANGED RECEIVED]`, `[GETSTATUS RESPONSE]`, `[UI STATUS RECEIVED]` i `[DASHBOARD RUNTIME STATE]`. Log aplikacji jest w `%LOCALAPPDATA%\GameShell\Logs\gameshell-yyyy-MM-dd.log`.
- `IGameSessionMonitor` jest osobnym kontraktem. Implementacja Windows sprawdza tylko jawnie skonfigurowane `Gaming.GameProcessNames`; pusta lista oznacza brak heurystycznego wykrywania gry.
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
- Na ostatnim kroku dodawania profilu główny przycisk ma napis `Zapisz zmiany`, a nie `Panel główny`; po zapisie następuje powrót do dashboardu.
- Nazwy profili gamingowych sa uppercase-only: UI konwertuje wpis podczas edycji, a `AppSettingsValidator` normalizuje takze starsze ustawienia i konfiguracje przy zapisie/odczycie.
- Krok `Ekrany`:
  - wymaga nazwy profilu,
  - wymaga wyboru co najmniej jednego ekranu,
  - pokazuje te bledy lokalnie i niezaleznie (osobne komunikaty),
  - nie ma sztucznego limitu liczby ekranow w profilu; walidowane sa tylko wymagania topology i poprawnosc danych.
- Canvas profilu ma teraz stała wysokosc `360px` na calej zakladce, z fitowaniem topology do srodka i wyrównanym panelem po prawej; layout nie skacze pomiedzy profilami.
- Panel profilu i przyciski akcji maja odswiezony, bardziej wylozany wyglad z subtelna ramka, chip-count i spójna hierarchia akcji, bez zmiany logiki workflow.
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
- Odrzucony duplikat nie nadpisuje draftu ani zapisanego skrótu. Przez `2,8 s` kandydat jest pokazany na czerwono wraz z błędem, po czym znika i wraca poprzednia wartość; dla pola bez wcześniejszego skrótu pozostaje pusto.
- Aktywny błąd skrótu blokuje przycisk `Zapisz zmiany`, zapis z modala niezapisanych zmian oraz próbę wyjścia do `Panel główny`.

## Zachowanie zapisu

- Brak `window.confirm` dla standardowego `Zapisz zmiany`.
- Modal nadpisania pojawia sie tylko przy rzeczywistym konflikcie nazwy z innym profilem.
- Launcher i ustawienia profilu nie sa nadpisywane starym snapshotem po edycji ekranow.
- Jawne `null` dla pustego `gamingLayout` i `activeGamingProfileName` jest zachowywane podczas zapisu, aby walidator nie odtwarzal profilu `Domyslny`.
- Nazewnictwo domenowe jest ujednolicone wokol `GamingProfile`, `GamingProfiles` i `GamingSettings`. Stare klucze `pcGaming*` sa przyjmowane tylko przez jednorazowa migracje wczytywania ustawien; nowe zapisy uzywaja nazw kanonicznych.
- Kontrakt nazewniczy jest celowo rozdzielony: `Work Mode` oraz konfigurowalne `Gaming profiles` to obszary produktu, a `Console Mode` pozostaje tylko kompatybilnosciowa sciezka techniczna. Friendly names ekranow sa diagnostyczne i nie tworza aliasow modeli.
- `SettingsService` zapisuje ustawienia atomowo: serializacja i walidacja odbywaja sie przed otwarciem pliku docelowego, nastepnie tworzony jest plik `.tmp`, flush do dysku i `File.Move` na miejsce `settings.json`.
- Backup `.bak` jest aktualizowany w bezpiecznej sekwencji; przy niepoprawnym albo uszkodzonym `settings.json` loader preferuje poprawny backup zamiast resetu do pierwszego uruchomienia.
- Pliki z uszkodzeniem sa zachowywane jako kopie `.corrupt-<timestamp>` z zachowaniem diagnostyki, a w przypadku braku poprawnego glownego pliku i backupu system tworzy domyslne ustawienia dopiero po potwierdzeniu, ze brak bezpiecznego odzyskania.
- Weryfikacja: `SettingsServiceTests` obejmuje migracje i zapis nazw kanonicznych; aktualnie `dotnet test GameShell.sln` przechodzi `191/191`, 0 failed.

## Desktop host i runtime

- Tray menu: Open, Gaming profiles, Work Mode, Exit.
- CLI: `--console`, `--profile`, `--work`; `--gaming` i `--pcgaming` pozostaja aliasami kompatybilnosciowymi.
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
- Ostatnia walidacja katalogu ekranów i readiness: `dotnet test GameShell.sln` -> `191/191` passed; build .NET oraz ręczny runtime na realnym TV również zostały zweryfikowane. Bundle UI jest kopiowany do WPF `wwwroot` przez `build.ps1`.

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

## Dirty state i ponowne ladowanie profilu

- Frontend porownuje semantyczny payload formularza z ostatnimi zapisanymi ustawieniami. Geometria okna nie jest traktowana jako zmiana profilu.
- Zapis bez realnych zmian nie wysyla `saveSettings` i pokazuje komunikat `No changes to save` albo `Brak zmian do zapisania`.
- `ConsoleSessionManager` przechowuje fingerprint aktywacji profilu. Ten sam profil bez zmian zwraca `NoChanges`, a po zapisanej zmianie jest bezpiecznie przeladowywany z ponownym uruchomieniem monitora sesji.
- Fingerprint pomija niestabilne metadane snapshotu layoutu (`Id`, `CapturedAt`) i obejmuje konfiguracje profilu, topology, launcher oraz zachowanie gamingowe.

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
