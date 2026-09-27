# M0 – eldre lokal spilløkt, undersøkt 18. september 2026

Dette er observasjoner fra lokal `CS2MP-flight.log` datert 15. september, ikke
en baseline for dagens protokoll-65-pakke. Ingen save eller installasjon ble endret.
Private stier, nettverksadresser, spilleridentifikatorer og rå stack er utelatt.
Tidspunktene nedenfor er UTC.

## Bekreftede observasjoner

- Den siste prosessøkten startet 09:02:12 med protokoll **62**, spill **1.6.0f1**
  og Unity **2022.3.71f1**. Senere health-linjer angir rollen Client.
- Loggen registrerer snapshot-resume for epoch 1–6: 09:05:14, 09:15:29,
  09:19:21, 09:25:21, 09:36:06 og 09:51:42. Det er fem nye resume-hendelser etter
  den første, men dette beviser ikke fem automatiske resync-forespørsler eller årsaken.
- Ved 09:50:37 rapporterer ett 30-sekundersvindu 211 frames, gjennomsnittlig
  frameintervall 142 ms og verste intervall 310 ms. Loggens avrundede rate er 7/s.
- Samme vindu rapporterer `SyncCost` på 9848 ms (32,8 %). De største oppførte
  delene er Companies.StateBoundary (2401 ms), CityState (2277 ms),
  Occupancy.Economy (1422 ms) og Occupancy.Purchases (1345 ms).
  Dette er profilerens egne summer, ikke et kontrollert mål på moddens nettooverhead.
- Ved 09:52:24 registrerer Unity en out-of-memory-feil under forsøk på å
  allokere **10 737 418 248 bytes**, omtrent 10 GiB, med etiketten NativeArray.
  Feilen følger etter epoch-6-resume i loggen. Tidsrekkefølgen beviser ikke årsak.
- Den nye operasjonseksporten ga **0 kompatible poster** fra denne gamle loggen.
  Den kan derfor ikke rekonstruere dagens capture → apply-kontrakt eller nye kømålinger.

## Hva dette ikke fastslår

Ingen bekreftet rotårsak til minnefeilen, ingen identifisert ansvarlig mod/system,
ingen bevis for at feilen finnes eller er rettet i protokoll 65. Native stack alene
identifiserer ikke en C#-allokering. Ingen host-logg eller korrelert klientlogg fra
en annen maskin er analysert. Det er ikke målt singleplayer/2-/4-spiller-baseline.

## Prioritet i den kommende M0-testen

1. Bruk identisk komplett testpakke og samme utgangssave på alle testmaskiner.
   Lokal save-mappe finnes, men en eksisterende by er ikke valgt som baseline.
   Den midlertidige join-save-filen må ikke behandles som en original host-save.
2. Samle samtidige logger før og etter join/resync og ved bygging. Dokumenter
   hva som utløste hver sync; ikke utled automatiske resync-antall fra resume alene.
3. Følg prosess-/native-minne over flere snapshot-runder. Bevar krasjdiagnostikk
   hvis allokeringsfeilen gjentas; ikke gjør store allokeringsendringer uten identifisert kodebane.
4. Sammenlign frameintervall og profilerens delkostnader med samme save og handlinger
   i singleplayer, 2 og 4 spillere. Hold lastetid utenfor spill-framevinduene.

M0 står fortsatt åpen. Den gamle økten gir konkrete testprioriteringer, ikke godkjenning.
