# Backend adversarial review

One verified finding. No production files changed. All executable additions are inside this reviewer's snapshot. Synthetic HTTP actors and isolated SQLite/uniquely named SQL Server LocalDB catalogs only; fixture disposal drops its own SQL catalog.

## P2 — EMS narrative accepts more text than SQL Server can persist

The new care-notes form accepts unrestricted text and sends it as the incident Notes field. At 2,001 characters, the SQL Server API throws an unhandled DbUpdateException caused by SQL truncation instead of saving or returning usable field validation. SQLite accepts exactly the same request, so local/default testing hides this hosted-provider failure. Repeatedly clicking Save cannot recover until the user guesses that the text must be shortened. No clinical-data-loss claim is made: the editor retains its draft after failure.

Source locations in the original repository (the snapshot has identical lines):
- frontend/src/pages/IncidentDetailPage.tsx:443 — EMS narrative textarea lacks a length constraint or limit explanation.
- frontend/src/pages/IncidentDetailPage.tsx:84 — sends notesDraft verbatim.
- backend/PulseLink.Api/Dtos/IncidentDtos.cs:17 — UpdateIncidentRequest Notes has no MaxLength validation.
- backend/PulseLink.Infrastructure/Data/PulseLinkDbContext.cs:49 — database Notes limit is 2000.
- backend/PulseLink.Infrastructure/Data/Migrations/SqlServer/20260814202926_InitialCreate.cs:201 — actual nvarchar(2000) column.
- backend/PulseLink.Api/Controllers/IncidentsController.cs:182,196,607 — assignment then unhandled persistence failure.

Executable repro: snapshot/backend/PulseLink.Tests/Api/InterrogateNarrativeTests.cs. Run from snapshot:

```powershell
dotnet restore backend/PulseLink.Tests/PulseLink.Tests.csproj --ignore-failed-sources --verbosity minimal
dotnet test backend/PulseLink.Tests/PulseLink.Tests.csproj --no-restore --filter FullyQualifiedName~InterrogateNarrativeTests --logger 'console;verbosity=normal'
```

Restore and test required sandbox escalation for NuGet/LocalDB access. The test creates a synthetic OnScene incident, authenticates as its agency paramedic, fetches the current version, and PUTs a valid complaint/destination plus 2,001 ASCII n characters as notes. It repeats against both actual providers.

Expected: a saved record or controlled HTTP 400 field validation; the UI must expose any applicable limit. Actual run output (2026-11-05):

```text
Provider=SQL Server narrative length=2001
Unhandled exception: DbUpdateException; String or binary data would be truncated in table 'PulseLink_ConcurrencyTest_99a55799cca7420cbceec3cbd8cc4727.dbo.Incidents', column 'Notes'.
... IncidentsController.SaveIncidentAsync ... line 607
... IncidentsController.Update ... line 196
Failed ...AcceptedNarrativeShouldPersistOrReturnValidation(sqlServer: True)
Passed ...AcceptedNarrativeShouldPersistOrReturnValidation(sqlServer: False)
Total tests: 2
Passed: 1
Failed: 1
```

ASP.NET TestServer propagates the unhandled exception to the test client; this probe does not claim to have observed an HTTP 500 over a deployed socket. It proves the HTTP application pipeline has no controlled response for this valid form input. The source's matching SQL Server schema establishes relevance to that deployment provider.

## Dropped candidates (5)

1. Hospital read/receipt role bypass — shared hospital visibility predicate and endpoint role attribute enforce destination/status restrictions. Existing dedicated HTTP coverage checked; no counterexample.
2. Receipt duplication, clinical version change, or competing receipt actor replacement — existing deterministic receipt race coverage checked; no counterexample.
3. Stale clinical edit crossing handoff — existing explicit version and concurrency checks/tests cover parent/child atomicity; no counterexample.
4. Demo invalid destination and loosely validated request keys — source differs from API validation, but normal UI selection/key generation does not reach those inputs; no independently demonstrated user-facing failure retained.
5. Queue counts becoming inconsistent during concurrent receipt — separate count/read queries permit timing differences, but list traversal already documents non-snapshot behavior; not retained without a convincing contract violation.

Possible cross-tab demo read/modify/write loss was forwarded to the parent/other reviewer and is not claimed here. Export's deliberately partial FHIR-inspired shape and lack of offline recovery were excluded by scope.

Context7: resolved /dotnet/entityframework.docs and consulted EF interception documentation for EF Core 8 test investigation. Used existing repository WebApplicationFactory/fixture APIs and installed 8.0.11 EF/TestHost dependencies.

## Supporting scoped regression run

Command (from snapshot):
```powershell
dotnet test backend/PulseLink.Tests/PulseLink.Tests.csproj --no-build --filter 'FullyQualifiedName~HandoffReceiptHttpTests|FullyQualifiedName~IncidentVisibilityHttpTests|FullyQualifiedName~IncidentConcurrencyHttpTests|FullyQualifiedName~IncidentStateHttpTests' --logger 'console;verbosity=minimal'
```
Actual output: `Passed! - Failed: 0, Passed: 60, Skipped: 0, Total: 60, Duration: 47 s`. Full captured output: snapshot/scope-tests.log. Passing coverage supports dropped candidates but is not a claim that all interleavings are safe.
