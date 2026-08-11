# UI Guidelines (Current)

To jest aktywny standard UI dla GameShell. Nizsze, historyczne notatki zostaly usuniete.

## Zasady glowne

- Interfejs ma pozostac ciemny, techniczny i oszczedny.
- Nie zmieniamy logiki biznesowej podczas prac stricte wizualnych.
- Potwierdzenia wymagajace decyzji uzytkownika realizujemy modalami aplikacji, nie `window.confirm`.
- UI pokazuje tylko stabilne funkcje GameShell: profile ekranów, lifecycle sesji, launcher/recovery, zachowanie pulpitu i skróty kontrolera. Nie pokazuje opcji korekcji fullscreen, borderless ani rozdzielczości gry.

## Struktura frontendu

- `App.tsx` odpowiada za wysokopoziomowe składanie stanu, feature coordinatorów i `AppView`; nie zawiera stron ani lokalnych efektów workflow.
- `features/dashboard` zawiera dashboard, jego model, akcje oraz rozpoczęcie/edycję flow profilu.
- `features/profiles` zawiera edycję profili, mutacje profilu i konfigurację ekranów.
- `features/settings` zawiera stan formularza, hydratację, payload i persystencję ustawień.
- `features/setup` zawiera flow kreatora, nawigację, zapis oraz model strony setupu.
- Nie dodajemy barrel files `index.ts`, jeśli nie są potrzebne do konkretnego importu.

## Encoding

- Wszystkie frontendowe pliki źródłowe `.ts`, `.tsx`, `.css`, `.html` i `.json` zapisujemy jako UTF-8.
- `src/index.html` ma pojedynczy `<meta charset="UTF-8" />` wysoko w sekcji `head`.
- Nie naprawiamy tekstu w runtime przez dekodowanie bajtów, `TextDecoder`, `decodeURIComponent`, `escape` ani konwersje ANSI.
- `npm run test:encoding` skanuje frontendowe źródła i pilnuje kluczowych polskich napisów oraz braku mojibake.

## Tokens i styl

- Jeden zestaw tokenow kolorow i spacingu w `styles.css`.
- Aktywny theme to GameShell Green v2: `#0D1110` dla tla, `#151A17` dla surfaces, `#1B211D` dla surfaces elevated, `#1E5128` dla aktywnych stanow, `#4E9F3D` jako glowny akcent i `#7AC943` jako bright accent.
- Tekst Green v2: `#F2F5F2`, `#B7C2B9`, `#7E8A80`; obramowania: `#303A32` i `#465248`.
- Zielony akcent dotyczy stanow aktywnych, zaznaczen, CTA, focusu, chevronow oraz brandingu GameShell. Tla, sidebar, panele i karty pozostaja neutralne.
- Kolory Playnite (`#FF8A00`), Steam (`#66C0F4`) i Xbox (`#107C10`) pozostaja brandingiem launcherow i nie sa recolorowane.
- Karty i kontrolki z mala geometria (techniczne rogi, bez duzych zaokraglen).
- Ostrzezenie (zolty) tylko semantycznie, nie jako glowny akcent.
- Motion korzysta z tokenow `--motion-fast: 120ms`, `--motion-normal: 180ms`, `--motion-slow: 220ms` oraz easingow `--ease-standard` i `--ease-out`.
- Motion animuje tylko kolory, tlo, obramowania, opacity, box-shadow i ograniczone transformacje wejscia; nie zmienia geometrii, spacingu ani zawartosci JSX.
- `prefers-reduced-motion: reduce` skraca transitions i usuwa ruch directional/page entrance.

## Komponenty bazowe

- Przyciski: `primary-button`, `secondary-button`, `danger-button`.
- Pola: `text-input`, `hotkey-input`.
- Dialogi: `dialog-backdrop`, `dialog-panel`, `dialog-actions`.
- Badge i statusy: ten sam system tokenow, bez losowych wariantow.

## Sidebar i dashboard

- Sidebar pokazuje aktualny tryb i skrocone fakty konfiguracji.
- W module `STAN` sidebar pokazuje drugorzedny status kontrolera z realtime `controllerStatus`; przy braku urzadzenia wyswietla `Brak kontrolera`, bez badge `ROZLACZONY`.
- Work Mode jest prezentowany w obszarze sidebar/profilu, nie jako glowna karta quick-switch.
- Przy aktywnej sesji konsolowej sidebar pokazuje osobny, zwiezly status lifecycle sesji obok informacji o trybie; status nie wymaga osobnego pollingu UI. Jest to status nadzoru GameShell, a nie zastapienie lifecycle launchera.
- Dashboard ma byc zwarty i czytelny; unikamy pustych, wysokich kart bez informacji.
- Przy pustej liscie profili boxy `Profile gamingowe` i `USTAWIENIA PROFILU` pozostaja widoczne. Profilowe akcje i ustawienia sa wtedy wyszarzone oraz nieaktywne; `+ Dodaj profil` i ustawienia globalne pozostaja aktywne.
- Globalne linki sidebaru (`Zachowanie Work Mode`, `Skróty Work Mode`, `Uruchamianie aplikacji`) wypelniaja cala dostepna szerokosc wewnetrznego kontenera, maja stabilny padding i nie przesuwaja tekstu podczas hoveru.
- Hover sidebaru animuje pelne tlo rowa, separator, tekst, dekoracyjny left accent i chevron. Chevron pozostaje pionowo wycentrowany i moze przesunac sie tylko poziomo o 2 px.
- Chevrony kart skrotow zachowuja stale centrowanie pionowe; ich hover cue jest wylacznie poziomy, maksymalnie 3 px.

## Krok Ekrany

- Nazwy profili gamingowych sa automatycznie zapisywane wielkimi literami; wpisanie malych liter jest natychmiast normalizowane w polu.
- Walidacja lokalna i rozdzielona:
  - `Podaj nazwe profilu.` pod polem nazwy,
  - `Wybierz co najmniej jeden ekran.` przy liscie ekranow.
- Komunikaty walidacyjne sa czerwone, dyskretne i nie udaja helpera.
- Komunikaty pokazuja sie po probie zapisu/nawigacji lub blur, i znikaja od razu po korekcie.
- Profil nie ma sztucznego limitu liczby ekranow; lista wykrytych ekranow pozostaje dostepna do dalszego dodawania.
- Akcje `Użyj bieżącego układu` i `Odśwież listę` sa wyrównane do prawej strony toolbara, także w responsywnym widoku mobilnym.
- Canvas jest technicznym obszarem roboczym z uniform scalingiem, paddingiem i obsluga ujemnych wspolrzednych. Skala, offset viewportu i padding nie sa zapisywane do topology.
- Drag przelicza ruch z pikseli ekranu na wspolrzedne topology przy uzyciu skali z poczatku gestu. Pozycje sa zapisywane jako rzeczywiste `PositionX`/`PositionY`.
- Przy przeciaganiu dziala snap krawedziowy oraz ograniczony alignment snap. Resolver kolizji nie pozwala zakonczyc drag z dodatnim overlapem; dotykajace krawedzie i narożniki sa poprawne.
- Primary moze byc wizualnie ustawiony w dowolnym miejscu. Dopiero snapshot runtime jest normalizowany do primary `0,0`, bez przesuwania draftu w edytorze.
- Przed wywolaniem DisplayConfig topology runtime sprawdza jeden primary, rozmiary, overlap i unikalnosc fizycznych targetow. Bledna geometria zatrzymuje apply z opisem wewnetrznego bledu.
- Diagnostyke apply czytamy z logu `[DISPLAY TOPOLOGY APPLY]`; wspolrzedne w tym logu sa wspolrzednymi przekazanymi do Windows, nie pikselami canvasa.
- Pole `Odświeżanie` zachowuje szerokosc i geometrie pozostalych pol, ale uzywa `appearance: none`, prawego paddingu `38px` oraz wlasnego subtelnego chevrona ustawionego `13px` od prawej ramki. Chevron ma `pointer-events: none`, a tekst opcji nie nachodzi na niego.

## Krok Skroty profilu

- Walidacja duplikatu hotkey ma byc lokalna, czerwona i osadzona bezposrednio przy polu hotkey.
- Gdy skrot klawiaturowy jest zapisany, przycisk `Usun` znajduje sie po prawej stronie inputa i jest kompaktowy, tak jak akcja usuwania skrotu kontrolera.
- Podczas `NASLUCHIWANIE` pole zachowuje stan bledu (`aria-invalid`) i nie moze byc zagluszone przez globalny hotkey hosta.
- Nowy profil nie dziedziczy hotkeya po poprzednio wybranym profilu.

## Zachowanie aplikacji

- `Startuj z Windows` i `Startuj zminimalizowany do tray` sa ustawieniami runtime, nie tylko dekoracja UI.
- `Startuj zminimalizowany do tray` moze byc wlaczone tylko gdy autostart jest wlaczony.

## Responsywnosc

- UI musi dzialac czytelnie dla 1280x720 i 1600x900.
- Akcje i stopki nie moga wychodzic poza kontener.
- W mniejszych szerokosciach panele przechodza do jednej kolumny.

## Dostepnosc

- Widoczny focus keyboard.
- Stany bledow nie moga byc przekazywane wyłącznie kolorem.
- Dla pol walidowanych stosujemy `aria-invalid` i `aria-describedby`.

## Checklista przed commit

- `npm run build` w `src/GameShell.Ui`
- `npm run test:encoding` w `src/GameShell.Ui`
- `dotnet build GameShell.sln`
- (zalecane) `dotnet test GameShell.sln`

