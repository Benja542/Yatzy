# Yatzy — spil, simulering og sandsynligheder

En C#-løsning der spiller yatzy med **6 terninger** og **20 slag** (maxi-yatzyblokken),
og som viser
sandsynligheden for at slå hvert af de slag man har tilbage — beregnet på **tre**
forskellige måder:

| Metode | Hvad den regner ud | Eksakt? |
| --- | --- | --- |
| **Analytisk** | Sandsynligheden i ét enkelt kast, med kombinatorik og eksakt brøkregning | Ja |
| **Udfaldstræ** | Sandsynligheden i en hel tur med omkast, ved at folde træet af kast og valg sammen bagfra | Ja |
| **Monte Carlo** | Et estimat af det samme, fundet ved at kaste terninger tilfældigt mange gange | Nej — med 95 % konfidensinterval |

De to eksakte metoder kontrollerer hinanden (med ét kast skal de give samme tal ned
til sidste decimal), og simuleringen kontrollerer dem begge.

Alle tre metoder besvarer det samme, meget konkrete spørgsmål:

> **Hvis jeg beholder præcis de terninger jeg har markeret og bruger resten af turen
> så godt som muligt, hvor ofte ender jeg så med den maksimale score i slaget?**

Målet kan skiftes til "mindst 1 point", og markeringen kan udelades — så regnes der
med den bedst mulige markering i stedet.

## Kør det

```bash
dotnet run --project src/Yatzy.Web        # brugerfladen — åbn adressen der skrives i konsollen
dotnet test                               # 117 enhedstests
```

Kommandolinjeværktøjet er til de tunge kørsler, hvor browseren bliver for langsom:

```bash
dotnet run --project src/Yatzy.Cli -- tabel --kast 200000       # de tre metoder side om side
dotnet run --project src/Yatzy.Cli -- tabel --maal point        # mål: bare at få point
dotnet run --project src/Yatzy.Cli -- analytisk                 # udregningerne og deres led
dotnet run --project src/Yatzy.Cli -- spil --spil 100000        # simulerer hele spil
```

## Brugerfladen

Blazor WebAssembly — alt regnes i browseren, der er ingen server.

* **Spil** — 1-6 spillere der skiftes til at tage en tur på det samme sæt terninger,
  med én kolonne pr. spiller på blokken. Klik på terningerne for at markere hvilke der
  beholdes; tabellen under dem viser med det samme sandsynligheden for maks point i
  hvert åbent slag **ud fra netop den markering** — eksakt fra udfaldstræet og estimeret
  med Monte Carlo. Kolonnen "Bedst mulige" viser hvad den bedste markering ville give,
  så man kan se hvad ens valg koster. Når alle blokke er fulde, vises slutstillingen.
* **Sandsynligheder** — hele tabellen for 1, 2 eller 3 kast og for begge mål, med de
  analytiske udregninger foldet ud led for led, og en kolonne der siger om den eksakte
  værdi ligger inden for simuleringens konfidensinterval.
* **Udfaldstræ** — træet tegnet op for en hånd man selv sammensætter og selv markerer.
  Roden bruger ens egen markering, resten af træet spilles optimalt. Nedenunder står
  alle 64 måder at dele hånden på, rangeret efter hvad de er værd, med ens egen fremhævet.
* **Simulering** — hele spil spillet igennem af en computerspiller, med fordelingen af
  slutscoren og hvor ofte hvert slag rammer.
* **Regler** — den variant der spilles her.

## Reglerne der spilles med

Seks terninger, tre kast pr. tur (første kast plus to omkast hvor man selv vælger
hvilke terninger der bliver liggende), 20 slag og dermed 20 runder. Efter tredje kast
skal hånden skrives — også hvis den giver 0 point.

Der kan være **1-6 spillere**. De har hver deres kolonne på blokken og skiftes til at
tage en tur: når en spiller har skrevet sit slag, ryddes terningerne, og den næste
spiller har tre nye kast. Spillet er slut når alle har fyldt deres blok, og den med
flest point vinder — er der lige mange point, deles førstepladsen.

| Slag | Krav | Point | Højest mulige |
| --- | --- | --- | ---: |
| Enere … Seksere | Mindst én terning med øjenværdien | Summen af dem | 6 … 36 |
| Et par | To ens | Summen af de to | 12 |
| To par | To par med forskellig øjenværdi | Summen af de fire | 22 |
| Tre par | Tre par med forskellig øjenværdi | Summen af alle seks | 30 |
| Tre ens | Tre ens | Summen af de tre | 18 |
| Fire ens | Fire ens | Summen af de fire | 24 |
| Fem ens | Fem ens | Summen af de fem | 30 |
| Lille straight | 1-2-3-4-5 blandt de seks terninger | 15 | 15 |
| Stor straight | 2-3-4-5-6 blandt de seks terninger | 20 | 20 |
| Fuld straight | 1-2-3-4-5-6 — alle seks øjenværdier | 21 | 21 |
| Hus | Tre ens + et par med anden øjenværdi | Summen af de fem | 28 |
| Villa | Tre ens + tre ens med forskellig øjenværdi | Summen af alle seks | 33 |
| Tårn | Fire ens + et par med anden øjenværdi | Summen af alle seks | 34 |
| Chance | — | Summen af alle seks | 36 |
| Yatzy | Seks ens | 100 | 100 |

**Bonus:** 100 point hvis de seks øverste slag tilsammen giver mindst 84 — det svarer
til fire terninger af hver øjenværdi (4·1 + 4·2 + … + 4·6 = 84). Grænsen er sat efter
antallet af terninger, ligesom den velkendte 63-grænse i 5-terningers-yatzy svarer til
tre af hver.

To steder hvor blokke er uenige med hinanden, og hvad der er valgt her:

* **Par skal have forskellig øjenværdi.** Fire ens tæller altså som ét par, ikke to,
  så 3-3-3-3-5-5 giver 0 i både to par og tre par. (Nogle regelsæt lader fire ens
  tælle som to par.)
* **Villa er 3+3 og tårn er 4+2**, begge med forskellig øjenværdi. Et hus (3+2) må
  gerne tages fra fire ens — 2-2-2-2-5-5 giver 16 i hus.

Begge dele sidder ét sted, i `YatzyRules.Score`, og er dækket af testene.

## Sådan regnes der

### 1. Analytisk (`AnalyticProbability`)

Ét kast med seks terninger har 6⁶ = 46.656 lige sandsynlige udfald. De gunstige tælles
med tre klassiske teknikker:

* **Komplementærreglen** for de øverste slag: P(mindst én sekser) = 1 − (5/6)⁶ = 31031/46656.
* **Inklusion-eksklusion** for de tre straights:
  Σ<sub>j=0..m</sub> (−1)<sup>j</sup> · C(m,j) · (6−j)⁶, hvor m er antallet af krævede
  øjenværdier. For lille og stor straight (m = 5) giver det 2520, altså 35/648;
  for fuld straight (m = 6) giver det 720 = 6!, altså 5/324.
* **Optælling over partitioner** for par, ens, hus, villa og tårn. Hvert kast har en "form" —
  en partition af 6, fx `4+2` eller `2+2+1+1` — og antallet af kast med formen λ er
  `6!/((6−k)!·∏mⱼ!) · 6!/∏λᵢ!`. De 11 partitioner af 6 dækker tilsammen præcis alle
  46.656 udfald, hvilket testene også tjekker.
* **Optælling over hænder** når målet er maks point. Formen er ikke nok — to par giver
  kun 22 med netop 6-6-5-5 — så der summeres i stedet over de af de 462 hænder der
  rammer maksimum, hver vægtet med sin multinomialkoefficient. Stadig eksakt, bare
  uden lukket formel.

Resultatet regnes i `Fraction` (BigInteger-brøker), så tabellen kan vise fx `1325/7776`
uden afrundingsfejl. Alle 20 slag efterprøves i testene mod en optælling af samtlige
46.656 udfald — for begge mål.

### 2. Udfaldstræ (`OutcomeTreeSolver`)

Rekursionen er en expectimax:

```
V(hånd, 0 omkast) = 1 hvis målet er nået, ellers 0
V(hånd, r omkast) = max over "behold"-mængder K ⊆ hånd af  Q(K, r)
Q(K, r)           = Σ P(udfald u) · V(K ∪ u, r−1)
```

Maks-leddet er spillerens valg, summen er terningernes tilfældighed. Resultatet er den
eksakte sandsynlighed når man spiller optimalt efter netop det slag.

Har spilleren selv markeret hvilke terninger der skal beholdes, springes maks-leddet
over i første omkast: så er svaret `Q(min markering, r)` — værdien af netop den gren.
Det er det tal brugerfladen viser, og `V` bliver i stedet til kolonnen "bedst mulige",
så man kan se hvad markeringen koster.

Et fuldt udfoldet træ over *ordnede* kast har 46.656 grene pr. kast og op til 64
beslutninger pr. knude — i størrelsesordenen 10²⁰ blade. To ting skærer det ned til
under 100.000 kanter pr. slag:

* Rækkefølgen af terningerne er ligegyldig, så en hånd beskrives som en tællevektor.
  Der findes kun C(11,5) = **462** hænder med 6 terninger og **924** "behold"-mængder
  i alt (`DiceCatalog`).
* Værdien af en hånd afhænger kun af hånden og antal omkast tilbage, så `V` beregnes
  én gang pr. hånd i stedet for én gang pr. gren.

### 3. Monte Carlo (`MonteCarloEstimator`)

Simuleringen kaster rigtige tilfældige terninger. Første omkast bruger spillerens egen
markering, præcis som `Q` ovenfor; derefter følges den strategi udfaldstræet har regnet
frem til. De to tal beskriver derfor samme hændelse, og forskellen er ren
simuleringsusikkerhed. Konfidensintervallet er et **Wilson
score-interval**, som i modsætning til det simple normalinterval også opfører sig
fornuftigt når sandsynligheden er meget lille — hvilket den er for yatzy (2,05 % på en
hel tur).

### Computerspilleren (`AutoPlayer`)

Samme udfoldning af træet som ovenfor, men med point i bladene i stedet for 0/1, og med
et bonus-incitament: for de øverste slag tæller point ud over "fire ens af øjenværdien"
ekstra. Det er en heuristik, ikke en optimal løsning af hele spillet — den ville kræve
et tilstandsrum på 2²⁰ blokke gange bonus-status. Med standardindstillingerne giver den
omkring **340 point** i gennemsnit og rammer bonussen i ca. **29 %** af spillene.

## Nogle af tallene

Sandsynligheden for at slå slaget i ét kast, og i en hel tur på tre kast hvor man kun
går efter netop det slag:

### Mindst 1 point

| Slag | 1 kast | Som brøk | Hel tur (3 kast) |
| --- | ---: | ---: | ---: |
| Enere … Seksere | 66,51 % | 31031/46656 | 96,24 % |
| Et par | 98,46 % | 319/324 | 100,00 % |
| To par | 55,62 % | 4325/7776 | 94,87 % |
| Tre par | 3,86 % | 25/648 | 27,26 % |
| Tre ens | 36,73 % | 119/324 | 88,87 % |
| Fire ens | 5,22 % | 203/3888 | 50,11 % |
| Fem ens | 0,399 % | 31/7776 | 15,72 % |
| Lille straight | 5,40 % | 35/648 | 43,72 % |
| Stor straight | 5,40 % | 35/648 | 43,72 % |
| Fuld straight | 1,54 % | 5/324 | 19,68 % |
| Hus | 17,04 % | 1325/7776 | 76,14 % |
| Villa | 0,643 % | 25/3888 | 16,26 % |
| Tårn | 0,965 % | 25/2592 | 19,49 % |
| Chance | 100 % | 1 | 100,00 % |
| Yatzy | 0,0129 % | 1/7776 | 2,05 % |

De 96,24 % kan tjekkes i hovedet: beholder man alle terninger med den rigtige
øjenværdi, får hver terning tre uafhængige forsøg, så P = 1 − ((5/6)³)⁶ = 96,243 %.
Udfaldstræet rammer det tal ned til sidste decimal.

### Maks point

Et helt andet - og noget barskere - billede:

| Slag | Maks | 1 kast | Hel tur (3 kast) |
| --- | ---: | ---: | ---: |
| Enere … Seksere | 6 … 36 | 0,0021 % | 0,56 % |
| Et par | 12 | 26,32 % | 79,84 % |
| To par | 22 | 4,22 % | 45,53 % |
| Tre par | 30 | 0,193 % | 8,93 % |
| Tre ens | 18 | 6,23 % | 49,98 % |
| Fire ens | 24 | 0,870 % | 20,99 % |
| Fem ens | 30 | 0,066 % | 5,17 % |
| Lille/stor straight | 15 / 20 | 5,40 % | 43,72 % |
| Fuld straight | 21 | 1,54 % | 19,68 % |
| Hus | 28 | 0,589 % | 18,52 % |
| Villa | 33 | 0,043 % | 4,23 % |
| Tårn | 34 | 0,032 % | 3,34 % |
| Chance | 36 | 0,0021 % | 0,56 % |
| Yatzy | 100 | 0,0129 % | 2,05 % |

Bemærk at maks i enere er seks 1'ere — lige så svært som en yatzy af 1'ere, og præcis
lige så svært som maks i chance (seks 6'ere). For straights og yatzy er de to mål den
samme hændelse, fordi pointtallet er fast; derfor er de rækker uændrede.

## Projekter

```
src/Yatzy.Core    Regler, terningekatalog, de tre sandsynlighedsmodeller,
                  spillere og turskifte, computerspiller
src/Yatzy.Web     Blazor WebAssembly-brugerfladen
src/Yatzy.Cli     Kommandolinjeværktøj til store kørsler
tests/            117 enhedstests
```

## Udgivelse til GitHub Pages

`.github/workflows/deploy.yml` bygger og udgiver `Yatzy.Web` til GitHub Pages. Den
starter **kun manuelt** (Actions → "Udgiv til GitHub Pages" → Run workflow), så den ikke
overskriver et eksisterende site ved et uheld. Skal den køre automatisk ved hver
push til `main`, fjernes kommentaren omkring `push:`-udløseren i filen.

`wwwroot/.nojekyll` og `wwwroot/404.html` er nødvendige på GitHub Pages: den første
fordi Jekyll ellers ignorerer `_framework`-mappen, den anden fordi Pages ikke kan
serverside-route en enkeltside-app — 404-siden er en kopi af `index.html`, så
dybe links som `/udfaldstrae` virker.
