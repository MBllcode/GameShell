# UI Guidelines (Current)

To jest aktywny standard UI dla GameShell. Nizsze, historyczne notatki zostaly usuniete.

## Zasady glowne

- Interfejs ma pozostac ciemny, techniczny i oszczedny.
- Nie zmieniamy logiki biznesowej podczas prac stricte wizualnych.
- Potwierdzenia wymagajace decyzji uzytkownika realizujemy modalami aplikacji, nie `window.confirm`.

## Tokens i styl

- Jeden zestaw tokenow kolorow i spacingu w `styles.css`.
- Karty i kontrolki z mala geometria (techniczne rogi, bez duzych zaokraglen).
- Ostrzezenie (zolty) tylko semantycznie, nie jako glowny akcent.

## Komponenty bazowe

- Przyciski: `primary-button`, `secondary-button`, `danger-button`.
- Pola: `text-input`, `hotkey-input`.
- Dialogi: `dialog-backdrop`, `dialog-panel`, `dialog-actions`.
- Badge i statusy: ten sam system tokenow, bez losowych wariantow.

## Sidebar i dashboard

- Sidebar pokazuje aktualny tryb i skrocone fakty konfiguracji.
- Work Mode jest prezentowany w obszarze sidebar/profilu, nie jako glowna karta quick-switch.
- Dashboard ma byc zwarty i czytelny; unikamy pustych, wysokich kart bez informacji.

## Krok Ekrany

- Walidacja lokalna i rozdzielona:
  - `Podaj nazwe profilu.` pod polem nazwy,
  - `Wybierz co najmniej jeden ekran.` przy liscie ekranow.
- Komunikaty walidacyjne sa czerwone, dyskretne i nie udaja helpera.
- Komunikaty pokazuja sie po probie zapisu/nawigacji lub blur, i znikaja od razu po korekcie.
- Ograniczenie do 2 ekranow pozostaje osobnym inline warningiem `LIMIT`.

## Krok Skroty profilu

- Walidacja duplikatu hotkey ma byc lokalna, czerwona i osadzona bezposrednio przy polu hotkey.
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
- `dotnet build GameShell.sln`
- (zalecane) `dotnet test GameShell.sln`
