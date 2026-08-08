# Yatzy — spil, simulering og sandsynligheder

En C#-løsning der spiller yatzy med **6 terninger** og **15 slag**, og som viser
sandsynligheden for at slå hvert af de slag man har tilbage — beregnet på **tre**
forskellige måder:

| Metode | Hvad den regner ud | Eksakt? |
| --- | --- | --- |
| **Analytisk formel** | Sandsynligheden i ét enkelt kast, med kombinatorik og eksakt brøkregning | Ja |
| **Udfaldstræ** | Sandsynligheden i en hel tur med omkast, ved at folde træet af kast og valg sammen bagfra | Ja |
| **Monte Carlo** | Et estimat af det samme, fundet ved at kaste terninger tilfældigt mange gange | Nej — med 95 % konfidensinterval |

De to eksakte metoder kontrollerer hinanden (med ét kast skal de give samme tal ned
til sidste decimal), og simuleringen kontrollerer dem begge.

## Kør det

```bash
dotnet run --project src/Yatzy.Web        # brugerfladen — åbn adressen der skrives i konsollen
dotnet test                               # 90 enhedstests
```

Kommandolinjeværktøjet er til de tunge kørsler, hvor browseren bliver for langsom:

```bash
dotnet run --project src/Yatzy.Cli -- tabel --kast 200000   # de tre metoder side om side
dotnet run --project src/Yatzy.Cli -- analytisk             # formlerne og deres led, skrevet ud
dotnet run --project src/Yatzy.Cli -- spil --spil 100000    # simulerer hele spil
```

## Brugerfladen

Blazor WebAssembly — alt regnes i browseren, der er ingen server.

* **Spil** — 1-6 spillere der skiftes til at tage en tur på det samme sæt terninger,
  med én kolonne pr. spiller på blokken. Under terningerne står sandsynligheden for
  hvert af de slag *den spiller der har tur* har åbne, og hvilke terninger der skal
  beholdes for at maksimere den. Monte Carlo-estimatet kører i baggrunden og opdaterer
  sig selv. Når alle blokke er fulde, vises slutstillingen med vinderen.
* **Sandsynligheder** — hele tabellen for 1, 2 eller 3 kast, med de analytiske formler
  foldet ud led for led, og en kolonne der siger om den eksakte værdi ligger inden for
  simuleringens konfidensinterval.
* **Udfaldstræ** — træet tegnet op for en hånd man selv sammensætter, plus alle 64
  måder at dele hånden på, rangeret efter hvad de er værd.
* **Simulering** — hele spil spillet igennem af en computerspiller, med fordelingen af
  slutscoren og hvor ofte hvert slag rammer.
* **Regler** — den variant der spilles her.

## Reglerne der spilles med

Seks terninger, tre kast pr. tur (første kast plus to omkast hvor man selv vælger
hvilke terninger der bliver liggende), 15 slag og dermed 15 runder. Efter tredje kast
skal hånden skrives — også hvis den giver 0 point.

Der kan være **1-6 spillere**. De har hver deres kolonne på blokken og skiftes til at
tage en tur: når en spiller har skrevet sit slag, ryddes terningerne, og den næste
spiller har tre nye kast. Spillet er slut når alle har fyldt deres blok, og den med
flest point vinder — er der lige mange point, deles førstepladsen.

| Slag | Krav | Point |
| --- | --- | --- |
| Enere … Seksere | Mindst én terning med øjenværdien | Summen af dem |
| Et par | To ens | Summen af de to |
| To par | To par med forskellig øjenværdi | Summen af de fire |
| Tre ens / Fire ens | Tre eller fire ens | Summen af dem |
| Lille straight | 1-2-3-4-5 blandt de seks terninger | 15 |
| Stor straight | 2-3-4-5-6 blandt de seks terninger | 20 |
| Fuldt hus | Tre ens + et par med anden øjenværdi | Summen af de fem |
| Chance | — | Summen af alle seks |
| Yatzy | Seks ens | 100 |

**Bonus:** 100 point hvis de seks øverste slag tilsammen giver mindst 84 — det svarer
til fire terninger af hver øjenværdi (4·1 + 4·2 + … + 4·6 = 84). Grænsen er sat efter
antallet af terninger, ligesom den velkendte 63-grænse i 5-terningers-yatzy svarer til
tre af hver.

## Sådan regnes der

### 1. Analytisk (`AnalyticProbability`)

Ét kast med seks terninger har 6⁶ = 46.656 lige sandsynlige udfald. De gunstige tælles
med tre klassiske teknikker:

* **Komplementærreglen** for de øverste slag: P(mindst én sekser) = 1 − (5/6)⁶ = 31031/46656.
* **Inklusion-eksklusion** for de to straights:
  Σ<sub>j=0..5</sub> (−1)<sup>j</sup> · C(5,j) · (6−j)⁶ = 2520, altså 35/648.
* **Optælling over partitioner** for par, ens og fuldt hus. Hvert kast har en "form" —
  en partition af 6, fx `4+2` eller `2+2+1+1` — og antallet af kast med formen λ er
  `6!/((6−k)!·∏mⱼ!) · 6!/∏λᵢ!`. De 11 partitioner af 6 dækker tilsammen præcis alle
  46.656 udfald, hvilket testene også tjekker.

Resultatet regnes i `Fraction` (BigInteger-brøker), så tabellen kan vise fx `1325/7776`
uden afrundingsfejl. Alle 15 formler efterprøves i testene mod en optælling af samtlige
46.656 udfald.

### 2. Udfaldstræ (`OutcomeTreeSolver`)

Rekursionen er en expectimax:

```
V(hånd, 0 omkast) = 1 hvis slaget er opfyldt, ellers 0
V(hånd, r omkast) = max over "behold"-mængder K ⊆ hånd af  Σ P(udfald u) · V(K ∪ u, r−1)
```

Maks-leddet er spillerens valg, summen er terningernes tilfældighed. Resultatet er den
eksakte sandsynlighed når man spiller optimalt efter netop det slag.

Et fuldt udfoldet træ over *ordnede* kast har 46.656 grene pr. kast og op til 64
beslutninger pr. knude — i størrelsesordenen 10²⁰ blade. To ting skærer det ned til
under 100.000 kanter pr. slag:

* Rækkefølgen af terningerne er ligegyldig, så en hånd beskrives som en tællevektor.
  Der findes kun C(11,5) = **462** hænder med 6 terninger og **924** "behold"-mængder
  i alt (`DiceCatalog`).
* Værdien af en hånd afhænger kun af hånden og antal omkast tilbage, så `V` beregnes
  én gang pr. hånd i stedet for én gang pr. gren.

### 3. Monte Carlo (`MonteCarloEstimator`)

Simuleringen kaster rigtige tilfældige terninger og bruger nøjagtig den strategi
udfaldstræet har regnet frem til. De to tal beskriver derfor samme hændelse, og
forskellen er ren simuleringsusikkerhed. Konfidensintervallet er et **Wilson
score-interval**, som i modsætning til det simple normalinterval også opfører sig
fornuftigt når sandsynligheden er meget lille — hvilket den er for yatzy (2,05 % på en
hel tur).

### Computerspilleren (`AutoPlayer`)

Samme udfoldning af træet som ovenfor, men med point i bladene i stedet for 0/1, og med
et bonus-incitament: for de øverste slag tæller point ud over "fire ens af øjenværdien"
ekstra. Det er en heuristik, ikke en optimal løsning af hele spillet — den ville kræve
et tilstandsrum på 2¹⁵ blokke gange bonus-status. Med standardindstillingerne giver den
omkring **254 point** i gennemsnit og rammer bonussen i ca. **25 %** af spillene.

## Nogle af tallene

Sandsynligheden for at slå slaget i ét kast, og i en hel tur på tre kast hvor man kun
går efter netop det slag:

| Slag | 1 kast | Som brøk | Hel tur (3 kast) |
| --- | ---: | ---: | ---: |
| Enere … Seksere | 66,51 % | 31031/46656 | 96,24 % |
| Et par | 98,46 % | 319/324 | 100,00 % |
| To par | 55,62 % | 4325/7776 | 94,87 % |
| Tre ens | 36,73 % | 119/324 | 88,87 % |
| Fire ens | 5,22 % | 203/3888 | 50,11 % |
| Lille straight | 5,40 % | 35/648 | 43,72 % |
| Stor straight | 5,40 % | 35/648 | 43,72 % |
| Fuldt hus | 17,04 % | 1325/7776 | 76,14 % |
| Chance | 100 % | 1 | 100,00 % |
| Yatzy | 0,0129 % | 1/7776 | 2,05 % |

De 96,24 % kan tjekkes i hovedet: beholder man alle terninger med den rigtige
øjenværdi, får hver terning tre uafhængige forsøg, så P = 1 − ((5/6)³)⁶ = 96,243 %.
Udfaldstræet rammer det tal ned til sidste decimal.

## Projekter

```
src/Yatzy.Core    Regler, terningekatalog, de tre sandsynlighedsmodeller,
                  spillere og turskifte, computerspiller
src/Yatzy.Web     Blazor WebAssembly-brugerfladen
src/Yatzy.Cli     Kommandolinjeværktøj til store kørsler
tests/            90 enhedstests
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
