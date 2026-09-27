# CS2MPMod – roadmap for pålitelig flerspiller

M1 – etterkontroll av oppgraderingscommit (19. september): Ubekreftet native
oppgraderingsrot kan kontrolleres igjen uten å bygge på nytt. Køen har maks 64
elementer, fire kontroller per pump, minst 250 ms mellom forsøk og 10 sekunders
levetid. Bekreftelse utløser samme engangssperrede betalingsforsøk. Utløp forblir
ubekreftet/ubetalt; ingen automatisk resync eller blind betalingsretry. Drenering
av sesjonskøen kansellerer kontrollene og ugyldiggjør eldre callbacks. To nye
Core-tester dekker kapasitet, budsjett, rettferdig kø, frist og kansellering:
**74/74 består.** Faktiske Unity-oppgraderinger og økonomiavstemming gjenstår.

M1 – native oppgraderingsbetaling (19. september): Belastning er flyttet fra Armed
til native completion med verifisert oppgraderingsrot. En engangssperre per native
commit-forsøk hindrer dobbelt effekt ved gjentatt callback; ved unntak forsøkes
ikke automatisk igjen, fordi penger kan være trukket før unntaket. To Core-tester
dekker verifikasjonskrav, duplikat og delvis feil. **72/72 Core-tester består.**
Dette er ikke en varig betalingsjournal: reconnect/replay på tvers av forsøk,
fallback-betaling, uteblitt verifikasjon og pengeavstemming må fortsatt håndteres.
Ubekreftet commit belastes ikke i den nye native banen; ingen automatisk retry av
betaling er innført. Faktisk native commit-/økonomitest med klient gjenstår.

M1 – oppgraderingssporing (19. september): UpgradeSync beholder epoch/sekvens/
avsender gjennom den begrensede retry-køen og native commit-callback, uten å beholde
meldingsbody. Utløpt venting og køutkast merkes rejected. Native commit merkes
completed bare ved nøyaktig én levende oppgraderingsrot med forventet prefab,
owner, posisjon (10 cm) og rotasjon. Ingen/flere treff gir commit-unverified;
eksisterende nærliggende objekt er ikke automatisk kvittering. Fallback er submitted.
Dette er lokal rotverifikasjon, ikke full undergrafkontroll eller nettverkskvittering.
Betaling/recovery-regler er ikke endret, og ekte mottakertest gjenstår.

M1 startet etter brukerens godkjenning, mens M0-spilltest fortsatt er åpen.
Første steg: MoveSync beholder kommandoidentitet gjennom native commit-callback
og logger decode/apply/armed/retry/rejected/submitted. Fullføring krever samme
levende rotentity med riktig prefab/owner, posisjon (10 cm toleranse), rotasjon
og kjent destinasjonstilknytning. Ubekreftet eller gjenopprettet entity gir
commit-unverified. Nærliggende eksisterende objekt teller ikke som verifisert commit.
Fallback-definisjoner merkes submitted, ikke completed. Dette er lokal observasjon
av roten, ikke verifisering av hele undergrafen eller en kvittering til andre peers.
Oppgraderinger, oppfølging av uverifisert commit og målrettet recovery gjenstår.

M0 – generator-API tilpasset spill 1.6.2f1: NativeDerive sender nå standard
PlacementOverrides før inputDeps, som spillets UpgradeToolSystem gjør. Bindingen
kontrollerer alle 24 parametertyper, JobHandle-retur og UInt32-seedfelt. Alle
parametertypene er kontrollert mot installert Game.dll med Mono.Cecil. Feildiagnosen
skiller manglende generatorsignatur fra manglende seedfelt. Ordinære gameplay-kall
bruker ingen overrides; editorens særinnstillinger er ikke lagt til nettprotokollen.
Faktisk flytting/oppgradering/stamp hos flere spillere er fortsatt ikke verifisert.

M0 – første protokoll-65-singleplayerdata: 214 frame-vinduer (107 loggede minutter)
er sikret fra en rent avsluttet økt. Bekreftet API-avvik: CreateDefinitions har
24 argumenter i installert Game.dll, mens modden forventer 23; PlacementOverrides
mangler i argumentlisten. Dette forklarer generator-fallback-advarselen. Se
[singleplayer-rapporten](tests/M0-SINGLEPLAYER65.md). API-tilpasning og verifisering
i spillet gjenstår, sammen med kontrollert 2-/4-spiller-baseline.

M0 – installasjonsfeil korrigert (18. september): Spillet lastet protokoll 62 fra
backup under brukerdata. Backup er flyttet helt ut av spillområdet; 11 installerte
protokoll-65-filer er hashverifisert. Ny installasjonskontroll og regresjonstest
avviser slike duplikater. Den siste økten inneholder 90 frame-vinduer, men gjelder
gammel versjon og Host uten peers, ikke ønsket singleplayer-baseline. Se
[øktrapporten](tests/M0-SEPTEMBER18-SESSION.md). Ny runtime-versjon må bekreftes etter omstart.

M0 – sammenlignbar frame-innsamling (18. september): FrameProbe kjøres nå også
i en ferdig lastet singleplayer-by når modden er aktiv og sesjonen er offline.
Vinduer merkes singleplayer/host/client; bytte av rolle, world-preload, lasting,
meny og deaktivert mod nullstiller målingen. Eksporten bevarer rollen og lar gamle
umerkede vinduer stå som ukjent. Eksportregresjonene består. Dette er måleverktøy,
ikke utførte baseline-resultater; riktig innsamling i spillet må fortsatt verifiseres.

M0 – eksport av frame-målinger (18. september): `build/export-frame-diagnostics.ps1`
eksporterer kun validerte numeriske frame-vinduer og lokalt remappede run-aliaser.
Histogramintervallene er disjunkte; avrundede verdier merkes eksplisitt. Testene
dekker histogram-sum, ugyldig varighet/tall, privatdata og tom logg. Eksporten er
begrenset til 20 000 vinduer / 128 run-aliaser og rapporterer avvisning/trunkering.
Den lokale loggen fra 15. september ga 357 vinduer uten avvisning/trunkering.
Dette er historiske observasjoner, ikke baseline for protokoll 65. Testen inngår
nå i lokal pakkebygging og CI-konfigurasjonen; GitHub-jobben er ikke kjørt her.

M0 – snapshot-regresjoner ved epoch-skifte (18. september): To nye tester bruker
ekte meldingskodek/sesjon med falsk transport. Delvis gammelt snapshot erstattes av
ny epoch; forsinkede gamle chunks endrer ikke nytt mottak. Etter Resume avvises
flere chunks fra samme snapshot. Abort rydder delvis mottak og tillater neste
gyldige epoch. **70/70 Core-tester består.** Ingen produksjonskode endret i denne
oppfølgingen; testene bekrefter ikke Unity-innlasting eller reell flermaskin-sync.

M0 – kanalavgrenset feilopprydding (18. september): Avvist blob-total og
protokollfeil under mottak nullstiller nå bare fremdriften dersom den tilhører
den berørte kanalen. En regresjon dekker begge avvisningsbanene og etterfølgende
fullføring av den uberørte kanalen. **68/68 Core-tester består.** Dette korrigerer
fremdriftsstatus; ingen endring i timeout, protokollformat eller spill-synkregler.

M0 – opprydding etter stoppet kartmottak (18. september): Timeout-oppryddingen
nullstiller nå fremdrift bare dersom den utløpte kanalen er den som vises. En annen
aktiv kanals fremdrift beholdes. Regresjonen bruker styrt klokke og kontrollerer
60-sekundersgrensen, fjerning av buffer/transfer-ID og slutt-opprydding. **67/67
Core-tester består.** Selve timeout-verdien er uendret; dette er en retting av
fremdriftsstatus, ikke dokumentert forbedret spill-sync eller nettverkslatency.

M0 – sesjonstester for kartmottak (18. september): To nye integrasjonsregresjoner
kjører ekte meldingskodek og MultiplayerSession over falsk transport. De dekker
overflow etter delvis mottak, opprydding av fremdrift/buffer, gyldig retry og uavhengig
eierskap mellom ferdige payloads. Ukjent kanal, overskredet kanaltak, feil transfer-epoch,
ufullstendig avslutning og klientopplasting til host avvises. **66/66 Core-tester
består.** Ingen produksjonskode endret i denne oppfølgingen; ekte TCP/Steam og
Unity-saveinnlasting er fortsatt ikke verifisert av disse testene.

M0 – eierskap ved blob-fullføring (18. september): `Complete()` overleverer nå
den ferdige bufferen uten `ToArray()`-kopi og slipper referansen i reassembleren.
Dette bygger på at bufferkapasiteten er begrenset til annonsert total. Mottaket
kan fullføres kun én gang; videre append/fullføring avvises. Regresjonstesten
kontrollerer referanseidentitet, frigitt referanse, ufullstendig mottak og avvisning
etter fullføring. **64/64 Core-tester består.** Én fullstørrelseskopi er fjernet
fra denne kodebanen; faktisk minnetopp i spillet og NativeArray-feilen er ikke avklart.

M0 – minnekontroll ved blob-mottak (18. september): Mottaksbufferen vokser fortsatt
gradvis, men kapasiteten begrenses nå til annonsert total. Standard MemoryStream-
dobling kunne tidligere reservere mer enn totalen. En regresjon med to hele chunks
og én ekstra byte kontrollerer kapasitet og dataintegritet; ingen stor buffer
allokeres bare på grunnlag av annonsert størrelse. **63/63 Core-tester består.**
`Complete()` kopierer fortsatt data, og gamle vekstbuffere kan vente på GC.
Faktisk prosess-/native-minne under spill-resync er ikke målt; den eldre 10 GiB-
NativeArray-feilen er ikke forklart av denne rettingen.

M0 – binærleserens buffergrenser (18. september): `NetworkReader` validerer
offset/count før bufferområdet opprettes, med overløpssikker kontroll. Tester dekker
negative verdier, områder utenfor bufferen, `int.MaxValue`, gyldig delbuffer og
tom delbuffer. Avviste lesinger konsumerer ingen bytes. **62/62 Core-tester består.**
Dette er defensiv validering, ikke en bekreftet årsak til tidligere minnefeil.

M0 – kontroll av blob-grenser (18. september): En chunk som overskrider annonsert
reststørrelse avvises nå før MemoryStream vokser eller chunk-/tidsstatus endres.
Negative og for store chunk-lengder avvises eksplisitt ved dekoding. To regresjoner
dekker avvisning uten tilstandsendring, gyldig fullføring og ugyldige wire-lengder.
**60/60 Core-tester består.** Gyldig protokollformat er uendret. Dette er ikke en
fastslått rotårsak eller dokumentert løsning på den eldre NativeArray-minnefeilen.

M0 – faktisk lokalt loggfunn (18. september): Den siste tilgjengelige spilløkten
er fra 15. september og protokoll 62, ikke dagens testpakke. Den viser flere
snapshot-resume-hendelser, et tregt framevindu og en NativeArray-allokeringsfeil
på omtrent 10 GiB. Observasjoner, avgrensninger og neste testprioriteringer er
dokumentert i [M0-LOCAL-EVIDENCE.md](tests/M0-LOCAL-EVIDENCE.md). Rotårsak er ikke
fastslått; dette er ikke en fullført baseline eller en bekreftet feil i protokoll 65.

M0 – validering av analyseinput (18. september): Analyseverktøyet avviser feil
JSON-struktur, ugyldige lokale session-aliaser og negative/ikke-heltallige målefelt.
Tall som tekst og boolske verdier konverteres ikke lenger stilltiende til målinger.
Hele input valideres før rapporten skrives. Regresjonstestene bekrefter at eksisterende
rapport beholdes ved avvist input. Dette er analyseverktøy, ikke en endring i spill-sync.

M0 – årsaksoversikt (18. september): Analyseverktøyets `reasonOutcomes` grupperer
siste observerte operasjonsstatus/årsak per session, epoch og avsender. Eksport og
analyse deler en fast liste godkjente årsakskoder. Testene dekker duplikat-retry,
senere completion, private/ukjente årsaker og separate epochs/sessions. Alle
PowerShell-analyse-, eksport- og rapportlesertester består. Ingen spillkode endret;
resync-hendelser og faktisk flermaskinbaseline er fortsatt ikke målt.

M0 – manglende kømålinger (18. september): Global samplet køtopp er nå `null`
når ingen kømåling finnes, i stedet for et misvisende nulltall. Analyseeksporten
teller køsamples og samples uten epoch, med egne kvalitetsvarsler. Testene skiller
manglende data fra faktisk målt tom kø og kontrollerer eldre logger uten epoch.
Eksport-/analysetestene består; ingen spillkode eller installasjon er endret.

M0 – køanalyse per epoch (18. september): `epochQueues` i analyseeksporten skiller
siste observerte kø, køtopp, mottatt/fullført sekvens og eldste ventende arbeid
per session/epoch/avsender. Ukjent epoch blandes ikke inn i et kjent vindu;
manglende målinger blir null, ikke null kø eller null ventetid. PowerShell-testene
dekker blandet rekkefølge, flere sessions, eldre loggformat og tom eksport.
Ingen endring i spillmodulen denne oppfølgingen; faktisk baseline gjenstår.

M0 – epoch-isolering av diagnostikk (18. september): Periodiske køoversikter
merkes og grupperes nå per epoch/spiller. Klientrapporten inkluderer bare
operasjoner som tilhører rapportens oppgitte epoch; eldre observasjoner får ikke
feil epoch-merke. Regresjonen dekker eldre ventende arbeid, ny ferdig operasjon
og en epoch uten observasjoner. **58/58 Core-tester består.** Dette endrer ikke
protokollformatet eller selve replay-/resync-logikken.

M0-oppfølging 18. september 2026: Analyseverktøyet deler nå lokal kø-/apply-timing
per session og epoch. Det varsler når samlede kvantiler blander flere slike vinduer,
og beholder uferdige operasjoner og manglende timing eksplisitt. PowerShell-testen
dekker gjenbruk av sekvens etter epoch-skifte, separate sesjoner, manglende timing
og tom eksport. Ingen ny spillbaseline er målt; M0 står fortsatt åpen.

Oppdatert: 17. september 2026. Prosjekt: CS2MPMod. Ønsket utviklerprofil: Nextarch Studio; Paradox-utgiver: LegacyAngel.

M0 – diagnostikktekst: Lengdebegrensning splitter ikke lenger Unicode-surrogatpar.
Ugyldige enkelt-surrogater erstattes før escaping, slik at formatteringen ikke
sender ugyldig UTF-16 til URI-encoder. Prefab-/feiltekst er fortsatt begrenset til
200 UTF-16-kodeenheter før escaping; linjeskift og feltskilletegn escapes fortsatt.
Regresjonen dekker emoji, avkorting midt i et par og ugyldig tekst. **57/57 Core-tester
består.** Endringen påvirker ikke spillprotokollen eller native byggesynkronisering.

M0-oppfølging – rapportkø ved reconnect: En låst eller utilgjengelig enkeltfil
blokkerer ikke lenger lesbare rapporter til samme host. Filen beholdes og prøves
igjen senere. Regresjonstestene kontrollerer også at full outbox (128 rapporter)
bevarer eksisterende rapporter og tillater oppdatering av eksisterende checkpoint,
og at full host-innboks (200 rapporter) fortsatt kvitterer identiske retries.
Nye rapporter avvises ved kapasitetsgrensen; lagring er ikke garantert ved full disk
eller full kø. **55/55 Core-tester består.** Dette er ikke en flermaskintest;
reproduksjon i spillet og singleplayer/2-/4-spiller-baseline gjenstår i M0.

Rapportforsøk roterer nå mellom ventende rapporter, fortsatt maksimalt ett ordinært
forsøk hvert 10. sekund. En rapport uten kvittering sperrer dermed ikke alle andre.
Forsinkede kvitteringer kan godtas for tidligere sendte rapporter i samme tilkobling;
ingen rapport slettes bare fordi neste rapport forsøkes. Aktivt checkpoint og andre
host-mål holdes utenfor rotasjonen. Ny regresjon dekker rotasjon, omløp og slettet
markørfil. Spilltest av forsinkede kvitteringer gjenstår.

## Implementasjonsstatus – første sync-retting

Andre oppfølging: Bekreftet sync-feil under 90-sekundersgrensen blir nå beholdt i en
enkeltplass-kø og forsøkt når grensen tillater det. Pågående snapshot sperrer nye forsøk;
vellykket snapshot fjerner køen, mens avbrutt/ignorert forespørsel ikke sletter feilen.
Ny sesjon nullstiller både feil og tidsgrense. Fire nye regresjoner dekker dette (34 totalt).
Dette reparerer ikke selve byggekommandoen lokalt og erstatter ikke planlagt målrettet reparasjon.

Oppfølging 17. september: Native byggehandlinger beholdes i den ordnede retry-plassen når
et manglende festepunkt blir vurdert av resync-arbiteren. Hvis punktet finnes senere,
trekkes dette varselet tilbake; native commit-validering avgjør fortsatt om plasseringen lykkes.
Rapporter identifiseres med epoch, sekvens og avsender, slik at ulike bygg ikke deler varsel.
Resync-innboksen konsumerer nå flagg og feilrapport under samme lås.
Testsettet er utvidet til 30 regresjoner, inkludert ekte arbiter-/innbokskode med erstattet
spill-loggsink. Ingen Unity-plassering eller flermaskintest er utført av disse testene.
Protokollen er fortsatt 63. Byggepreview og native commit-kvittering gjenstår.

Første del er implementert 17. september 2026, med protokoll **63**:

- Sekvensbekreftelse sendes til avsenderen uten å anvende egen handling dobbelt; hull fra første kommando blir forespurt.
- Kommandoer og replay er epoch-merket. `Resume` bærer snapshotets sekvensgrunnlag, og gammel ventekø/historikk kasseres ved installasjon.
- Replay har begrensede batcher, retries, eksplisitt svar ved utløpt historikk og snapshot-fallback. Ventekøen har tak på 2048 meldinger / 8 MiB; journalen 4096 meldinger / 16 MiB. Normal sekvens går direkte uten allokering av et køtre-element.
- Full TCP-datakø mister ikke lenger close-eventen; lukkede forbindelser beholder sin kapasitetsplass til hendelsen blir levert. Recovery-kvoten alene sparker ikke lenger ut spilleren eller forgifter påfølgende heartbeat.
- [Core-testprosjektet](tests/CS2MPMod.Core.Tests/Program.cs) har **26 beståtte regresjoner**, inkludert én simulert host og tre klienter, sen join, snapshot/Abort, køgrenser og replay over flere batcher. De to opprinnelige sekvensfeilene feilet før rettingen og består nå.
- [Byggescriptet](build/build-test-package.ps1) kjører testene og lager en separat komplett staging-/ZIP-pakke med SHA-256-manifest, uten å installere eller publisere. En Core-CI-workflow er lagt til; den er ennå ikke kjørt på GitHub.
- Komplett Debug-bygg er verifisert med Unity-etterbehandling, Steam-backend og UI: **0 advarsler, 0 feil**. ZIP-pakken `build/packages/CS2MPMod-protocol63-test.zip` er kontrollert for nødvendige filer og samsvarende SHA-256-hasher. Core-testbygget har tre eksisterende advarsler om eldre API-er i `PortForward.cs` på .NET 10; spillbygget er net48.

Dette avslutter ikke M1 eller resten av roadmapen: faktisk native bygging hos alle spillere, commit-kvitteringer, konfliktregler, målrettet reparasjon og byggepreview krever videre arbeid og spilltest. Funn-tabellen nedenfor dokumenterer tilstanden **før denne rettingen**; ovenstående er aktuell fremdrift. Ingen ytelsesgevinst i spillet er målt ennå.

## Målet

Det hosten eller en annen spiller bygger, flytter, oppgraderer eller river, skal bli riktig hos **alle tilkoblede spillere**, uten at de må laste inn byen på nytt. Nye spillere skal få riktig nåtilstand og deretter alle nye endringer. Spillere skal også kunne se hvor andre planlegger å plassere et bygg, gjennom en lett forhåndsvisning.

Spilltesten som er rapportert, viser dårlig synkronisering av bygg fra host til andre spillere. Dette er hovedprioriteten. At en nettverksmelding er sendt eller mottatt, er ikke tilstrekkelig: resultatet må være ferdig opprettet i mottakerens spillverden.

«Sanntid» betyr her lav, målt forsinkelse og en felles autoritativ bytilstand. Identisk rendering i samme millisekund på ulike maskiner er ikke et realistisk ferdigkriterium. Synkronisering av trafikk og innbyggere må også avklares og implementeres særskilt dersom «alle ser alt» skal inkludere hver bevegelig enhet.

## Grunnlaget for analysen

Den opprinnelige analysen dekker nettverk/protokoll/session, bygg- og veipipelinen, tilstandskanaler, resync, overføring av by, diagnoseverktøy, UI, prosjektfiler og byggeoppsett. Ved analysestart inneholdt arbeidskopien 317 C#-filer uten genererte `bin`/`obj`-filer. Da ble ingen automatiske testfiler eller testprosjekter funnet; eneste GitHub-workflow bygde/publiserte dokumentasjon. Nye tester og endringer fremgår av implementasjonsstatus over.

Statusord brukt nedenfor:

- **Bekreftet i kode:** Oppførselen følger direkte av lest implementasjon.
- **Reprodusert isolert:** Faktiske Core-kilder ble kompilert i minnet og kjørt uten spillet, sockets eller distribusjon. Dette er ikke en Unity/net48-spilltest.
- **Må undersøkes i spill:** Krever logger fra begge sider, en konkret save og gjentakbare handlinger.
- **Foreslått forbedring:** Fremtidig arbeid; ikke en påstand om at funksjonen allerede finnes.

Under den opprinnelige dokumentasjonsanalysen ble det ikke kjørt full mod-bygging, installasjon, publisering eller flermaskintest, og kode ble ikke endret. Den påfølgende implementasjonen og staging-byggingen er dokumentert øverst. Det ordinære bygget har fortsatt deployment som sideeffekt; det nye testpakkescriptet styrer denne til en separat mappe i prosjektet.

**Viktig korrigering til tidligere feilsøking:** `closed by peer: disconnected by host` beviser ikke en timeout. Timeout, protokollavvisning, rategrense og mislykket world-sync kan ende med samme generiske transporttekst. 30 sekunders timeout og heartbeat hvert sekund er bekreftet i koden, men årsaken til den rapporterte utkastelsen er ikke fastslått uten relevante logger.

## Det som allerede er nyttig

Prosjektet har et godt grunnlag å bygge videre på: et spilluavhengig Core-lag, et felles transportgrensesnitt, TCP og separat Steam-backend, challenge-response-autentisering, meldingsgrenser, eksplisitte world-sync-faser, noe operasjonsdeduplisering, vertsstyring av flere simuleringssystemer og mange tilstandskanaler. `ResyncArbiter`, `FlightRecorder`, `SyncProfiler`, romlige oppslag og eksisterende budsjetter per frame bør videreutvikles.

Det er derfor ikke anbefalt å skrive om hele modden eller skifte transport først. Flere alvorlige problemer ligger over transportlaget, i rekkefølge, gjenoppretting og ferdigstilling av handlinger.

## Konkrete funn

| ID | Prioritet | Funn og konsekvens | Grunnlag |
| --- | --- | --- | --- |
| F01 | P0 | Klientkoden krever neste sammenhengende sekvensnummer, men hostens `HandleCommand` videresender med `BroadcastToAll(command, from)`. Avsenderen får ikke sin egen plass i den globale sekvensen. Senere handlinger kan bli stående bak et kunstig hull. | Bekreftet: [Messaging.cs][messaging], [Notify.cs][notify]. |
| F02 | P0 | Når første mottatte sekvens er større enn 1, settes meldingen i ventekø, men replay blir ikke forespurt fordi `_lastReceivedCommandSequence > 0` er et vilkår. En klient som kommer inn i en pågående strøm, kan derfor stoppe helt. | Reprodusert isolert. [Messaging.cs][messaging]. |
| F03 | P0 | World-sync har epoch for kontroll/blob, men kommandoer og replay-forespørsler mangler epoch. Snapshot-kontrollen mangler sekvensgrunnlag. `ResetCommandSequenceState` brukes ved session-livssyklus, ikke ved snapshot-installasjon. Nye og eksisterende klienter mangler en entydig grense mellom snapshot og nye kommandoer. | Bekreftet: [Session][session], [WorldSync][worldsync], [kommandomelding][command]. |
| F04 | P0 | `_pendingCommands` mangler grense for antall/bytes/alder. Replay-journalen er begrenset til 4096 meldinger, ikke bytes. Replay har ikke eksplisitt svar ved manglende historikk eller en tidsstyrt retry-/fullføringsmekanisme. | Bekreftet; 5001 ventende meldinger reprodusert. [Session][session], [Messaging.cs][messaging]. |
| F05 | P0 | Mottatt sekvens markeres før `NotifyCommand`, mens realisering skjer senere i separate systemkøer. Den eldre byggebanen kan logge og hoppe over ukjent prefab eller feil body. Det finnes ikke en felles ende-til-ende-kvittering som beviser at bygget finnes hos mottakeren. | Bekreftet: [Messaging.cs][messaging], [CommandObserver][observer], [BuildSync/Realize][buildrealize], [SyncRealizeSystem][realize]. Hvilken bane som feilet i spilltesten er uavklart. |
| F06 | P1 | Full world-resync utløses periodisk, normalt hvert 15. minutt, også uten målt avvik. `0`/`off` slår den ikke av. Ett join eller én recovery samler alle handshakede deltakere i en ny global barrier. | Bekreftet: [WorldResyncSystem][resyncsystem], [MultiplayerService][service], [Setting][setting]. |
| F07 | P0 | Ved full TCP-eventkø fjerner `HandleClosed` forbindelsen, men kan droppe `Disconnected`. Sessionen kan sitte igjen med en peer den aldri får ryddet ut gjennom forventet callback. | Reprodusert at close-event forsvinner ved 10000 køelementer. [TcpServerTransport][tcpserver]. |
| F08 | P0 | Alle meldinger fra samme `Update` får samme behandlingstidspunkt. Rategrenser måles med dette tidspunktet. Oppsamlet trafikk etter en pause kan tolkes som én burst. Tredje resync-forespørsel innen ett minutt gir ratebrudd, som hosten håndterer med `Punt`. | Kode og isolert limiter-sjekk. [Session][session], [Transport][transport], [PeerRateLimiter][limiter]. |
| F09 | P1 | Heartbeat/peer-timeout drives av session-pumpen. Steam-outbox og mottak pumpes i `Poll`. 30 sekunder løser ikke i seg selv blokkert spilltråd, køventing eller ulike fasefrister. World-sync har også 20 s quiescence og 180 s load; save-fasen har ingen egen deadline i `PumpSave`. | Bekreftet: [Session][session], [SteamRelayIo][relayio], [WorldResyncSystem][resyncsystem]. |
| F10 | P1 | Uavklart feil holdes normalt 12 s før resync. Automatisk recovery innen 90 s kan deretter bli droppet; loggen sier at endringen blir usynkronisert og foreslår `/sync`. Dette begrenser reload-stormer, men sikrer ikke at feilen faktisk blir rettet. | Bekreftet: [ResyncArbiter][arbiter], [MultiplayerService][service]. |
| F11 | P1 | Hele snapshotet chunkes og kølegges i én løkke per mål. Fremdrift bruker samlet sendebacklog. Dette gir kandidater til allokeringstopper, lang kontrollmeldingsventing og dårlig oversikt over den enkelte spiller. | Bekreftet mekanisme; faktisk kostnad må måles. [Blob.cs][blob], [WorldResyncSystem][resyncsystem]. |
| F12 | P1 | Flere simuleringsdomener er vertsstyrte, men byen kjører fortsatt lokal simulering på klientene. Kalenderkorreksjon er ikke deterministisk lockstep. Befolkning i HUD omtales eksplisitt som lokalt resultat. | [LocalAuthorityHold][authority], [CityStateSyncSystem][citystate], [GameClockStateChannel][gameclock]. Full visuell likhet er ikke dokumentert. |
| F13 | P1 | Bygg og deployment er koblet sammen. Steam bruker filreferanse til hoved-DLL. UI-verktøyet `tools/css-presence.js` ignoreres av Git-regelen `tools/`; `tests/` ignoreres også. Et rent checkout kan derfor mangle nødvendige filer. | Bekreftet med `git check-ignore`. [Prosjekt][project], [Steam-prosjekt][steamproject], [.gitignore](.gitignore). |
| F14 | P1 | Alle fire skjermbildestiene i publiseringsfilen peker på manglende omdøpte filer. UI-versjon er `1.0.0`, publiseringsversjon `0.1.6.1h1`, og `ModsCheck` har fortsatt hardkodet egen plattform-ID `150432`, mens publiserings-ID er tom. | Filer/stier kontrollert. [PublishConfiguration][publish], [UI-metadata][uimetadata], [ModsCheck][modscheck]. |
| F15 | P1 | Dagens spillerstate beskriver kamera/fokus. Den inneholder ikke valgt bygg, plasseringstilstand eller byggeforhåndsvisning. Markørsystemet er et nyttig utgangspunkt for overlay-rendering, men er ikke en delt byggepreview. | Bekreftet: [PlayerStateMessage][playerstate], [RemotePlayerMarkerSystem][markers]. |

### Isolerte kontroller utført 17. september

Kontrollene under kjørte den eksisterende Core-koden med syntetiske innganger. Sekvenstestene kalte klientens `HandleCommand` via refleksjon; «dispatched» betyr sendt videre til observatører, ikke realisert i Unity.

| Inngang | Resultat |
| --- | --- |
| Sekvens `1, 2, 3` | Dispatched til 3; tom ventekø. |
| Sekvens `2, 3` på ny klient | Dispatched fortsatt 0; to ventende; ingen registrert replay-range. |
| Sekvens `500, 501` på ny klient | Samme stopp. Underbygger manglende grunnlag for late join. |
| Sekvens `1, 3, 2` | Dispatched til 3 og tom kø. Enkel innhenting fungerer når starten allerede er kjent. |
| Sekvens `2..5002` | 5001 ventende; dispatched fortsatt 0. |
| Close på full TCP-eventkø | 10000 dataevents tappet ut; null disconnect-events. |
| Resync-forespørsler ved 10, 16 og 22 sekunder | Den tredje gir `resyncs/min (3)`. |
| Kommandoer med samme behandlingstidspunkt | Nummer 1501 gir `commands/sec (1501)`. Dette beviser terskelen, ikke at konkret spillertrafikk overskred den. |

## Arkitektur vi skal arbeide mot

Host bestemmer godkjent bytilstand. Alle deltakere får hver relevant autoritativ handling, inkludert den opprinnelige avsenderen. Lokalt allerede utførte handlinger skal bekreftes og eventuelt korrigeres, ikke utføres dobbelt.

Foreslått flyt:

1. Spilleren gjør en handling. Capture knytter den til en stabil `OperationId` og avhengigheter.
2. Host validerer og plasserer handlingen i autoritativ rekkefølge. Mottak, godkjenning og faktisk native commit må være forskjellige tilstander.
3. Host sender autoritativt resultat/commit til alle relevante klienter. Avsenderen kvitterer også, selv om lokal prediksjon allerede viser resultatet.
4. Mottaker holder avhengige handlinger til deres forutsetninger er ferdige og realiserer gjennom spilltråden.
5. Mottaker sender `Applied` først når riktig objekt/graf er verifisert. `Received` alene må ikke brukes som dette beviset.
6. Manglende resultat gir bounded retry, operasjonsreplay eller målrettet reparasjon. Full world-resync er siste utvei.

Felles kontrakt må omfatte `SessionId`, verdenens `Epoch`, host-sekvens, `OperationId`, objektidentitet og relevante revisjoner. Et snapshot må angi hvilke ferdigstilte operasjoner og revisjoner det inkluderer. Snapshot-grunnlaget kan ikke uten videre være «sist mottatte melding» hvis native arbeid fortsatt pågår.

To strømtyper skal behandles forskjellig:

- **Byendringer:** pålitelig levering, autorisasjon, deduplisering, kjent avhengighetsrekkefølge og kvittert ferdigstilling. Ingen endring skal forsvinne fordi spilleren ser et annet sted.
- **Tilstedeværelse og byggepreview:** bare siste tilstand er viktig. Mellomliggende oppdateringer kan erstattes før sending. Dette må aldri blokkere byggekommandoer eller utløse world-resync.

Ikke anta at TCP mister enkeltmeldinger under normal bruk: transporten er allerede pålitelig og ordnet per forbindelse. Replay er nødvendig for applikasjonsfeil, gjeninnhenting og reconnect; feil i avsender-/epoch-logikken må rettes først.

## Mål og måling

Tallene er foreslåtte akseptansemål, ikke målte resultater eller garantier. Baseline fra M0 kan begrunne justering. Alle sammenligninger må oppgi by, maskinvare, grafikkoppsett, spill-/modversjon, transport, RTT og spillerantall.

| Område | Første akseptansemål | Hvordan måle |
| --- | --- | --- |
| Korrekt bygging | Ingen permanent manglende eller dupliserte resultat i en definert serie på minst 1000 blandede handlinger med 2 og 4 spillere. | Verifiser objekter, eiergraf, veiforbindelser og revisjon på alle maskiner. |
| Byggesynlighet | For enkle plasseringer: p95 ≤ 500 ms fra autoritativ commit til synlig/verifisert resultat hos hver mottaker, ved RTT ≤ 100 ms og spill ≥ 30 FPS. | Spor én operasjon gjennom capture, commit, kø og native apply. Store transaksjoner får eget budsjett. |
| Full resync | 0 unødvendige automatiske full-resyncs i en 2-timers, feilfri akseptansetest etter ferdig M2/M3. | Tell initial join, manuell resync, målrettet reparasjon og full recovery separat. |
| Recovery | Alle injiserte strømfeil ender i verifisert innhenting eller eksplisitt begrunnet full recovery; ingen ubegrenset venting. | Feilinjeksjon og maksimale frister per recovery-fase. |
| Frakobling | Ingen falske utkastelser i de definerte pause-/save-/load-scenarioene; reell død forbindelse avsluttes innen dokumentert frist. | Separate scenarier for aktiv nettverkstråd, stanset pump og brutt forbindelse. |
| Ytelse | Første budsjett: p95 mod-arbeid ≤ 2 ms/frame i referansebyen under vanlig 4-spillerbygging; mål p99 og topp separat. | Eksklusiv profiling av egne systemer, ikke summen av nestede scopes. |
| Minne | Alle nettverks-/replaykøer har antalls- og bytegrenser; ingen vedvarende vekst gjennom 2 timer. | Køhøyvann, allokeringer, GC, minne og entiteter etter gjentatte operasjoner. |
| Byggepreview | Start med 10 Hz ved bevegelse, adaptivt 5–10 Hz; p95 visningsalder ≤ 300 ms i samme testmiljø. | Mål også ekstra nettverk og renderkostnad; ingen trafikktopper når verktøyet står stille. |

Unngå å trekke fra usynkroniserte klokker på ulike PC-er. Bruk korrelerte hendelser med målt klokkeavvik eller konservative rundtursmålinger. «Host meldte sendt» er ikke tidspunktet bygget faktisk ble synlig.

## M0 – reproduksjon, måling og et pålitelig utviklingsgrunnlag

**Arbeidsgrense oppdatert etter brukerens godkjenning:** M1 kan utvikles mens
M0 venter på koordinert flerspillertest. M0 er ikke markert fullført; M1-endringer
må fortsatt verifiseres med ekte mottakere før de regnes som ferdige.
Siste leveranse bruker protokoll **65**: en felles `OperationDiagnosticRecord`
(schema 1) brukes av capture/apply-loggene med eksplisitte ukjente felt og escaped
tekst. Numeriske klientrapporter lagres lokalt og forsøkes sendt ved reconnect
til samme konfigurerte mål, med sletting først etter varig mottak hos host.
Dette er en avgrenset rapport, ikke automatisk overføring av rå logger/save.
Ny by skal brukes i spilltesten; save og koordinerte flermaskinresultater foreligger
fortsatt ikke. Ingen reproduksjon eller baseline er krysset av på grunnlag av disse testene.
Rapporttesten dekker nå også avsendere som bare har køoppsummeringer. De vises med
siste/høyeste samplede kø og eldste ventende sekvens; manglende målinger er null,
ikke en oppdiktet tom kø. Dette endrer kun M0-verktøyene, ikke modpakken.
M0-rapporten er nå også testet med separate timing-/statusoversikter per lokal
sesjon og kommandoavsender. Avsender-ID må ikke forveksles med mottakermaskin;
rapportene beviser ikke nettverkslatency eller fjernklientens ytelse.
Siste M0-oppfølging: diagnostiske koblinger for like sammenfalte ruteopprettelser,
46 C#-regresjoner og testet lokal kvantilrapport fra eksport. Koblingene er bounded
og endrer ikke gameplay/recovery. Rapporten teller ukjente/ufullførte målinger separat;
den erstatter ikke flermaskintest, frame-/trafikkmålinger eller resterende dekning.
Ikke kryss av spillreproduksjon eller baseline på grunnlag av Core-tester.
Ny M0-instrumentering skiller køtid fra native apply-tid, bevarer første
ferdigstilling ved duplikater og sporer også vanlig og sammensatt veicommit.
40 automatiske regresjoner og eksporttesten passerer etter denne oppfølgingen.
Neste M0-oppfølging utvider rutediagnostikken: kommandoidentitet følger create/update
gjennom native commit og replay uten å beholde meldingsbody. Opprettelse blir først
ferdig når ruteidentitet og metadata er satt; update-commit uten bekreftet sluttstate
logges som `commit-unverified`, ikke ferdig. Eksporten bevarer disse statusene og et
fast sett ikke-private årsakskoder. Testsettet er nå 42 regresjoner; testene verifiserer
diagnostikkmodellen, ikke rutebygging i Unity. Recovery-/spillreglene er uendret.
Spilltesten krever koordinering med testspillerne; ingen testresultater fra disse
maskinene er mottatt i denne arbeidsøkten.

Status: **pågår, ikke ferdig**. Verifikasjon og spilltestmatrise finnes i
`tests/M0-VALIDATION.md`. Staging/preflight og første avgrensede operasjonsdiagnostikk
er implementert; all-domain native commit-dekning, korrelert flermaskinreproduksjon
og målte baseline-resultater mangler. Lokal flight-logg fra 15. september er ikke
tilstrekkelig alene. Ingen syntetiske målinger er brukt som spilltestresultater.
Lokalt verifisert: 38/38 automatiske C#-tester, privatdata-eksporttest og komplett
staging-bygg av mod/Steam/UI i tidligere oppfølginger. Nyeste pakke er `CS2MPMod-protocol65-test.zip`.

Protokoll **64** innførte API-tilpasning fra fjernede
`Household.m_SalaryLastDay` til nåværende `m_Income` i capture, hash og apply.
Dette er en semantisk protokollendring; alle deltakere må oppgradere samtidig.

Prioritet: P0. Gjennomføres først og parallelt med de første feilrettingene.

- [x] Opprett et versjonert Core-testprosjekt med falsk transport og kontrollerbar monoton klokke. Det skal kunne kjøres uten spillinstallasjon. Ta inn de isolerte reproduksjonene over som regresjonstester.
- [x] Rett ignore-reglene slik at testkilder og nødvendige UI-verktøy følger prosjektet. Ignorer faktisk genererte cacher, lokale deploy-filer og nye UI-buildstier.
- [x] Lag en byggesti som kun bygger/tester til en lokal staging-mappe. Ha installasjon og publisering som separate eksplisitte handlinger.
- [x] Valider SDK, game-managed-sti, Mod.props/targets, Node, låsefil og UI-verktøy før byggestart. Rett null-/tom-path-håndtering og forskjellen mellom process- og user-miljø i setup/prosjekter.
- [x] Innfør en liten felles diagnostikkontrakt: session, epoch, operation, spiller, sekvens, prefab, capture-resultat, køtid, apply-resultat og recovery-årsak. Schema 1 bruker eksplisitt ukjent når et felt ikke er observert; dette er ikke full instrumentering av alle domener.
- [ ] Bevar og utvid `FlightRecorder`/`SyncLog`; legg til oversikt over siste mottatte, siste ferdig anvendte og eldste ventende operasjon per peer. Eksport skal sladde private stier og nettverks-/spilleridentifikatorer.
- [ ] Reproduser rapportert host → klient-problem med én host og én klient, deretter host og to klienter. Test både før og etter første join-resync og etter at en tredje spiller kommer inn.
- [ ] Ta baseline med samme save i singleplayer, 2 spillere og 4 spillere: latency, framekostnad, køer, datamengde og resync-årsaker.

**Ferdig når:** Feilen har en gjentakbar test eller en konkret loggkjede som viser hvor capture → sending → mottak → realisering stopper. Core og UI kan valideres fra rent checkout uten å endre installert mod.

## M1 – alt som bygges skal bli riktig hos alle

Prioritet: P0. Avhenger av M0-testgrunnlaget. Dette skal være første funksjonelle leveranse.

- [ ] Rett F01/F02: Alle deltakere må få sin plass i sekvensen, også avsenderen. Innfør et eksplisitt startpunkt; ikke gjett at første melding betyr at alt før den er ferdig.
- [ ] Test host → alle klienter, klient A → host + B/C, samtidig bygging og at avsenderens lokale objekt ikke opprettes eller belastes dobbelt.
- [ ] Definer epoch/snapshot-kontrakten i F03. Innfør sekvensgrunnlag for late join og resync, og avvis gamle pakker/replay fra andre epoker. Protokollendringer krever nytt protokollnummer og samtidig oppgradering hos alle.
- [ ] Gjør replay komplett: tydelige svar for full range, delvis historikk og utløpt historikk; tidsstyrt retry/backoff; begrensning per peer; bytebudsjett i journal og ventekø. Ikke vent på en ny live-kommando for å fortsette et uferdig replay.
- [ ] Skill mottaksbekreftelse fra native ferdigstilling. Et sammenhengende `AppliedThrough` må ikke passere en eldre uferdig operasjon.
- [ ] Spor alle byggebaner: vanlige bygg, serviceutvidelser, flytting, oppgradering, rebuild, sammensatte asset-stamps, veier og soning. Kontroller capture-rekkefølgen rundt `ToolOutputSystem`, `ObjectToolApplyCaptureSystem` og faktisk commit.
- [ ] Gjør stille «dropp/skip» til et konkret feilresultat med operasjons-ID. Ukjent prefab, feil decode eller mislykket realisering må gi synlig status og en kontrollert recovery-bane.
- [ ] Innfør stabil host-tildelt objektidentitet og generasjon for bygg/veier gradvis. Knytt lokale ECS-entities til den, bevar mapping i snapshot, og bruk tombstones for slettede objekter. Prefab + posisjon alene er ikke entydig ved overlapping, flytting og riv/bygg på samme sted.
- [ ] Gjør avhengigheter mellom veier, terreng, bygg, soner og ruter eksplisitte. Behold effektive systemkøer, men la ikke en senere rivning overta før et avhengig tidligere bygg er ferdig. Atomiske eier-/veigrafer må fullføres eller feile samlet.
- [ ] Definer konfliktregelen når to spillere endrer samme objekt eller felt. Hostens validerte revisjon avgjør; klientprediksjon må kunne bekreftes, avvises og korrigeres.
- [ ] Bevar deduplisering gjennom retry/replay og tilstrekkelig journallevetid. Operasjoner med økonomisk effekt må ikke belastes to ganger.

**Ferdig når:** Den blandede 1000-handlingstesten består med 2 og 4 spillere. Kontrollene omfatter faktisk sluttresultat, ikke bare identiske meldingssekvenser. Sen join, resync og gjentatt levering lager verken hull, spøkelsesbygg eller doble bygg.

## M2 – gjenoppretting og forbindelser uten unødvendige avbrudd

Prioritet: P0 for F07/F08, ellers P1. Start etter M1s sekvenskontrakt; transport- og limiterrettinger kan gå parallelt.

- [ ] Sørg for at close/fault/epoch-kontroll ikke forsvinner i full datakø. Bruk bounded, deduplisert livssyklustilstand eller reservert kontrollkapasitet, og garanter peer-opprydding selv om transporten allerede er borte.
- [ ] Gi alle disconnect-baner maskinlesbar årsak: heartbeat, handshake, rategrense, protokollfeil, kick, save-/load-feil og snapshot-frist. Vis relevant tekst og correlation-ID i UI; lever årsaken før lukking der det er mulig.
- [ ] Gjør ratebegrensning burst-tolerant og rettferdig per peer. Skill transportens ankomstinformasjon fra spilltrådens behandlingstid. Steam-pumping må også håndtere normal oppsamling uten å klassifisere hele køen som ett ondsinnet sekund.
- [ ] La gjentatte legitime recovery-forespørsler gi «allerede pågår»/«prøv etter» og samles, fremfor automatisk utkastelse ved forespørsel nummer tre. Behold egne, reelle misbruksgrenser.
- [ ] Samordne heartbeat, handshake, quiescence, save, transfer og load med fase/fremdrift, jitter og harde maksimumsfrister. Legg til save-deadline med sikker håndtering av en sent fullført save-task.
- [ ] Mål hvor lenge lokal pump har stått. Ved behov flyttes spilluavhengig I/O/liveness til en egnet worker; Steam-trådkrav må verifiseres først. Unity/ECS og native byggehandlinger forblir på riktig spilltråd.
- [ ] Innfør recovery-trapp: retry av avhengighet → operasjonsreplay → objekt-/område-/kanalreparasjon → full snapshot som siste utvei. Hvert trinn må ha resultat, bytebudsjett og timeout.
- [ ] Behold «må repareres»-tilstand under cooldown. Fullført cooldown skal fortsette arbeidet; ikke la en logglinje og `/sync` bli eneste vei til korrekt by.
- [ ] Bruk hashes/revisjoner per autoritativt område eller kanal på samme logiske tidspunkt. Unngå rå ECS-ID-er og forventet lokale/flytende data i sammenligningen. Bekreft avvik før reparasjon.
- [ ] Erstatt periodisk full resync med kontroll av faktisk konsistens når denne er validert. Innfør tydelige innstillinger for kontroll, manuell recovery og eventuell midlertidig sikkerhets-resync; `0`/«av» må ha dokumentert mening.
- [ ] Legg til begrenset reconnect for samme autentiserte deltaker når snapshot/epoch og journal fortsatt passer. Utgått historikk gir tydelig ny synkronisering. En klient uten host-kontakt kan ikke fortsette å endre en separat autoritativ by.
- [ ] Gjør late join og reparasjon målrettet mot den berørte klienten når snapshot + innhenting er konsistent. Verifiser at eksisterende klienter kan spille videre; full global pause beholdes som fallback til dette er bevist trygt.

**Ferdig når:** To timers normal bygging gir ingen unødvendig full-resync. Injiserte feil blir reparert eller avvist med konkret årsak innen en grense. Ingen ventende reparasjon forsvinner, ingen peer blir hengende etter disconnect, og én treg spiller ødelegger ikke de andres session.

## M3 – optimalisert replisering og overføring

Prioritet: P1. Bygg på målingene fra M0 og korrektheten fra M1/M2.

- [ ] Del budsjett mellom kontroll/liveness, autoritative kommandoer, tilstandsoppdateringer, previews og store blobs. Bevar nødvendige barrierer: prioritering må aldri la `Resume` komme før data den avhenger av.
- [ ] Sett antalls-, byte- og tidsbudsjett per peer og per frame for polling, dispatch og apply. Kontrollert tilbakestrykk skal erstatte ubegrenset drain; recovery må håndtere når kapasiteten faktisk ikke er tilstrekkelig.
- [ ] Strøm snapshots gjennom et begrenset vindu fremfor å kølegge hele filen for alle spillere samtidig. Vis sendte, mottatte og lastede bytes separat per mottaker; legg til transfer-ID, integritetskontroll og sikker retry.
- [ ] Mål komprimering før valg. Save-formatet kan allerede være komprimert; ekstra komprimering må forsvares av båndbreddegevinst mot CPU og ventetid. Håndhev størrelse også etter dekomprimering.
- [ ] Send endrede felt/objekter og bruk revisjonert delta med kjent baseline der det lønner seg. Behold full kanal-/områdesnapshot som reparasjonsmulighet.
- [ ] Bruk nyeste-verdi-kø for uavhengig absolutt tilstand og preview. Ikke slå sammen ordnede occupancy-sider, kjøp/salg, bygg/riv eller andre handlinger med sideeffekter som om de var markørposisjoner.
- [ ] Gi raske redigerbare felt en egen responsbane. Dagens 250 ms edit-scan, normalt 1 s snapshot og opptil 5 s venting på egne felt krever revisjon/kvittering for god konflikt- og responsopplevelse.
- [ ] Profilér allokeringer, `ToEntityArray`, fullby-søk, serialisering, låser, job-completion og logg-flush. Gjenbruk buffere/romlige indekser der måling viser gevinst; kontroller eierskap og livstid før pooling.
- [ ] Avklar profilerens total og scope-grenser: `SyncProfiler` tillater nesting, men `Report` summerer scope-tider. Mål inkluderende og eksklusiv tid tydelig; nye målepunkter inne i `Realize` må ikke gi dobbelttelling. Ikke anta at separate `OnUpdate`-målinger er nestet bare fordi systemene samarbeider.
- [ ] Test fair scheduling og båndbredde for 2 og 4 spillere før 8. Logg faktisk trafikk per meldingsklasse; ikke bare total hastighet.

**Ferdig når:** Korrekthetstestene fortsatt består og baseline viser forbedring mot de avtalte latency-, frame- og minnebudsjettene. Ingen optimalisering kan godkjennes bare fordi den senker antall resyncs ved å skjule avvik.

## M4 – vis hva andre planlegger å bygge

Prioritet: P1, eksplisitt ønsket funksjon. Kan utvikles etter stabil M1, med ytelsesgrenser fra M0/M3. En 3D-forhåndsvisning er en plausibel retning, men den konkrete renderintegrasjonen må bevises i spillet.

- [ ] Lag først en liten prototype som leser aktivt byggevalg og viser et farget fotavtrykk/omriss hos en annen spiller. Verifiser at det ikke opprettes spillobjekter, trekkes penger eller påvirkes pathfinding.
- [ ] Innfør en egen `BuildPreview`-melding med spilleridentitet fra host, epoch, preview-ID/revisjon, prefab-ID, posisjon, rotasjon, høyde, verktøytype og start/oppdater/slutt. Avsenderens plasseringsgyldighet er informasjon, ikke autoritativ byggetillatelse.
- [ ] Host videresender previews til andre spillere. Bruk én nyeste preview per spiller/verktøy, lav prioritert trafikk og grense for både størrelse og frekvens.
- [ ] Oppdater ved meningsfull bevegelse/rotasjon/prefabbytte, normalt 5–10 Hz; interpoler visuelt. En stående preview kan få en sjelden keepalive. Ikke send hele Temp-/eiergrafen hver frame.
- [ ] Vis spillerens navn/farge, gjennomsiktig omriss eller ghost og tydelig forskjell mellom planlagt, ugyldig og bekreftet plassering. Legg til innstilling for å skjule previews.
- [ ] Bruk separat renderer som ikke deler den lokale spillerens muterende `ApplyTool`-pipeline. Undersøk gjenbruk av eksisterende overlay-system og cache/pool renderressurser.
- [ ] Rydd ved cancel, verktøybytte, commit, disconnect, world-load og epoch-bytte. Sett TTL, og avvis forsinkede oppdateringer som ellers ville gjenopplive en avsluttet preview.
- [ ] Knytt commit til preview-/operasjons-ID slik at planen forsvinner når den blir et faktisk bygg. Forsinket preview må ikke fjerne eller duplisere et ferdig bygg.
- [ ] Fase 1: enkeltbygg og flytting med omriss; fase 2: korrekt 3D-ghost der API-et tillater det; fase 3: veilinjer, soningsareal og terrengpensel med kompakte geometribeskrivelser.
- [ ] Culling/interessefiltrering kan brukes for kostbar preview-rendering og eventuelt preview-trafikk. Autoritative byendringer skal fortsatt nå alle og være korrekte utenfor kameraet.

**Ferdig når:** Alle andre ser planlagt bygg og orientering, og preview forsvinner korrekt på avbrudd/commit. Fire spillere kan bevege previews samtidig uten resync, varige ekstra entiteter eller merkbar forverring av bygge-latency. Sett eget målt CPU-/GPU-/båndbreddebudsjett etter prototypen.

## M5 – fullfør autoritet og funksjonsdekning

Prioritet: P2. Nødvendig for en bredere påstand om at alle ser samme by, utover pålitelig bygging.

- [ ] Lag en funksjonsmatrise med capture, validering, host-autoritet, apply, identitet, avhengigheter, reparasjon og automatiske/manuelle tester for hver type endring.
- [ ] Dekk bygninger/oppgraderinger/subobjekter, vei/bro/tunnel/ledninger, terreng, vannrelaterte verktøy, soning, distrikter/områder, ruter/stopp, policies, navn, farger, trær og katastrofer. Registrert command-ID er ikke bevis for komplett støtte.
- [ ] Kartlegg alle simuleringssystemer som fortsatt kan endre autoritativt innhold på klienten. Viderefør eksisterende `LocalAuthorityHold` med testet aktivering og gjenoppretting ved disconnect/world-bytte.
- [ ] Definer felles økonomi-, befolknings- og statistikkgrunnlag. Fjern konkurrerende lokale skrivere gradvis; test at HUD og detaljpaneler viser samme logiske revisjon.
- [ ] Avklar trafikk-/innbyggernivå: full likhet krever host-styrt lifecycle og relevante bevegelige tilstander med interpolasjon. Del dette i en målt prototype før generell aktivering; kamera-/spillerposisjoner løser ikke dette.
- [ ] Valider prefab-/DLC-/assets-fingeravtrykk og schema før join. Samme tekstlige modversjon er ikke tilstrekkelig når lokale builds kan ha ulik kode/UI/Steam-backend.
- [ ] Innfør mods-kompatibilitet gradvis med testet kontrakt/allowlist. Ikke fjern dagens beskyttelse mot andre gameplay-mods uten en måte å verifisere deres påvirkning.
- [ ] Undersøk risiko i `WorldRepairSystem`: testen må bevise at automatisk rydding kun berører dokumentert ugyldige legacy-enheter, og hva som skjer i singleplayer når modden er aktiv.

**Ferdig når:** Hver støttet funksjon har dokumentert og testet autoritet. Funksjoner som fortsatt bare ligner visuelt eller simuleres ulikt, er tydelig merket som begrensninger.

## M6 – testbar distribusjon, brukeropplevelse og vedlikehold

Prioritet: P1 for trygg testpakke; øvrig produktarbeid P2. Bygg-/metadataarbeid kan gå parallelt med M1.

- [ ] Lag én versjonskilde som genererer DLL-/UI-/Steam-/pakkeversjon og separat protokoll-/schema-versjon. Inkluder build-ID og hashes i manifest og diagnostikk.
- [ ] Bygg hele pakken i staging før installasjon. Verifiser postprocessor, Steam-backend, JS/CSS og manifest; installer samlet med backup/rollback. Oppdag låste filer og rapporter dem tydelig uten å stoppe uvedkommende prosesser.
- [ ] Test ren restore/build både Debug og Release og UI-only endringer. Erstatt skjult avhengighet av eksisterende DLL/`NeedBuild` med en dokumentert, ikke-sirkulær byggeorden.
- [ ] Samle identitet: `CS2MPMod` som teknisk navn, avtalt visningsnavn, Nextarch Studio som utviklerprofil og LegacyAngel som utgiver. Bevar opprinnelig attribution og verifiser produktmetadata separat fra filnavn.
- [ ] Rett manglende skjermbildereferanser, gammel egen plattform-ID, README-stier og hjelpelinker. Verifiser faktiske URL-er før endring; ikke oppfinn repository-/Paradox-adresser ved tekstlig navnebytte.
- [ ] Gjennomgå `LICENSE` før eventuell offentlig distribusjon. Den medfølgende teksten krever skriftlig tillatelse til offentlig redistribusjon og har attribution-/identitetsvilkår. Registrer dokumentert tillatelse og behold notices som en release-forutsetning.
- [ ] Vis spillerliste med individuell status: tilkoblet, henter inn, laster by, venter på avhengighet, reparerer eller frakoblet. Skill RTT fra køtid og «sendt» fra «lastet».
- [ ] Vis konkret årsak og fremdrift når en handling avvises eller tar tid. En generell «sync»-spinner må ikke skjule at en operasjon aldri blir ferdig.
- [ ] Lag enkel diagnoseeksport med session-/build-ID, metrics, årsak og siste operasjonskjede. Utvid eksisterende redigering av private data; ikke krev permanente verbose-logger fra alle.
- [ ] Test UI-oppstart, mod-oppdatering, språkbytte, skjermskalering, fokus og gamle/manglende UI-moduler. Valider om CSSPresence-no-op er riktig for spill-loaderen med både ren bygging og spilltest.
- [ ] Sett opp CI for Core-regresjoner, protokoll/fuzz, UI-typekontroll/build, locale-/metadata-/stivalidering og dokumentasjon. Egen lokal/selvdriftet spillintegrasjon skal ikke publisere proprietære game-DLL-er i offentlige testartefakter.
- [ ] Dokumenter installasjon/oppdatering for host og klient med samme testpakke. Behold forrige fungerende pakke og changelog med reelle endringer og kjente begrensninger.

**Ferdig når:** En annen tester kan bygge eller installere en hel, identifiserbar pakke og levere en brukbar feilrapport. «DLL generert» regnes ikke som vellykket komplett mod-build eller bekreftet installasjon.

## Obligatorisk testmatrise

| Gruppe | Scenarioer | Hva skal kontrolleres |
| --- | --- | --- |
| Retning og antall | Host → A/B/C; A → host/B/C; samtidig A/B; 2 og 4 spillere, deretter 8. | Samme endelige objekter og revisjoner hos alle; egen handling kvitteres uten duplikat. |
| Bygging | Enkeltbygg, gjentatte plasseringer, serviceutvidelser, flytt/oppgrader/riv, bygg-riv-bygg samme sted, overlappende mål. | Capture, eiergraf, økonomi, nettverkstilknytning og faktisk synlig resultat. |
| Avhengigheter | Terreng → vei → bygg; veioppgradering/rivning mens bygg venter; soning/growables; ruter/stopp. | Ingen ombyttet avhengighet eller mutasjon mot fjernet entity. |
| Sekvens | Første nummer > 1, manglende første/midtre/siste, duplikat, forsinket replay, gap > 256, journal utløpt. | Begrenset kø, eksplisitt innhenting/fallback og ingen permanent stopp. |
| Epoch/join | Sen join etter > 4096 kommandoer; ny spiller midt i transfer; gjentatt resync; Abort/Resume; gammel pakke etter ny by. | Korrekt snapshot-grunnlag; ingen dobbeltapply eller data fra gammel verden. |
| Transport | TCP og Steam Relay separat; RTT 20/100/250 ms, jitter, begrenset båndbredde, kort avbrudd og reelt brudd. | Pålitelighet og køforsinkelse. Test også applikasjonshull; dropp av IP-pakker i TCP er ikke det samme som en tapt kommando. |
| Pauser/press | 5/15/25/45 s lokal frame-/pumppause, save/load, fulle data-/kontrollkøer, stor command-burst, treg klient. | Faseriktig liveness, beskyttelse mot falske ratebrudd og fungerende hard deadline. 45 s pause kan gi begrunnet timeout avhengig av fasen. |
| Recovery | Kun én klient avviker; gjentatte recovery-ønsker; reparasjon under cooldown; feil per kanal/objekt. | Riktig reparasjon med minst mulig global pause; utestående arbeid blir ikke glemt. |
| Preview | Fire bevegelige previews, prefab-/verktøybytte, cancel, bygg, disconnect, epoch-skifte, forsinkede updates. | Ingen varig ghost, ingen simuleringseffekt, korrekt navn/farge, målt kostnad. |
| Integritet/misbruk | Negativ/feil lengde, ukjent kommando/prefab, NaN/Inf, for stor batch, spoofet spiller, ugyldig epoch, replay-flood, feil passord. | Validering før dyrt arbeid; riktig årsak og begrenset ressursbruk. |
| Livssyklus | Host avslutter, klient avbryter join/load, tilbake til meny, ny session i samme prosess, oppdatering. | Ryddede sockets/køer/static-state/preview, gjenopprettede authority-systemer. |
| Save-sikkerhet | Avbrutt snapshot/save, feilet load, autosave/Continue-metadata, midlertidig klientby, legacy-repair. | Original save og brukerens metadata forblir korrekte; ingen utilsiktet overskriving. |
| Varighet | 2 timer med blandede handlinger; egen lengre natt-test før stabil release. | Ingen memory-/entity-lekkasje, stadig økende køer eller skjult permanent drift. |

## Anbefalt leveranserekkefølge

1. **Testgrunnlag og første sync-hotfix:** M0 + M1s sekvens, avsenderbekreftelse og snapshot-baseline. Ta også F07/F08 tidlig. Dette adresserer bekreftede feil som kan forklare at nye bygg ikke vises.
2. **Verifisert bygging:** M1s capture/apply-kvittering, avhengigheter og konfliktregler, med testpakken fra M6. Spilltesten deres gjentas her med samme save og handlinger.
3. **Færre resyncs:** M2s feilårsaker, reparasjonskø og målrettet innhenting; behold full recovery tilgjengelig til konsistenskontrollen er validert.
4. **Ytelse og samarbeid:** M3 og M4 itereres med samme målegrunnlag. Lever enkel byggepreview før dyrere 3D-/veipreview.
5. **Bred funksjonsdekning og stabil release:** M5 og resterende M6, inkludert 8-spillertest først etter at 2/4-spillerkravene holder.

Ingen hel milepæl er ferdig. Implementasjonsstatus øverst og avkryssede deloppgaver viser fremdriften; øvrige oppgaver beholder sine ferdigkriterier. Tidsestimat og videre oppdeling settes etter spilltest/baseline. Arbeidet krever ikke en total omskriving, men hver protokoll-/snapshot-endring må ha kompatibilitets- og migrasjonsplan.

[messaging]: CS2MPMod/Core/Session/MultiplayerSession/Messaging.cs
[notify]: CS2MPMod/Core/Session/MultiplayerSession/Notify.cs
[session]: CS2MPMod/Core/Session/MultiplayerSession/MultiplayerSession.cs
[worldsync]: CS2MPMod/Core/Session/MultiplayerSession/WorldSync.cs
[command]: CS2MPMod/Core/Protocol/Messages/Sync/SimulationCommandMessage.cs
[observer]: CS2MPMod/Game/Sync/Infrastructure/Pipeline/CommandObserver.cs
[buildrealize]: CS2MPMod/Game/Sync/Systems/Objects/BuildSyncSystem/Realize.cs
[realize]: CS2MPMod/Game/Sync/Systems/Pipeline/SyncRealizeSystem.cs
[resyncsystem]: CS2MPMod/Game/Sync/Systems/World/WorldResyncSystem.cs
[service]: CS2MPMod/Game/MultiplayerService/MultiplayerService.cs
[setting]: CS2MPMod/Setting.cs
[tcpserver]: CS2MPMod/Core/Networking/Tcp/TcpServerTransport.cs
[transport]: CS2MPMod/Core/Session/MultiplayerSession/Transport.cs
[limiter]: CS2MPMod/Core/Session/Peers/PeerRateLimiter.cs
[relayio]: CS2MPMod.Steam/SteamRelayIo.cs
[arbiter]: CS2MPMod/Game/Diagnostics/ResyncArbiter.cs
[blob]: CS2MPMod/Core/Session/MultiplayerSession/Blob.cs
[authority]: CS2MPMod/Game/Sync/Infrastructure/Pipeline/LocalAuthorityHold.cs
[citystate]: CS2MPMod/Game/Sync/Systems/City/CityStateSyncSystem/CityStateSyncSystem.cs
[gameclock]: CS2MPMod/Game/Sync/Channels/World/GameClockStateChannel.cs
[project]: CS2MPMod/CS2MPMod.csproj
[steamproject]: CS2MPMod.Steam/CS2MPMod.Steam.csproj
[publish]: CS2MPMod/Properties/PublishConfiguration.xml
[uimetadata]: CS2MPMod/UI/mod.json
[modscheck]: CS2MPMod/Game/MultiplayerService/Checks/ModsCheck.cs
[playerstate]: CS2MPMod/Core/Protocol/Messages/Session/PlayerStateMessage.cs
[markers]: CS2MPMod/Game/Sync/Players/RemotePlayerMarkerSystem.cs
