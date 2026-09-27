# CS2MPMod – lokal testpakke, protokoll 65

Denne pakken inneholder første retting av kommandorekkefølge, bekreftelse til
avsender, snapshot-startpunkt, bounded replay og opprydding ved full TCP-kø.
Oppfølgingen beholder byggehandlinger gjennom reparasjonsperioden når et festepunkt
ankommer sent, og trekker tilbake det tilhørende resync-varselet hvis punktet finnes.
Bekreftede feil under resync-grensen på 90 sekunder blir nå satt på vent, ikke glemt.
Avbrutt recovery beholder feilen for nytt forsøk; fullført snapshot fjerner den.
Pakken er bygget mot spillbiblioteket med Household.m_Income, ikke det eldre
m_SalaryLastDay. Protokoll 64 avviser eldre pakker selv om feltets bytebredde er lik.
Den endrer nettverksprotokollen: host og samtlige klienter må bruke samme komplette
pakke. Ikke bland DLL/UI/Steam-backend fra forskjellige builds.

Protokoll 65 legger til numeriske frakoblingsrapporter med mottakskvittering.
Klienten lagrer et øyeblikksbilde hvert 30. sekund og ved normal frakobling. Etter
ny autentisert tilkobling til samme konfigurerte adresse/port eller relay-mål
sendes utestående rapporter på nytt. De slettes lokalt først etter host-kvittering.
Samme adresse er ikke et kryptografisk bevis på at det er samme person som hoster.
Ingen rapport sendes automatisk til et annet konfigurert mål.

Rapporter ligger under spillets brukerdata: `Logs/CS2MP-diagnostics/outbox` hos
klienten og `Logs/CS2MP-diagnostics/received` hos host. Rapporten er en begrenset
numerisk oversikt (maks 32 rader / 2 KiB), ikke rå logger, save eller full baseline.
Host beholder maks 200 rapporter; klientutboksen maks 128. Ved full lagring beholdes
eksisterende filer, og en ikke-lagret rapport kvitteres ikke ut. Brå krasj kan miste
inntil 30 sekunders nylige målinger, mer hvis disken ikke kan lagre. En komplett
spilløkt er ennå ikke verifisert. `build/read-received-report.ps1` leser `.report` til JSON.

Lukk spillet før du erstatter en installert mod. Behold en kopi av forrige
modmappe **utenfor hele spillets brukerdataområde**, ikke bare utenfor Mods.
Spillets assetdatabase kan laste DLL/UI fra backupmapper under brukerdata.
Kontroller med `build/test-installed-package.ps1` og bekreft protokoll 65 i
oppstartsloggen etter omstart. Pakk ut `CS2MPMod` til den konfigurerte lokale Mods-mappen, og sørg for
at den gamle/originale multiplayer-modden ikke er aktiv samtidig. Byggescriptet
installerer eller publiserer ikke denne testpakken automatisk.

Test først med en kopi av en save:

1. Host bygger et enkelt bygg og en vei: alle klienter skal se resultatene.
2. Klient A bygger: host og klient B skal se bygget, uten et dobbelt bygg hos A.
3. La en ekstra spiller bli med etter flere handlinger. Bygg på nytt etter lasting.
4. Kjør én manuell world-sync; bekreft at nye handlinger fortsatt blir synlige.
5. Prøv flytting, oppgradering og rivning, og noter handlinger som fortsatt feiler.
6. Bygg en vei og deretter et bygg festet til veien tett etter hverandre. Gjenta fra
   både host og klient; se etter manglende bygg og unødvendige world-sync-pauser.

Send logger fra både host og berørte klienter ved feil. `build-manifest.json`
identifiserer pakkens filer med SHA-256. Core-regresjonene erstatter ikke spilltest
av faktisk native plassering, Steam-forbindelse eller grafiske forhåndsvisninger.

Byggepreview, ende-til-ende-kvittering på native commit og full målrettet reparasjon
er fortsatt videre arbeid i ROADMAP.md. Periodisk world-resync er ikke slått av.
Opprinnelig lisens og attribution følger med; dette er ikke en offentlig release.
