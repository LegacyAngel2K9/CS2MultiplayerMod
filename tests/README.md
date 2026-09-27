# Core regression checks

Run `dotnet run --project tests/CS2MPMod.Core.Tests -c Release` from the repository root.
The executable exits nonzero on failure. It compiles the real Core sources, uses a
fake reliable transport and a controlled clock, and requires no game installation,
NuGet test framework, network listener or mod deployment. Reflection is confined to
fixture construction; messages exercise the production codec and session Update.

These checks do not replace net48 compilation, Steam integration or multi-player
in-game verification of native object realization.

`build/build-test-package.ps1` runs these checks, builds the full mod into
`build/staging/Mods/CS2MPMod`, then creates a ZIP and file-hash manifest under
`build/packages`. It does not install the mod into the game's active Mods folder.
See `build/TEST-PACKAGE.md` for the in-game verification sequence.
# Oppfølging: reparasjonsperioden for bygg

Testsettet har 53 regresjoner. Syv dekker kontrakt, rapportkodek, autentisering, host-kvittering,
meldingsretning, varig utboks/reconnect og duplikater/ID-konflikt.
Fire dekker ventekø, cooldown, avbrudd og sesjonsbytte,
og tolv dekker begrenset operasjonsdiagnostikk, rutestatus og tidsmåling. `test-diagnostic-export.ps1` tester
separat at private felt ikke følger diagnostikkeksporten.
`test-diagnostic-summary.ps1` kjører også eksporttesten og kontrollerer lokale
kvantiler, duplikat-completion og at tomt datagrunnlag ikke gir oppdiktet null-latency.
RecoveryTests kompilerer den virkelige resync-arbiteren,
rapporten og innboksen; kun spill-loggsinken er erstattet. Dette tester hold, tilbaketrekking,
isolasjon mellom operasjoner og konsumering av rapporter, ikke Unity-plassering.
