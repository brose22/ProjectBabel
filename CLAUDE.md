## Agent skills

### Issue tracker

Issues live in GitHub Issues for brian-md-rose/ProjectBabel, via the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Triage labels

Default five-role vocabulary: `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: one `GLOSSARY.md` and `docs/adr/` at the repo root. See `docs/agents/domain.md`.

## Token budget

Every turn re-sends the whole context, so cost is roughly turns × context size. Keep both small.

- **Read narrowly.** Don't `cat` or Read whole large scripts (`RunScreens.cs`, `MatchManager.cs`, `Tower.cs`, `MainMenu.cs`, `CastleScreen.cs`, `Enemy.cs`, `AiPlayer.cs`, `GreyBoxMatchBuilder.cs`). Grep for the symbol, then read only the line range you need. Read each file once; don't re-read a range already in context.
- **Read only the docs you need.** Read the `GLOSSARY.md` terms and ADR sections the task touches, not every doc and issue comment.
- **Verify cheaply per issue.** Per issue: `py -I Tools/compile_check.py .` plus pure-logic checks. Save Play-mode passes through the Unity MCP for one integration check at the end of a milestone, unless the issue is UI or scene work that can't be checked any other way.
- **Keep Unity scripts tight.** Batch checks into one `RunCommand` and print short pass/fail summaries, not full object dumps or logs.
- **Size issues for about 60 turns or fewer.** Split anything that touches most of the codebase (as #27 did) into smaller issues before starting.
- **Match the model to the work.** Use a cheaper model or lower effort for mechanical work: data, renames, assets, tests. Keep the strongest model for design-heavy work.
