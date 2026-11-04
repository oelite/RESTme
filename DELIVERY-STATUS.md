# Restme Delivery Status — Emma umbrella #14

| Item | Status | Owner | Next actions | Evidence / Notes |
|---|---|---|---|---|
| restme#12 / MR !11 | **MERGED** (merge commit 571ac5d, merged 2026-11-03T14:14:49Z) | Daniel + Grace | Retrospective flag: merged WITHOUT recorded Grace serializer review (ShadowedStatusSerializer/RestmeShadowSerializationProvider) and WITHOUT head pipeline evidence (head_pipeline=null) — flag for retrospective; CI recovery tracked in restme#16. Record exact develop pre-release package version published by CI and notify obelisk#296. | MR desc has 14-test matrix, 103/103 MongoDb integration passed locally. Retrospective review item, not a merge blocker. |
| restme#11 | Open, assigned | Daniel + Olivia + Grace | Fix ServiceLocator.Clear fallback in Rest.cs + ServiceLocator.cs; regression tests (static-ctor survive, second host resolves redis/s3); validate against real shared stack; review. Create new origin-auth consumer follow-up for workaround removal when fix lands (do not reopen #1564). | Root cause matches ~110 test failure class; test-side workaround in OriginAuthWebApplicationFactory. |
| restme#9 | Open, P1 | Ethan + Daniel + Maya + Grace | Contract placement in Abstractions, IBucketEncryptionPolicy, MinIO error-code classification (501/NotImplemented/XMinioKMSNotConfigured/ServerSideEncryptionConfigurationNotFoundError), evidence fields, Unavailable first-class, fail-closed, tests vs shared-stack MinIO. | origin-auth#1416/#1412 closed — no proof of enablement/integration; reconcile with BR-21-020 R7 and open new consumer issues if needed; do not rely on closed tickets as completion. |
| restme#7 | Open, P2 | Daniel + Maya + Olivia | Case-insensitive User=/username= in ClickHouseHelpers.ParseConnectionString, remove DEBUG credential logging, tests both variants; security review; shared-stack ClickHouse integration. | Origin-auth currently normalizes User= at config-build time; evaluate workaround removal on separate issue after ship. |
| obelisk#296 | Blocked | Daniel | Consume package ONLY after verified develop package version is published (post-merge !11); bump with regression tests per note 63381 §5. Do not adopt on unverified version. | Waits on CI #16 to restore pipeline + package publication evidence. |
| obelisk#280 | Open, blocked by chain | Ethan | Credential/auth wiring per AC; UAT validation depends on #296 package bump/deploy. Use approved shared-stack provisioning only. | Serializer issues expected to clear after #296 lands with verified package. |
| origin-auth#1564 | Closed | — | Closed, do not reopen. New consumer follow-ups for workaround removals will be created when fixes land. | Triage closed; framework issues separated. |
| origin-auth#1416/#1412 | Closed | — | Closed, not evidence of completion for #9. Reconcile and create new issues if required. | Requires evidence review. |
| restme#13 | Open, medium | Isabella | Documentation accuracy audit — authoritative doc sync; includes F3 shadowed-Status technical documentation (see F3 gate decision below). | Assigned isabella.phanes. |
| restme#14 | In Progress, P1 | Emma | Umbrella coordination (this file, MR !13). | emma.phanes. |
| restme#16 | Open, P1 | Ethan | Restore MR + develop CI pipeline evidence for restme (head_pipeline=null on !12–!14; !11 merged without head pipeline). | Blocks verified merge decisions on all open MRs. |

## Current open MRs
| MR | Title | Source -> Target | Status |
|---|---|---|---|
| !12 | [#11] Fix ServiceLocator.Clear() wiping factories | fix/service-locator-clear-wipe -> develop | Open, awaiting re-review (Grace) |
| !13 | docs: record restme delivery coordination status | feature/emma-restme-delivery-14 -> develop | Open; Grace requested corrections (note 64027) — addressed in this commit |
| !14 | docs: sync S3 connection-string contract | task/doc-audit-13 -> develop | Open (Isabella, linked #13) |

## Checkpoints (repeatable; session-limited)
1. ~~MR !11 CI status on SHA 4ef6e328~~ — MOOT post-merge; replaced by: record develop pre-release package version published by CI and notify obelisk#296 (blocked until #16 restores CI)
2. CI evidence restored (#16, Ethan); then Daniel MR !12 re-review by Grace on final head SHA
3. Ethan #9 (bucket-encryption contract) per-issue CI + shared-stack MinIO tests + Maya review
4. Daniel #7 (ClickHouse User= alias + credential-log removal) + Maya/Olivia review
5. Isabella docs #13 (includes F3 shadowed-Status technical guidance; MR !14)
6. Package published -> obelisk#296 bump + tests (ONLY after verified package version)
7. obelisk deploy/UAT (Ethan) for #280
8. Consumer workaround-removal issues created as needed; documentation gates per workflow

## Boundaries
- No UI added to restme (library repo). E2E/UI verification only applies if consumer repos have UI; Isabella to map real journeys in obelisk/origin-auth.
- No continuous monitoring beyond this session. These checkpoints are proposed for repeatable execution.

## F3 shadowed-Status documentation — requirements-impact gate decision (#13 / #14)
**Decision: APPROVED — Isabella may document F3 as technical guidance under #13 / MR !14 without business sign-off.**
- **Basis:** restme#12 / MR !11 (fix(mongo): resolve Status-shadow serialization order-independently) is MERGED (571ac5d, 2026-11-03T14:14:49Z). The F3 content describes the *technical behavior of the framework* (serialization order-independence of shadowed Status), not a new BRD/SRD/user-story or business rule. A merged implementation is the authoritative source of behavior; documentation records observed behavior, it does not create requirements.
- **Scope of the decision:** descriptive technical guidance only (serialization contract, order-independence, provider behavior). It does NOT authorize: new consumer API surface, new user stories, new business rules, or UI scope in any consumer repo.
- **Traceability requirement (Isabella):** every F3 doc statement must cite the merged implementation as its source (MR !11 / commit 571ac5d / SHA 4ef6e3285e57c502791ebb5558d4bcb931e8a4c6). If any F3 statement cannot be traced to merged code, it is OUT OF SCOPE and must be escalated as a requirements question — it is not a documentation-only change.
- **Not blocked:** no business sign-off is required for this gate. If Isabella judges a statement needs business sign-off, she must say exactly what is blocked and escalate; nothing in this decision pre-empts that.
- **Retrospective flag (per Grace note 64027):** MR !11 was merged without recorded Grace serializer review and without head pipeline evidence. Recorded here; not a merge blocker, but a process gap to review in the retrospective.
