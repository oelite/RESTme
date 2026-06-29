# GitLab Issue Templates

These templates are the golden standard for all OElite repositories.

## Usage

Copy the `.gitlab/issue_templates/` and `.gitlab/merge_request_templates/` directories into each OElite repo, or configure GitLab to use a shared template repository.

## Templates

- `Feature.md` — New features and enhancements
- `Bug.md` — Bug reports and fixes
- `Task.md` — Technical tasks, refactors, documentation work, and tooling changes
- `Default.md` (in `merge_request_templates/`) — Standard MR template

For the detailed specification of each template, see `coding-standards/5_git_workflow_standards/ISSUE-MR-TEMPLATES.md` in the `oelite/coding-standards` repository.

## Target Branch

Unless a repo specifically documents otherwise in its `AGENTS.md`, MRs must target the `develop` branch.
