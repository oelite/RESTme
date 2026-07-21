# Summary

<!-- What does this MR do? Link the issue it closes. -->

Closes #<!-- issue number -->

## Changes

- <!-- change 1 -->
- <!-- change 2 -->
- <!-- change 3 -->

## Verification

<!-- Paste executed command output or screenshots. No claims without evidence. -->

- [ ] `dotnet build` / `npm run build` clean
- [ ] Unit tests pass: <!-- command + result -->
- [ ] Integration tests pass (local Docker): <!-- command + result -->
- [ ] E2E tests pass (Playwright): <!-- command + result -->
- [ ] Health endpoint responds 200
- [ ] No mock/placeholder/TODO/hard-coded values remain

## Documentation Impact

<!-- Does this change require doc updates? -->

- [ ] No documentation impact
- [ ] Yes — Isabella notified with details below

**Details:** <!-- what changed, what docs need updating -->

## Review Notes

<!-- Anything reviewers should focus on -->

## Checklist

- [ ] Branch is up to date with `develop` (or target branch)
- [ ] Worktree created with correct owner DNA
- [ ] Commit messages follow required format
- [ ] No AI/Anthropic references or emojis in commits
- [ ] LSP diagnostics clean on changed files
- [ ] Pre-commit hooks pass

/label ~"mr" ~"needs-review"
