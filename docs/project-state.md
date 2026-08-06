# GameShell - aktualny stan projektu

Dokument dla kolejnych sesji Codex. Przed kolejnymi zmianami najpierw przeczytaj ten plik, potem sprawdź aktualny kod.

## Handoff 2026-08-06 (najwazniejsze)

- Profil jest wybierany z `Szybkie przelaczenie`; kroki profilowe edytuja zawsze ten zaznaczony profil.
- Krok `Ekrany` nie zawiera listy profili i nie ma recznego wyboru monitora glownego.
- Przy edycji ukladu ekranow profil musi aktualizowac sie po `Zapisz zmiany`.
- Krytyczny fix: zapis payloadu ma byc budowany z najnowszego draftu ekranu dla profilu (bez race condition React state). W `App.tsx` sluzy do tego override/ref dla payloadu i commit draftu przed `saveSettings`.
- Modal nadpisania nazwy profilu ma pojawiac sie tylko gdy nazwa koliduje z innym profilem; zwykla edycja aktualnie zaznaczonego profilu nie powinna go pokazywac.
- W first-run sa kroki globalne (`Work Mode`, `Zachowanie aplikacji`).
- W `Dodaj profil` po pierwszej konfiguracji sa tylko kroki profilowe: `Ekrany`, `Launcher`, `Zachowanie profilu`, `Skroty profilu`, `Zakonczenie`.

## Cel produktu

GameShell to lokalna aplikacja Windows do przełączania komputera między:

- `Console Mode`,
- opcjonalnym `PC Gaming`,
- `Work Mode`.

Po Stage 3 GameShell ma jeden mechanizm ekranów: natywne Windows DisplayConfig API. DisplayFusion i `DisplaySwitch.exe` nie są już elementem workflow aplikacji.

## Aktualny workflow użytkownika

Na pierwszym uruchomieniu `GameShell.exe` pokazuje kreator. Po zapisaniu konfiguracji ten sam plik otwiera panel główny.

Kreator:

1. `Start`
2. `Ekrany`
3. `Launcher`
4. `Tryb Console`
5. `Tryb Work`
6. `Kontroler i skróty`
7. `Test`
8. `Zakończenie`

Panel główny:

- pokazuje wybrany ekran Console Mode, zapisany układ PC Gaming i launcher,
- uruchamia `Console Mode`,
- uruchamia `PC Gaming`, jeśli zapisano układ PC Gaming,
- uruchamia `Work Mode`,
- pozwala edytować konkretne sekcje i przechodzić dalej między sekcjami bez wracania wyłącznie do panelu,
- pokazuje sekcje pod nazwami: `Ekrany`, `Launcher gier`, `Zachowanie trybów gamingowych`, `Zachowanie Work Mode`, `Kontroler i skróty`, `Uruchamianie aplikacji`.
- sidebar panelu głównego pokazuje obecny tryb i najważniejsze fakty konfiguracji; panel główny nie pokazuje górnej pigułki statusu, żeby nie dublować informacji o trybie.
- główny obszar panelu jest rozdzielony na trzy kafle trybów `Szybkie przełączenie` ułożone obok siebie oraz kafelkową siatkę `Konfiguracja`; tryby pokazują jawny stan `AKTYWNY`, `GOTOWY` albo `NIESKONFIGUROWANY` oraz osobny tekst akcji.
- po komendach z tray i globalnych hotkeyów host WPF wysyła do WebView zdarzenie `statusChanged`, a React pobiera świeży `getStatus`, żeby aktywne karty trybów aktualizowały się bez klikania w aplikacji.

## Ekrany

GameShell korzysta z:

- `GetDisplayConfigBufferSizes`,
- `QueryDisplayConfig`,
- `DisplayConfigGetDeviceInfo`,
- `SetDisplayConfig`.

`WindowsDisplayConfigurationService` odczytuje aktywne ścieżki DisplayConfig, mapuje je na neutralne modele domenowe i stosuje układy przez `SetDisplayConfig`.

Lista wyboru ekranów nie jest ograniczona do aktualnie aktywnych monitorów. `GetAvailableDisplaysAsync()` używa topologii `QDC_ALL_PATHS`, deduplikuje ścieżki po monitorze i pokazuje również dostępne, ale wyłączone w ustawieniach Windows ekrany, np. TV wyłączony w Display Settings.

Modele domenowe:

- `DisplayIdentity` - trwała tożsamość monitora oparta głównie na `MonitorDevicePath`, z EDID i connector instance jako fallbackiem,
- `DisplayLayoutSnapshot` - neutralny snapshot aktywnego układu,
- `DisplayPathSnapshot` - pozycja, rozdzielczość, primary, orientation, refresh rate numerator/denominator i bieżące adapter/source/target,
- `DisplayOperationResult` - kontrolowany wynik walidacji i zastosowania układu.

Rekonstrukcja konfiguracji dla `SetDisplayConfig`:

- przed zastosowaniem GameShell ponownie odczytuje aktualne ścieżki DisplayConfig,
- do apply łączy topologię `QDC_DATABASE_CURRENT` z `QDC_ALL_PATHS`, żeby móc ponownie aktywować monitory, które są aktualnie wyłączone,
- dopasowuje zapisane monitory przez `DisplayIdentityMatcher`,
- używa bieżących adapter/source/target z aktualnego odczytu,
- buduje dostarczoną konfigurację z aktywnymi ścieżkami wymaganymi przez snapshot oraz przekazuje zarówno `source mode`, jak i `target mode`,
- stosuje `SDC_USE_SUPPLIED_DISPLAY_CONFIG` i `SDC_ALLOW_CHANGES`, żeby sterownik mógł zaakceptować pełną topologię i dopasować brakujące szczegóły timingów,
- najpierw uruchamia `SDC_VALIDATE`,
- dopiero po sukcesie uruchamia `SDC_APPLY | SDC_SAVE_TO_DATABASE`.

Jeżeli wymagany monitor nie może zostać jednoznacznie dopasowany, układ nie jest stosowany częściowo.

Dla nieaktywnych monitorów rozdzielczość na liście dostępnych ekranów jest preferencyjnie brana z `DISPLAYCONFIG_TARGET_PREFERRED_MODE`, a potem z `target mode`, a nie z przypisanego source desktopu. Zapobiega to pokazywaniu TV 4K jako rozdzielczości aktualnie aktywnego monitora, np. 3440 × 1440.

Console Mode nie traktuje odświeżania z diagnostycznej listy dostępnych ekranów jako wymuszonego profilu. Przy aktywacji pojedynczego ekranu snapshot Console przekazuje `0/0` jako refresh rate, a dobór natywnej ścieżki DisplayConfig preferuje wyższe odświeżanie raportowane dla tego samego monitora. Rekonstrukcja nadal przekazuje kompletny `target mode`, bo Windows odrzuca pusty target timing kodem 87.

Po udanym zastosowaniu Console Mode GameShell wykonuje dodatkową, wąsko ograniczoną korektę odświeżania tylko dla aktualnie aktywnego pojedynczego ekranu Console. Używa `EnumDisplaySettings` dla bieżącego `\\.\DISPLAYx`, wybiera najwyższe Hz dla aktualnej rozdzielczości i stosuje je przez `ChangeDisplaySettingsEx`. Ta korekta nie działa podczas wykrywania ekranów, nie zapisuje listy trybów do ustawień i nie dotyka Work Mode ani PC Gaming. Błąd tej korekty jest logowany jako ostrzeżenie, żeby nie psuć działającego przejścia do Console Mode.

## Tryby ekranów

`ConsoleDisplayLayoutBuilder` buduje układ Console Mode:

- aktywny jest tylko wybrany ekran Console Mode,
- pozycja `0,0`,
- ekran jest primary,
- zachowana jest aktualna rozdzielczość, orientacja i częstotliwość odświeżania.

`PC Gaming`:

- użytkownik może zapisać aktualny układ jako `DisplaySettings.PcGamingLayout`,
- użytkownik może zapisać PC Gaming jako układ złożony wyłącznie z aktualnego monitora głównego,
- jeśli nie zapisano układu, PC Gaming fallbackuje do Console Mode.

`Work Mode`:

- nie ma ręcznego profilu Work,
- `DisplayTransitionService` zapisuje `WorkReturnLayout` przed wejściem do sesji gamingowej,
- Work Mode przywraca ten snapshot także po restarcie aplikacji.
- Work Mode ma dedykowaną procedurę restore: próbuje `WorkReturnLayout`, a jeśli jest stary/błędny, próbuje `PreviousDisplayLayout`; nie używa generycznego rollbacku, który mógł wracać z powrotem do Console.
- weryfikacja powrotu sprawdza aktywne monitory, pozycje i rozdzielczości; nie rollbackuje przez samą różnicę refresh rate albo primary flag znormalizowaną przez Windows.
- Work Mode nie używa już końcowego, blokującego `CompletionDelaySeconds` po wykonaniu skryptu, więc UI kończy operację zaraz po przywróceniu ekranów i workflow pulpitu. Starsze ustawienia z tym polem nadal się wczytują, ale timer nie steruje już czasem przełączenia.
- Work Mode nie steruje obecnie dźwiękiem; audio jest zostawione Windowsowi.

## Audio

Wybór i automatyczne przełączanie audio w Console Mode są tymczasowo usunięte z UI i workflow. `AudioSettings` pozostaje w modelu ustawień dla kompatybilności starszych plików `settings.json`, ale aktualny payload UI zapisuje:

- `Audio.SwitchAudioInConsoleMode = false`,
- `Audio.RestoreAudioInWorkMode = false`,
- `Audio.ConsoleAudioDeviceId = null`,
- `Audio.ConsoleAudioDeviceName = null`.

Jeżeli użytkownik ręcznie zmieni domyślne urządzenie audio w Windows po wejściu w Console Mode, Windows może zapamiętać ten wybór po swojej stronie. GameShell obecnie nie próbuje nim sterować.

## Runtime State

`state.json` zawiera:

- `CurrentMode`,
- `WorkReturnLayout`,
- `PreviousDisplayLayout`,
- `PreviousAudioDeviceId` - starsze pole runtime, obecnie nieużywane przez workflow,
- ostatni żądany tryb i wynik operacji.

`WorkReturnLayout` nie jest nadpisywany podczas przejść między Console i PC Gaming. `PreviousDisplayLayout` służy do automatycznego rollbacku i opcji tray.

## Rollback i testy ekranów

Normalne pełne workflow nie pokazują potwierdzenia ekranów.

Testy konfiguracji ekranów są dostępne w kroku `Ekrany`:

- `Testuj wybrany ekran`,
- `Testuj zapisany układ`.

Po teście UI pokazuje mały panel z licznikiem 15 sekund oraz przyciskami:

- `Zachowaj`,
- `Przywróć`.

Brak reakcji przywraca poprzedni układ. Rozpoczęcie kolejnego testu również przywraca poprzedni pending layout.

## Hotkeye i komendy

GameShell obsługuje globalne hotkeye dla pełnych trybów:

- Console Mode,
- PC Gaming,
- Work Mode.

Hotkey przejścia do `Work Mode` jest globalny (nie per profil). UI konfiguruje go w sekcji `Zachowanie Work Mode` pod checkboxami.

Obsługiwane argumenty CLI:

```powershell
GameShell.exe --gaming
GameShell.exe --pcgaming
GameShell.exe --work
```

Single-instance forwarding nadal działa: druga instancja przekazuje komendę do działającej instancji.

## Architektura

```text
src/
  GameShell.Core/
  GameShell.Desktop/
  GameShell.Ui/
  GameShell.Windows/
scripts/
build.ps1
package.ps1
README.md
docs/project-state.md
```

Główne technologie:

- .NET 9,
- WPF,
- WebView2,
- React,
- TypeScript,
- Vite,
- JSON settings,
- Microsoft.Extensions.DependencyInjection,
- Microsoft.Extensions.Logging.

## Core

`GameShell.Core`:

- modele ustawień w `Models/AppSettings.cs`,
- `DisplaySettings.ConsoleDisplay`,
- `DisplaySettings.PcGamingLayout`,
- `RuntimeState`,
- `GameShellMode`,
- `ControllerSettings` i `ControllerButton` są przygotowane pod przyszłą natywną obsługę kontrolerów przez SDL3,
- `IDisplayQueryService`,
- `IDisplayLayoutApplicationService`,
- `IDisplayTransitionService`,
- `ConsoleDisplayLayoutBuilder`,
- `DisplayTransitionService`,
- `DisplayLayoutVerifier`,
- `DisplayIdentityMatcher`,
- `SettingsService`,
- `RuntimeStateService`,
- `ModeOrchestrator`.

`ModeOrchestrator` uruchamia natywny workflow Windows dopiero po udanym przejściu ekranów. Jeśli przełączenie ekranów się nie powiedzie, akcje ikon/taskbara/launchera nie są wykonywane. Dla Work Mode błąd dodatkowego kroku po udanym przywróceniu ekranów jest traktowany jako ostrzeżenie, a nie jako błąd całego trybu.

## Windows

`GameShell.Windows`:

- `NativeModeWorkflowService` wykonuje natywnie workflow Console/PC Gaming/Work: ikony pulpitu, taskbary, zamykanie i minimalizowanie okien, launcher, focus launchera, pozycję kursora oraz tymczasowe ukrywanie kursora przez przezroczyste kursory systemowe,
- `WindowsAudioDeviceService` pozostaje w projekcie jako infrastruktura CoreAudio, ale nie jest obecnie używany przez Console Mode ani Work Mode,
- Work Mode próbuje pokazać ikony pulpitu od razu; opóźnienie `DesktopIconsDelaySeconds` jest tylko krótkim fallbackiem retry, limitowanym do maksymalnie 5 sekund, żeby nie powodować wolnego powrotu pulpitu,
- `WindowsDisplayConfigurationService` odczytuje i stosuje układy ekranów przez DisplayConfig API,
- `DependencyDetectionService` wykrywa tylko launchery opcjonalne, obecnie Playnite i Steam.

## Desktop Host

`GameShell.Desktop`:

- WPF hostuje WebView2,
- DI i logging są podpięte w `App.xaml.cs`,
- `WebViewBridge` ma whitelistę komend,
- minimalizacja okna zostawia GameShell na taskbarze,
- zamknięcie okna chowa GameShell do traya, jeśli `Ui.CloseToTray` jest włączone,
- tray menu ma:
  - Open GameShell,
  - Enter Console Mode,
  - Enter PC Gaming,
  - Enter Work Mode,
  - Przywróć poprzedni układ ekranów,
  - Exit,
- globalne hotkeye są rejestrowane przez `RegisterHotKey`,
- po zapisie ustawień hotkeye są rejestrowane ponownie.

## UI

`GameShell.Ui`:

- React + TypeScript + Vite,
- UI ma ciemny, techniczny styl terminalowy powiązany z ikoną GameShell: paleta oparta o `#191A19`, `#141615`, `#1C1F1D`, `#1E5128`, `#4E9F3D` i `#62B44F`, cienkie obramowania, prostokątne kontrolki z promieniem do 2 px, subtelna siatka w tle i brak żółtego/cyanowego języka wizualnego poza ostrzeżeniami,
- jeden prosty krok `Ekrany`,
- lista wykrytych ekranów pokazuje nazwę, rozdzielczość, refresh rate, primary, pozycję, typ połączenia i techniczny identyfikator w szczegółach,
- nieaktywne, ale dostępne ekrany są widoczne i oznaczone jako nieaktywne,
- krok `Ekrany` nie pokazuje już osobnej listy zapisanych profili; edytuje profil zaznaczony na panelu głównym,
- krok `Ekrany` nie wymaga ręcznego wyboru monitora głównego profilu,
- profil może zostać zapisany nawet przy takiej samej kombinacji ekranów jak inny profil (różnicowanie profili odbywa się też przez zachowania/checkboxy i launcher),
- próba nadpisania profilu po nazwie pokazuje modal potwierdzenia w stylu aplikacji,
- podczas pierwszej konfiguracji użytkownik klika `Dalej` w kroku `Ekrany`, a profil jest przygotowywany automatycznie i kreator przechodzi przez pełny flow,
- po zakończeniu pierwszej konfiguracji, opcja `Dodaj profil` z panelu głównego otwiera flow profilowy bez kroków globalnych (`Start`, `Zachowanie Work Mode`, `Zachowanie aplikacji`),
- flow dodawania kolejnego profilu przechodzi przez wszystkie kroki profilowe (`Ekrany`, `Launcher`, `Zachowanie profilu`, `Skróty profilu`, `Zakończenie`) i zapisuje dopiero na końcu,
- podczas tworzenia kolejnego profilu użytkownik może na każdym kroku wrócić do panelu głównego i porzucić aktualny draft,
- ustawienia globalne aplikacji (`Startuj z Windows`, `Startuj zminimalizowany`, skróty systemowe) są w osobnym kroku `Zachowanie aplikacji`, a krok `Zakończenie` zawiera tylko podsumowanie profilu,
- `Startuj zminimalizowany do tray` jest zależne od `Startuj z Windows` i nie może pozostać aktywne, gdy autostart jest wyłączony,
- sekcja `Console Mode` pozwala wybrać ekran Console i przetestować go bez zmieniania audio; wybrany ekran ma zielone obramowanie i marker `CONSOLE`,
- sekcja `PC Gaming` jest kompaktowym profilem grania przy biurku: pozwala zapisać obecny układ, zapisać tylko monitor główny, przetestować profil i go usunąć; status profilu jest pokazany jako `ZAPISANY` albo `BRAK PROFILU`,
- krok `Launcher` pokazuje status wykrycia Playnite, Steam i Xbox App oraz pozwala otworzyć oficjalną stronę pobierania wybranego launchera, jeśli nie jest zainstalowany,
- krok `Test` pokazuje wyłącznie test pełnych trybów,
- kreator ma stały footer z przyciskiem `Wróć` po lewej, komunikatem pośrodku i akcjami kontynuacji/zapisu po prawej,
- potwierdzenia wymagające decyzji użytkownika używają ustandaryzowanych komponentów modalnych (`dialog-backdrop`, `dialog-panel`, `dialog-actions`); nie używamy `window.confirm`,
- UI blokuje przewijanie okna aplikacji; kroki są projektowane tak, aby mieściły się w jednym widoku,
- okno WPF startuje domyślnie w `1360 x 860`, ma minimum `1040 x 720`, zapamiętuje ostatni rozmiar i położenie użytkownika, a zbyt duży lub pozaekranowy zapis jest przy starcie przycinany/centrowany do aktualnego pulpitu,
- hotkeye klawiaturowe są rejestrowane przez pola przechwytujące kombinacje klawiszy.

## Testy

Solution zawiera projekt `tests/GameShell.Core.Tests`.

Aktualne testy obejmują:

- wczytywanie starszego `settings.json` z polami DisplayFusion i dawnym polem kontrolera,
- domyślne `ControllerSettings`,
- walidację czasu przytrzymania kontrolera,
- usuwanie duplikatów przycisków kontrolera,
- zachowanie hotkeyów klawiaturowych,
- mapowanie DisplayConfig,
- dopasowanie `DisplayIdentity`,
- budowanie układu tylko dla jednego ekranu Console,
- walidację przed apply,
- rollback po błędzie apply,
- PC Gaming fallback do Console,
- przejścia Work -> PC Gaming -> Console -> Work,
- brak blokowania Work Mode przez starsze `WorkMode.CompletionDelaySeconds`,
- ręczny rollback testu ekranów.

Testy automatyczne nie przełączają fizycznych monitorów.

## Narzędzia pakowania

Katalog `scripts/` nie zawiera już skryptów workflow aplikacji. Workflow ikon, taskbara, okien, launchera i kursora działa natywnie w C# przez `NativeModeWorkflowService`.

W `scripts/` pozostaje tylko narzędzie pakowania:

- `GenerateGameShellIcon.ps1` - używany przez `package.ps1` do przygotowania ikon z PNG.

## Artefakt końcowy

`package.ps1` buduje jeden plik:

```text
artifacts\installer\GameShell.exe
```

## Ważne decyzje projektowe

- Nie ma już wyboru strategii ekranów.
- Nie ma profilu Work w ustawieniach użytkownika.
- `WorkReturnLayout` jest dynamicznym punktem powrotu sesji gamingowej.
- `PreviousDisplayLayout` służy do rollbacku i opcji tray.
- Nie implementowano jeszcze SDL3.
- Nie ma telemetrii, chmury ani kont użytkowników.
- Playnite, Steam i Xbox App nie są pakowane w `GameShell.exe`; GameShell otwiera oficjalne strony pobierania wybranego launchera, żeby użytkownik instalował aktualną wersję z właściwego źródła.

## Jak testować obecny stan

Build:

```powershell
.\build.ps1
```

Package:

```powershell
.\package.ps1
```

Weryfikacja ręczna:

- pierwsze uruchomienie pokazuje kreator,
- krok `Ekrany` pokazuje aktywne ekrany,
- można wybrać ekran Console Mode,
- można zapisać obecny układ PC Gaming,
- test ekranów pokazuje 15-sekundowe potwierdzenie,
- brak potwierdzenia przywraca poprzedni układ,
- pełny Console Mode nie pokazuje potwierdzenia,
- Work Mode przywraca `WorkReturnLayout`,
- `GameShell.exe --gaming`, `--pcgaming`, `--work` działają,
- finalny plik istnieje jako `artifacts\installer\GameShell.exe`.

## Następny logiczny etap

1. Zweryfikować `SetDisplayConfig` na większej liczbie układów sprzętowych, zwłaszcza przy TV wyłączonym w ustawieniach Windows.
2. Jeżeli konkretny sterownik nadal odrzuca konfigurację kodem 31, dodać diagnostykę zapisywanych timingów i rozważyć wariant z niepodawaniem target mode dla ścieżek, dla których sterownik wymaga pełnej best-mode logic.
3. Dodać walidację konfliktów hotkeyów i czytelny status rejestracji.
4. W kolejnym osobnym etapie podłączyć SDL3 do przygotowanych modeli kontrolera.
