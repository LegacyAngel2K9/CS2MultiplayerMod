# M0: verifikasjon og gjenstående spilltest

M0 er **ikke fullført** før den rapporterte byggefeilen er dokumentert på tvers av
host og klient, og baseline er målt. Automatiske tester er ikke bevis for Unity-plassering.

## Reproduserbar lokal validering

Seneste Core-kjøring: **70/70 tester består**. Snapshot-testene dekker forsinkede
chunks etter nyere Begin, etter Abort og etter Resume, samt vellykket mottak i
neste epoch. Kodek og sesjonslogikk er ekte; transport og klokke er kontrollerte.
Dette er ikke en bekreftelse på at selve byen lastes riktig i Unity.

Oppfølging 18. september: **66/66 Core-tester består**, inkludert kartmottak gjennom
ekte sesjonskode og meldingskodek med falsk transport. Ugyldig mottak må ikke nå
observeren; en gyldig retry må leveres med riktig innhold, uten beholdt fremdrift
eller mutasjon av tidligere payload. Kanal-/størrelses-/epochgrenser og hostens
avvisning av klientopplasting testes separat. Dette er ikke en test av spillinnlasting
eller nettverksforbindelser mellom maskiner. Tidligere testantall nedenfor er historikk.

Utført 17. september 2026: 38/38 C#-regresjoner, eksporttesten med private testdata,
positiv preflight og negativ test med ugyldig process-toolpath bestod. Full net48-
mod, Unity-postprosessering, Steam og webpack bygget med 0 kompilatorfeil/-advarsler.
`npm ci` varslet om to blokkerte install-scripts; UI-bygget bestod likevel.
Nyeste testpakke: `build/packages/CS2MPMod-protocol65-test.zip`. Ikke installert/publisert.
GitHub-jobbene er konfigurert, men ikke kjørt på GitHub i denne arbeidsøkten.

- `build/test-build-environment.ps1 -CoreOnly`: stabil SDK fra global.json.
- `dotnet run --project tests/CS2MPMod.Core.Tests -c Release`: ekte Core-kode og
  arbiter/innboks uten spillinstallasjon, med falsk transport og styrt klokke.
- `tests/test-diagnostic-export.ps1`: eksporttest med syntetiske private data.
- `build/build-test-package.ps1`: full preflight, tester, låst `npm ci`, net48,
  Unity-postprosessering, Steam-backend og webpack i prosjektets staging-mappe.
  Ingen automatisk installasjon eller publisering. Spillverktøykjeden er nødvendig
  for fullbygget, men ikke Core-testene.

## Diagnostikkontrakt og avgrensning

`operation`-linjer har lokal anonym session-ID, epoch, sekvens, lokal spiller-ID,
kommando, operasjons-ID/prefab når dekodet, stage, capture/apply-status,
lokal forløpt tid og eventuell feilårsak. Før sekvenstildeling er epoch/sekvens
uttrykkelig ukjent. Koble maskiner med epoch + spiller + operation/sekvens;
session-ID er lokal, ikke en delt nettverks-ID.

Native object-tool- og asset-stamp-baner logger capture, mottak, decode,
retry/armed/rejected og native commit-callback eller verifisert ekvivalent objekt.
Andre domener har mottakssporing, men ikke full native commit-instrumentering ennå.
`completedObserved` er høyeste **observerte** ferdige sekvens, aldri `AppliedThrough`.
En senere ferdig operasjon skjuler ikke en eldre ventende operasjon.
Mottak er ikke kvittering for native plassering eller korrekt pengebruk.

Oversikten kommer hvert femte sekund. Den beholder maksimalt 2048 operasjoner og
rapporterer eviksjoner; tallene er et begrenset historikkvindu, ikke livstidsteller.
Snapshot-barrieren tømmer historikken for gammel epoch. Tid måler lokalt
mottak → observerbar status, ikke end-to-end-latency mellom usynkroniserte klokker.

M0-oppfølging: `queueMs` måler mottak til første decode/apply-start, mens `applyMs`
måler derfra til første observert completion. Retry starter ikke klokken på nytt,
og duplikat-completion forlenger ikke målingen. Manglende startpunkt gir `unknown`,
ikke null millisekunder. Vanlige og sammensatte veibatcher logger nå armed,
commit-lost/retry og fullført commit. `oldestPendingSequence` gjør den eldste
ventende operasjonen identifiserbar i eksporten. Full dekning av andre domener gjenstår.

Ruteoppfølgingen beholder kun meldingsidentitet gjennom create/update-replay.
`submitted` er ikke ferdigstilling, og create-commit går via `waiting-identity`
til nummer/farge er anvendt på den identifiserte ruten. Update-commit sammenlignes
med forventet snapshot; manglende eller avvikende snapshot gir `commit-unverified`.
Denne statusen observerer avviket, men innfører ingen ny recovery-regel.
Logisk rutedeletion regnes anvendt når Deleted-komponenten er satt; fysisk ECS-
opprydding måles ikke. Sammenfalte create-forespørsler med likt nummer, prefab,
geometri og farge følger nå originalens completion/rejection. Ulik farge eller
manglende korrelasjon gir ikke falsk completion. Koblingene kan ikke danne kjeder,
sykluser eller krysse epoch; de lever bare i det begrensede diagnostikkvinduet.
Full rutetest i spillet gjenstår.
To nye modelltester gir 42 C#-regresjoner totalt. Eksporttesten dekker også nye
rutestatuser og allowlistede årsakskoder; vilkårlig feiltekst eksporteres ikke.

### Lokal oppsummering av målingene

Frame-målinger eksporteres separat med
`build/export-frame-diagnostics.ps1 -LogPath <flight-logg> -OutputPath <frames.json>`.
Eksporten beholder frame-antall, avrundet varighet/rate/gjennomsnitt, verste intervall
og sju disjunkte histogram-buckets. Den utleder ikke eksakte p50/p95/p99 fra buckets
og identifiserer ikke moddens isolerte CPU-kostnad. Kjør eksporten én gang per
maskin/logg; lokale run-aliaser kan ikke brukes som felles maskinidentitet.
FrameProbe er nå også koblet til lastet singleplayer-by med modden aktiv, uten
aktiv sesjon. `frameScope` er singleplayer/host/client; gamle logger har null.
Rollebytte og lasting nullstiller vinduet. Dette gir ikke en mod-av-baseline;
innsamling i spillet må fortsatt verifiseres med kontrollerte scenarioer.
Save, scenario og pakkesamsvar må fortsatt dokumenteres ved spilltesten.
Eksporttest: `tests/test-frame-export.ps1`. Historisk lokal eksport er generert i
`build/reports/local-september15-frames.json`, uten råtekst eller private identifikatorer.

Analyseinput må være eksportformatet med numerisk `schema=1`, `records` som array
og ikke-negativt heltall i `truncatedRecords`. Session-aliaser må ha eksportens
`session-N`-format. Oppgitte måle-/sekvensfelt krever ikke-negative heltall;
null/manglende valgfrie målefelt forblir ukjent. Ugyldig input avvises før output
skrives, slik at en eksisterende rapport ikke erstattes av en misvisende analyse.

`reasonOutcomes` grupperer siste observerte status og godkjent årsakskode per
session/epoch/avsender. Hver unik operasjon telles én gang, også ved gjentatte
retries. En senere completion erstatter en tidligere retry i denne oversikten.
Ukjente eller manglende årsakskoder blir `unknown`; vilkårlig fritekst kopieres
ikke. Eksport og analyse deler `build/diagnostic-reason-codes.ps1` som allowlist.
Dette er **ikke resync-antall eller bekreftede resync-årsaker**, og historiske
retry-hendelser telles ikke. Vellykkede operasjoner kan ha en suksessårsak.

`peakSampledPendingPerPeer=null` betyr at ingen kømåling finnes; verdien 0 krever
en faktisk måling. `dataQuality.queueSummarySamples` teller køsamples, mens
`queueSummarySamplesWithoutEpoch` viser hvor mange som ikke kan fordeles per epoch.
Varslene `no-queue-summary-samples` og `queue-summary-samples-missing-epoch`
gjør hullene eksplisitte. De samlede tallene inkluderer eldre samples uten epoch,
men `epochQueues` gjør det ikke.

`epochQueues` viser køoversikter separat per lokal session, epoch og avsender:
siste samplede kølengde, høyeste samplede kølengde, mottatt/fullført observert
sekvens og eldste ventende sekvens/alder. Siste betyr sist i eksportens rekkefølge,
ikke nåtilstand. Poster uten epoch tilordnes ikke en antatt epoch; manglende felter
forblir null. Også vinduer med bare køoversikter vises, uten oppdiktede operasjoner
eller timing. `peers` er fortsatt en samlet oversikt på tvers av epochs.

Oppfølging 18. september: `epochs` i oppsummeringen viser separate lokale
session-/epoch-vinduer med antall fullførte, ventende og avviste operasjoner,
kø-/apply-kvantiler og manglende timing. Dette skiller observasjoner før/etter en
epoch-endring uten å slå dem sammen. Samlede tall og peer-tall beholdes;
`aggregate-timings-pool-multiple-session-epochs` varsler når flere vinduer inngår.
En epoch-endring beviser ikke at resync lyktes. Køoppsummeringer uten epoch
tilordnes ikke et slikt vindu. Testene bruker syntetiske data, ikke spillmålinger.

Etter eksport kan `build/summarize-sync-diagnostics.ps1 -InputPath <eksport.json>`
beregne lokale kø-/apply-kvantiler (nearest-rank p50/p95/p99), antall unike observerte
operasjoner og statuser. Hver fullført sekvens telles én gang, manglende tider holdes
utenfor kvantilene og telles separat. Ventende/avviste operasjoner er ikke null-latency.
Rapporten merker også eksporttrunkering, observerte eviksjoner og høyeste samplede
kø per peer. Den kan ikke beregne nettverkslatency, framekostnad eller båndbredde
fra felter som ikke finnes. Slike baseline-målinger står fortsatt åpne.

Rapportens `peers` deler observasjonene per lokal session og kommandoavsender,
med egne kvantiler, uferdige/avviste operasjoner og manglende timing. Dette hindrer
at én avsenders operasjoner skjules i et samlet gjennomsnitt. `player` er **ikke**
mottakermaskinen: faktisk sammenligning av klientenes ytelse krever én rapport fra
hver maskin. Session-aliaser fra ulike eksporter er ikke globale identiteter.
Regresjonstesten bruker en rask og en treg syntetisk operasjon, ventende arbeid og
samme spiller-ID i en annen sesjon; ingen av tallene er spillmålinger.

Avsendere med bare periodiske køoppsummeringer inkluderes også, selv om eksporten
ikke har individuelle operasjonsposter for dem. `lastSampledPending`,
`peakSampledPending`, `lastSampledOldestPendingMs` og
`lastSampledOldestPendingSequence` beskriver innsamlede målinger, ikke nåtilstand.
`hasSummarySamples=false` og nullverdier betyr manglende data, ikke tom kø.
Antall individuelle operasjoner og målt kølengde kan derfor være ulike; rapporten
skal ikke oppfinne operasjoner eller completion-tider for å få tallene til å stemme.

Fire nye koblingsregresjoner gir 46 C#-tester totalt. PowerShell-oppsummeringen
testes separat med syntetiske data; disse tallene er ikke en målt spillbaseline.

`build/export-sync-diagnostics.ps1 -LogPath <flight-logg>` eksporterer kun
allowlistede tall/statusfelt og remapper session-ID. Ingen rå fritekst, prefabnavn,
stier, IP-/Steam-adresser eller spillernavn eksporteres. Maks 20 000 poster / 128
sessions, med eksplisitt trunkering. Den originale loggen endres ikke.

## Spilltest som gjenstår

Bruk kopier av samme save og samme komplette protokoll-65-pakke/spillversjon på
alle maskiner. Noter pakkens manifest-hash, spillversjon, DLC og maskinvare separat.

Brukeren vil lage en ny by. Lag og lagre én baseline-by før testserien; bruk deretter
kopier av nettopp denne save-filen i alle scenarioene. Dette er en plan, ikke en
opprettet/testet save i denne arbeidsøkten.

### Automatisk rapport ved disconnect/reconnect

Epoch-isolering: Rapportens rader filtreres til headerens epoch, også hvis lokal
diagnostikk inneholder eldre observasjoner. Eldre arbeid regnes dermed ikke som
ventende eller ferdig i en ny epoch. Periodiske loggoversikter beholder separate
epoch-/spillergrupper. Den eksisterende samlede peer-analysen er fortsatt aggregert;
bruk individuelle poster og `epochs` for timing per epoch. Eviksjonstelleren
beskriver hele diagnostikkvinduet, ikke bare de filtrerte radene.

Protokoll 65 har en separat, autentisert og størrelsesbegrenset rapportmelding.
Map/blob-kanalen er fortsatt host → klient; rapportopplasting åpner ikke for å laste
en klient-save på host. Hver rapport inneholder GUID, epoch, eviksjoner og maksimalt
32 numeriske avsenderoversikter. Det følger ikke rå loggtekst, IP, navn, prefabnavn,
passord eller save-data. Den er ikke en full erstatning for M0-eksporten/baselinen.

Klient: checkpoint hvert 30. sekund og ved normal frakobling. Utboksen tåler at
service/prosess opprettes på nytt. Den velger kun rapporter for samme hash av det
konfigurerte målet; dette er endpoint-binding, ikke bekreftet kryptografisk host-identitet.
Levende checkpoints sendes ikke før de fryses ved frakobling; eldre rapporter prøves
hvert 10. sekund etter autentisert reconnect. Host kvitterer bare etter lagring,
og identiske duplikater gjenbruker samme fil. En ID med annet innhold overskriver ikke.
Klienten sletter kun den utestående rapporten som host kvitterer for.

Lagring: spillets brukerdata under `Logs/CS2MP-diagnostics/outbox` og `received`.
Utboksen har maks 128 rapporter, host maks 200, midlertidige filer maks 128 per mappe.
Full/feilende disk gir ikke falsk kvittering; eksisterende filer beholdes. Ved krasj
kan det siste checkpointet være opptil 30 sekunder gammelt, eller eldre ved diskfeil.
`build/read-received-report.ps1` gjør en mottatt `.report` lesbar som JSON.

53 C#-regresjoner dekker nå blant annet kontrakt, formatteringsinjeksjon, kodekgrenser,
ikke-godkjent peer, meldingsretning, mottakskvittering etter lagring, utboks etter ny
instans, målseparasjon, ID-konflikt og idempotent mottak. Reader/eksport/oppsummering
har egne PowerShell-tester. Ingen faktisk TCP-/Steam-flermaskinøkt er utført.

1. Kjør 10 minutter singleplayer med avtalt kamerarute og 100 forhåndsbestemte handlinger.
2. Gjenta med host + én klient, deretter host + tre klienter. Kjør også host + to
   klienter for den rapporterte feilens reproduksjon.
3. Host bygger vei, veifestet servicebygg, fritt bygg og utvidelse; flytt, oppgrader
   og riv. Gjenta fra hver klient. Tell faktiske objekter og kontroller pengebruk.
4. Gjenta før/etter første join-resync, etter manuell sync og etter sen tredje spiller.
5. Samle samtidige logger fra hver deltaker. Noter nøyaktig handling og synlig
   sluttresultat. Korrelér capture → mottak → native commit, ikke bare meldingsrekkefølge.

Registrer p50/p95/p99 lokal apply-forsinkelse, FPS/framekostnad, køens topp/eldste alder,
RAM, sendte/mottatte bytes, resync-antall/årsaker og resultat per handling. End-to-end
synlig forsinkelse krever separat tidsreferanse/video; beregn den ikke fra lokale klokker.

| Scenario | Save/pakke | Handlinger | Manglende/doble bygg | Latency | Frame/kø/bytes | Resync-årsaker |
|---|---|---|---|---|---|---|
| Singleplayer | Ikke målt | Ikke kjørt | Ikke målt | Ikke målt | Ikke målt | Ikke målt |
| 2 spillere | Ikke målt | Ikke kjørt | Ikke målt | Ikke målt | Ikke målt | Ikke målt |
| 3 spillere, sen join | Ikke målt | Ikke kjørt | Ikke målt | Ikke målt | Ikke målt | Ikke målt |
| 4 spillere | Ikke målt | Ikke kjørt | Ikke målt | Ikke målt | Ikke målt | Ikke målt |
