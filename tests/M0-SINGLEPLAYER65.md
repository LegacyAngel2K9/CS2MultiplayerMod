# M0 – singleplayer, protokoll 65, 18. september 2026

Økten avsluttet rent (`run-end clean=true`). Logger er arkivert lokalt i
`build/reports/singleplayer65-20260918`, inkludert filtrert `frames.json`.
Rålogger er ikke egnet for offentlig deling; eksporten utelater private identifikatorer.

## Målinger

- Runtime: protokoll 65, spill 1.6.2f1. Frame-vinduer er merket singleplayer;
  health viser Role=None / Offline.
- 214 validerte vinduer, totalt 6420 loggede sekunder (107 minutter; avrundet
  varighet per vindu) og 180 157 frameintervaller.
- Logget gjennomsnitt per vindu: 26–56 ms. Høyeste registrerte enkeltintervall:
  1028 ms. Dette er ikke eksakte frame-kvantiler eller isolert mod-overhead.
- Ingen `m_SalaryLastDay`-feil eller feilposter ble funnet ved kontrollen like
  før avslutning. Den gamle backup-lastingen var ikke aktiv i denne økten.

Dette er brukbare singleplayer-observasjoner, men ikke en ferdig kontrollert
baseline: samme utgangssave, handlingssekvens og grafikk-/simulasjonsinnstillinger
må dokumenteres og gjentas med 2 og 4 spillere. Ingen nettverkslatency kan utledes.

## Bekreftet kompatibilitetsavvik

Advarselen om utilgjengelig object definition generator forekommer også i denne
korrekte versjonen. Inspeksjon av installert Game.dll med Mono.Cecil bekrefter at
`Game.Tools.ObjectToolBaseSystem.CreateDefinitions` har **24 parametere**.
Moddens `NativeDerive.cs` krever 23 og lager en argumentliste med 23 elementer.

Spillets parameter nummer 22 (nullbasert) er nå
`Game.Tools.PlacementOverrides overrides`; `JobHandle inputDeps` er nummer 23.
RandomSeed.m_Seed finnes fortsatt som privat UInt32. Parameterantallsjekken
forklarer derfor hvorfor modden deaktiverer generatorbanen.

Det er ikke nok å endre tallet 23 til 24: argumentlisten og signaturvalideringen
må tilpasses, og riktig PlacementOverrides-semantikk må undersøkes. Etter en
retting må flytting, oppgradering og asset-stamp testes i spillet. Dette avviket
forklarer fallback-advarselen, ikke nødvendigvis alle rapporterte sync-feil.

## Kodeoppfølging

Generator-kallet er nå tilpasset 24 parametere, med `default(PlacementOverrides)`
før inputDeps. IL-inspeksjon bekrefter at UpgradeToolSystem bruker samme standardverdi.
ObjectToolSystem har også mulighet for eksplisitte mesh-/gruppe-/probability-overrides;
disse editorinnstillingene er ikke representert i moddens gameplay-kommandoer og
blir ikke hentet fra mottakerens lokale verktøy. Eksakt parameter-/returtype og
seedfelttype valideres før banen aktiveres. Metadata-sjekk av alle 24 typer består.
Dette er ikke en utført native plasseringstest; ny pakke må prøves i spillet.
