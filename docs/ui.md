za dużo kart i ramek,
za dużo zaokrągleń,
zbyt wiele kolorów akcentów naraz,
hierarchia informacji jest słaba,
zielony praktycznie nie buduje charakteru marki,
sidebar wygląda jak osobny, niedopracowany komponent,
aktywny element w sidebarze ma przypadkową żółtą linię i ciemnoszare tło,
dashboard ma dużo pustej przestrzeni, ale nie daje wrażenia premium,
napisy wielkimi literami w żółtym kolorze wyglądają jak domyślny „gaming UI”,
nie ma wizualnego połączenia z ikoną GameShell.

Najlepszy kierunek to nie dosłowny Matrix z „cyfrowym deszczem”, tylko industrialny, terminalowy interfejs:

czarne tło
grafitowe powierzchnie
ciemna zieleń
jasna zieleń tylko dla aktywnych elementów
kwadratowe karty
cienkie linie
wyraźna typografia
bardzo mało ozdobników

Kolorystycznie:

Tło aplikacji:       #080B09
Sidebar:             #050806
Powierzchnia:        #0D120F
Powierzchnia hover:  #111914
Obramowanie:         #26342A
Obramowanie aktywne: #3C6E49
Akcent główny:       #4F9B5F
Akcent mocny:        #69C779
Tekst główny:        #E2E8E3
Tekst drugorzędny:   #8C9A8F
Tekst wyciszony:     #5F6B62
Błąd:                #C65D5D
Ostrzeżenie:         #B9954A

Nie używałbym neonowego #00FF00, bo szybko wygląda tandetnie. Ciemniejsza, lekko wojskowa zieleń będzie pasować do ikony znacznie lepiej.

Prompt dla Codexa
Przeczytaj aktualny `docs/project-state.md`, sprawdź obecny kod UI oraz uruchom aplikację lub obejrzyj aktualne widoki przed wprowadzaniem zmian.

Masz dostęp do MCP i narzędzi UI. Użyj ich do przeanalizowania istniejących komponentów, stylów, layoutu oraz powtarzających się wzorców.

W tym zadaniu wykonaj pełny redesign wizualny GameShell.

Nie zmieniaj logiki biznesowej, bridge commands, workflow ekranów, przełączania trybów ani modeli ustawień, chyba że drobna zmiana struktury komponentu jest konieczna do poprawy UI.

Celem jest odejście od wyglądu generycznego dashboardu AI i stworzenie spójnego, rozpoznawalnego interfejsu GameShell.

==================================================
KIERUNEK WIZUALNY
==================================================

Interfejs ma być inspirowany:

- terminalami systemowymi,
- stylem Matrix, ale bez dosłownego deszczu znaków,
- retro-futurystycznym panelem sterowania,
- ikoną GameShell: zielony kontroler w muszli,
- narzędziami desktopowymi Windows,
- ciemnym, technicznym i uporządkowanym UI.

Nie twórz:

- neonowego cyberpunkowego interfejsu,
- zielonego tekstu na każdym elemencie,
- efektów glow na dużych powierzchniach,
- gradientów,
- półprzezroczystego glassmorphism,
- wielu kolorów akcentów,
- zaokrąglonych kart w stylu SaaS,
- ogromnych pustych paneli,
- dekoracyjnych danych terminalowych bez funkcji,
- przypadkowych napisów wielkimi literami.

UI ma być ciemny, czytelny, techniczny i charakterystyczny.

==================================================
PALETA
==================================================

Zdefiniuj jeden centralny zestaw tokenów CSS.

Użyj palety zbliżonej do:

```css
--bg-root: #080b09;
--bg-sidebar: #050806;
--bg-panel: #0d120f;
--bg-panel-hover: #111914;
--bg-panel-active: #121b15;

--border-subtle: #202a23;
--border-default: #2a372d;
--border-active: #3c6e49;

--accent: #4f9b5f;
--accent-strong: #69c779;
--accent-muted: #315f3b;

--text-primary: #e2e8e3;
--text-secondary: #96a198;
--text-muted: #637066;
--text-on-accent: #061008;

--danger: #c65d5d;
--warning: #b9954a;

Możesz lekko dostosować wartości po sprawdzeniu kontrastu, ale zachowaj:

prawie czarne tło,
grafitowo-zielone powierzchnie,
ciemną zieleń jako główny akcent,
jasną zieleń tylko dla aktywnych lub ważnych elementów,
brak żółtego jako głównego koloru.

Żółty może pozostać wyłącznie jako semantyczne ostrzeżenie.

==================================================
KSZTAŁTY I OBRAMOWANIA

Obecne elementy są zbyt zaokrąglone.

Zmień styl na bardziej kwadratowy:

główne karty: border-radius: 2px albo 0,
przyciski: maksymalnie 2px,
pola tekstowe i selecty: 2px,
status badge: maksymalnie 2px,
brak dużych zaokrągleń 8–16 px.

Używaj cienkich obramowań:

border: 1px solid var(--border-default);

Aktywne elementy:

border-color: var(--border-active);
box-shadow: inset 3px 0 0 var(--accent);

Nie używaj dużego zewnętrznego glow.

Opcjonalny bardzo subtelny efekt:

box-shadow: 0 0 12px rgba(79, 155, 95, 0.06);

tylko dla aktywnego elementu lub dialogu.

==================================================
SIDEBAR

Obecne podświetlenie sidebaru jest niespójne i wygląda przypadkowo.

Przebuduj sidebar.

Wymagania:

jednolite czarne tło,
brak osobnego szarego prostokąta obejmującego cały aktywny element,
brak żółtej linii,
brak żółtych numerów kroków,
aktywny krok ma:
ciemnozielone tło,
pionowy zielony pasek po lewej,
zielone obramowanie numeru,
jaśniejszy tytuł,
subtelnie zielony tekst opisu,
ukończony krok ma zieloną ikonę/check,
przyszły krok ma wyciszony szary kolor,
hover ma jedynie lekko rozjaśnić tło.

Przykładowy aktywny element:

▌ [02]  Ekrany
        TV i układ PC Gaming

Numer kroku powinien być małym kwadratowym elementem, a nie dużym ozdobnikiem.

Logo GameShell w sidebarze:

użyj aktualnej ikony,
nie umieszczaj jej w przypadkowym kole,
nazwa GameShell ma być mocna, ale nie przesadnie duża,
opis może być mały i wyciszony.

Sidebar powinien wyglądać jak nawigacja systemowego narzędzia, nie jak kreator instalatora z 2010 roku.

==================================================
TYPOGRAFIA

Obecna typografia ma zbyt dużo podobnych poziomów i przypadkowych uppercase.

Zachowaj font systemowy lub obecny font aplikacji, ale uporządkuj skalę:

Page title:       24–28 px / 700
Section title:    18–20 px / 650
Card title:       15–17 px / 650
Body:             14–15 px / 400
Secondary:        13–14 px / 400
Label:            12–13 px / 600
Micro/status:     11–12 px / 600

Nie pisz całych nagłówków uppercase poza małymi technicznymi etykietami, np.:

STATUS
EKRAN CONSOLE
AKTYWNY TRYB

Te etykiety powinny być niewielkie, z delikatnym letter-spacing.

Ogranicz liczbę tekstów pomocniczych. Jeśli tekst nie pomaga podjąć decyzji, usuń go.

==================================================
PANEL GŁÓWNY

Aktualny dashboard ma trzy duże karty, które wyglądają podobnie i zajmują dużo miejsca.

Przebuduj sekcję szybkiego przełączania jako techniczny panel trybów.

Każdy tryb powinien mieć:

ikonę lub prosty symbol,
nazwę,
krótkie jednozdaniowe wyjaśnienie,
najważniejszy ekran/układ,
jasny stan:
aktywny,
gotowy,
nieskonfigurowany,
jeden jednoznaczny przycisk.

Aktywny tryb:

zielony pasek po lewej lub u góry,
subtelne ciemnozielone tło,
etykieta AKTYWNY,
brak dużej żółtej ramki.

Nieaktywne tryby:

neutralne grafitowe karty,
zielony akcent dopiero na hover lub przycisku.

Karty nie powinny wyglądać jak trzy kolorowe kafelki SaaS.

Rozważ układ:

[ CONSOLE MODE      TV: S90F                    URUCHOM ]
[ PC GAMING         PL3480WQ · 3440×1440        URUCHOM ]
[ WORK MODE         Powrót do układu roboczego  AKTYWNY ]

Może to być lista pozioma lub zwarta siatka, ale ma być gęstsza i czytelniejsza niż obecne wysokie karty.

Sekcja konfiguracji powinna wyglądać jak lista modułów narzędzia:

Ekrany                  S90F / profil PC Gaming          >
Launcher                Playnite Fullscreen              >
Console Mode            Ikony, taskbar, okna             >
Work Mode               Przywracanie pulpitu             >
Skróty                   Klawiatura i kontroler           >
Uruchamianie             Tray i autostart                 >

Zamiast dużych pustych kart użyj zwartych wierszy lub mniejszych paneli.

==================================================
KROK EKRANY

Obecna sekcja ekranów ma zbyt dużo ramek, przycisków i informacji na jednym poziomie.

Przebuduj ją tak, aby najważniejsza decyzja była oczywista:

wybór ekranu Console Mode,
opcjonalny profil PC Gaming.

Lista ekranów:

każdy ekran jako kwadratowy, prosty panel,
nazwa jako główny tekst,
rozdzielczość i Hz w jednej linii,
pozycja i połączenie jako tekst drugorzędny,
ekran wybrany ma zielone obramowanie oraz mały znacznik CONSOLE,
nie używaj żółtego zaznaczenia,
przycisk powinien brzmieć:
Wybierz
po wyborze: Wybrany,
nie umieszczaj wielu różnych akcji wewnątrz każdej karty.

Przyciski:

Odśwież ekrany jako secondary,
Testuj wybrany ekran jako secondary z zielonym akcentem,
Zapisz zmiany jako primary.

Sekcja PC Gaming:

nie otaczaj jej grubą żółtą ramką,
status profilu pokaż jako mały badge:
ZAPISANY
BRAK PROFILU,
akcje uporządkuj według ważności,
Usuń profil ma być dyskretnym danger action,
opis profilu ma być zwarty.
==================================================
PRZYCISKI

Zdefiniuj trzy warianty:

Primary
ciemnozielone tło,
jasny tekst,
zielone obramowanie,
bez gradientu,
bez zaokrągleń,
hover lekko jaśniejszy.
Secondary
przezroczyste lub grafitowe tło,
neutralne obramowanie,
biały tekst,
hover z zielonym obramowaniem.
Danger
neutralne tło,
czerwony tekst lub obramowanie,
bez wypełnienia na stałe.

Nie używaj koloru niebieskiego dla przycisków testowych.

Wszystkie akcje powinny używać tej samej palety.

==================================================
STATUSY I BADGE

Zastąp obecne żółte statusy.

Przykłady:

AKTYWNY
GOTOWY
ZAPISANY
NIESKONFIGUROWANY
NIEAKTYWNY

Styl:

mały prostokąt,
1 px obramowania,
brak kapsułki/pill,
zielony dla poprawnych stanów,
szary dla neutralnych,
czerwony tylko dla błędu,
żółty tylko dla ostrzeżenia.
==================================================
TŁO I CHARAKTER MARKI

Dodaj bardzo subtelny, prawie niewidoczny motyw techniczny.

Może to być:

delikatna siatka 24 px,
pojedyncze linie przypominające PCB,
bardzo słaby pattern punktów,
cienkie terminalowe separatory.

Przykładowo:

background-image:
  linear-gradient(rgba(79,155,95,0.025) 1px, transparent 1px),
  linear-gradient(90deg, rgba(79,155,95,0.025) 1px, transparent 1px);
background-size: 24px 24px;

Pattern ma być prawie niewidoczny.

Nie dodawaj:

animowanego kodu Matrix,
ruchomych cyfr,
dużych dekoracyjnych grafik,
jaskrawego glow.
==================================================
LAYOUT

Zmniejsz wizualny chaos:

mniej ramek zagnieżdżonych jedna w drugiej,
mniej pustych przestrzeni,
bardziej konsekwentny spacing,
maksimum 2 poziomy paneli,
sekcje oddzielaj linią lub przestrzenią, a nie kolejną kartą.

Zaproponuj centralne tokeny spacingu:

--space-1: 4px;
--space-2: 8px;
--space-3: 12px;
--space-4: 16px;
--space-5: 24px;
--space-6: 32px;

Nie używaj losowych wartości odstępów w każdym komponencie.

==================================================
RESPONSYWNOŚĆ

UI działa w WPF WebView2 i musi dobrze wyglądać przy:

1280×720,
1600×900,
1920×1080,
skalowaniu Windows 100–150%.

Nie zakładaj jednego dużego monitora.

Unikaj sztywnych wysokości kart.

Sidebar może mieć stałą szerokość, ale główna treść ma reagować na szerokość okna.

Przy mniejszej szerokości:

karty trybów mogą przejść do jednej kolumny,
ekrany mogą przejść z 3 kolumn do 1–2,
przyciski nie mogą wychodzić poza panel.
==================================================
REFAKTORYZACJA

Nie dokładaj pojedynczych override’ów do obecnego CSS bez uporządkowania systemu.

Najpierw:

znajdź główne style globalne,
usuń nieużywane kolory i stare warianty,
zdefiniuj design tokens,
ujednolić:
Button,
Card/Panel,
SectionHeader,
StatusBadge,
SidebarItem,
FormField,
dopiero potem poprawiaj konkretne widoki.

Nie twórz dużej biblioteki komponentów dla kilku elementów, ale usuń duplikację stylów.

Nie zmieniaj architektury React bez potrzeby.

==================================================
WERYFIKACJA WIZUALNA

Po zmianach uruchom aplikację i sprawdź co najmniej:

kreator — krok Ekrany,
panel główny,
aktywny i nieaktywny krok sidebaru,
ekran z trzema wykrytymi monitorami,
zapisany i niezapisany profil PC Gaming,
aktywny Console Mode,
aktywny PC Gaming,
aktywny Work Mode,
hover, focus i disabled,
okno o mniejszej rozdzielczości.

Użyj MCP UI lub dostępnych narzędzi do sprawdzenia finalnego wyglądu.

Nie kończ zadania po samej zmianie CSS bez obejrzenia efektu.

==================================================
DOSTĘPNOŚĆ

Sprawdź:

kontrast tekstu,
widoczny focus klawiatury,
aktywny element nie może być oznaczony wyłącznie kolorem,
przyciski disabled muszą pozostać czytelne,
tekst drugorzędny nie może być zbyt ciemny,
badge nie może być jedynym źródłem informacji o stanie.
==================================================
BUILD

Po zmianach uruchom:

npm run build
dotnet test GameShell.sln
dotnet build GameShell.sln
.\build.ps1
.\package.ps1
==================================================
PODSUMOWANIE

Na końcu podaj:

główne decyzje wizualne,
zdefiniowaną paletę,
zmienione komponenty,
usunięte stare style,
wyniki buildów i testów,
jakie widoki sprawdzono przez MCP UI,
ewentualne elementy, które nadal wymagają dopracowania.

Najważniejsze kryterium:

GameShell ma wyglądać jak charakterystyczne, ciemne narzędzie do sterowania komputerem, a nie jak generyczny dashboard wygenerowany przez AI.