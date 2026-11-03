# Restme Delivery Status — Emma umbrella #14

| Item | Status | Owner | Next actions | Evidence / Notes |
|---|---|---|---|---|
| restme#12 / MR !11 | MR opened, mergeable, approvals ok, NO head pipeline | Daniel + Grace | Trigger/verify CI on SHA 4ef6e3285e57c502791ebb5558d4bcb931e8a4c6; Grace review ShadowedStatusSerializer/RestmeShadowSerializationProvider; after green CI+review merge, record exact develop pre-release package version and notify obelisk | MR desc has 14-test matrix, 103/103 MongoDb integration passed locally; pipeline unknown. Merge blocked by hard gates. |
| restme#11 | Open, assigned | Daniel + Olivia + Grace | Fix ServiceLocator.Clear fallback in Rest.cs + ServiceLocator.cs; regression tests (static-ctor survive, second host resolves redis/s3); validate against real shared stack; review. Create new origin-auth consumer follow-up for workaround removal when fix lands (do not reopen #1564). | Root cause matches ~110 test failure class; test-side workaround in OriginAuthWebApplicationFactory. |
| restme#9 | Open, P1 | Ethan + Daniel + Maya + Grace | Contract placement in Abstractions, IBucketEncryptionPolicy, MinIO error-code classification (501/NotImplemented/XMinioKMSNotConfigured/ServerSideEncryptionConfigurationNotFoundError), evidence fields, Unavailable first-class, fail-closed, tests vs shared-stack MinIO. | origin-auth#1416/#1412 closed — no proof of enablement/integration; reconcile with BR-21-020 R7 and open new consumer issues if needed; do not rely on closed tickets as completion. |
| restme#7 | Open, P2 | Daniel + Maya + Olivia | Case-insensitive User=/username= in ClickHouseHelpers.ParseConnectionString, remove DEBUG credential logging, tests both variants; security review; shared-stack ClickHouse integration. | Origin-auth currently normalizes User= at config-build time; evaluate workaround removal on separate issue after ship. |
| obelisk#296 | Blocked | Daniel | Consume package only after package published; bump with regression tests per note 63381 §5. | Waits on #12. |
| obelisk#280 | Open, blocked by chain | Ethan | Credential/auth wiring per AC; UAT validation depends on #296 package bump/deploy. Use approved shared-stack provisioning only. | Serializer issues expected to clear after #296 lands. |
| origin-auth#1564 | Closed | — | Closed, do not reopen. New consumer follow-ups for workaround removals will be created when fixes land. | Triage closed; framework issues separated. |
| origin-auth#1416/#1412 | Closed | — | Closed, not evidence of completion for #9. Reconcile and create new issues if required. | Requires evidence review. |

## Checkpoints (repeatable; session-limited)
1. MR !11 CI status on SHA 4ef6e3285e57c502791ebb5558d4bcb931e8a4c6
2. Grace review complete; merge + record package version + cleanup
3. Package published -> obelisk#296 bump + tests
4. obelisk deploy/UAT (Ethan) for #280
5. #11, #9, #7 per-issue reviews/CI + shared-stack tests
6. Consumer workaround-removal issues created as needed; documentation gates per workflow

## Boundaries
- No UI added to restme (library repo). E2E/UI verification only applies if consumer repos have UI; Isabella to map real journeys in obelisk/origin-auth.
- No continuous monitoring beyond this session. These checkpoints are proposed for repeatable execution.
