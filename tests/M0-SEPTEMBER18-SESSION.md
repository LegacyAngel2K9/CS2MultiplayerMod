# Testøkt 18. september – feil versjon lastet

## Årsak og korrigering

Installasjonsbackupen var feilaktig plassert under spillets brukerdata i
`ModBackups`. Modding-loggen registrerte Burst- og UI-innlasting fra denne mappen
i tillegg til Mods-installasjonen. Oppstartsloggen bekrefter protokoll 62 med
spill 1.6.2f1. Den korrekte protokoll-65-DLL-en i Mods hadde riktig SHA-256, men
det beviste ikke hvilken assembly prosessen brukte.

Etter at spillet var lukket ble de tre relevante loggene arkivert lokalt under
ignorert `build/reports`. Sletting av backup ble blokkert av verktøyet; backupen
ble i stedet flyttet utenfor hele spillbrukerdataområdet, til brukerens separate
`CS2MPMod-Backups`-mappe. Den kan gjenopprettes. Ny kontroll finner én CS2MPMod.dll
under spillbrukerdata og verifiserer alle 11 installerte filer mot manifestet.
Neste oppstart må fortsatt bekrefte protokoll 65 i runtime-loggen.

## Observasjoner fra siste prosessøkt

- Omtrent 50 minutter fra oppstart til ren avslutning. Hosting startet cirka
  fem minutter etter oppstart; 271 health-poster har Host-rolle. Ingen health-post
  viser tilkoblede peers. Dette er ikke en ren singleplayer-økt eller en flermaskintest.
- 90 gyldige frame-vinduer, 104 141 registrerte frameintervaller. Loggede, avrundede
  gjennomsnitt per vindu varierer fra 23 til 30 ms; høyeste enkeltintervall er 2850 ms.
  Dette er ikke eksakte latency-kvantiler eller et mål på moddens isolerte overhead.
- Manglende `Household.m_SalaryLastDay` rapporteres i den gamle modden. Nåværende
  kildekode bruker `m_Income`; feilen krever først korrekt runtime-versjon, ikke en
  ny blind endring av samme felt.
- Varsel om utilgjengelig object definition generator indikerer redusert replikering
  ved oppgradering/flytting i den gamle versjonen. Må kontrolleres med protokoll 65
  før det kan brukes som bevis for en feil i dagens kode.
- Den opprinnelige frame-loggen inneholder flere eldre prosessøkter. Tallene ovenfor
  er fra den siste isolerte økten, ikke alle 447 vinduer i hele filen.

## Ny forebyggende kontroll

`build/test-installed-package.ps1` avviser doble CS2MPMod.dll-filer under hele
spillbrukerdataområdet og sjekker installerte filhasher. Testene bruker egne
syntetiske filer, og dekker gyldig installasjon, endret DLL og backup utenfor Mods
men innenfor brukerdata. Ingen save eller gameplay-kode ble endret i korrigeringen.

## Neste test

Start spillet på nytt og kontroller protokoll 65. Last en by uten å trykke Host
eller Join. Nye frame-linjer skal ha `frameScope=singleplayer`. Først da kan økten
brukes som singleplayer-delen av M0-baselinen.
