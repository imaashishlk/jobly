# code-scanner subagent

## Context

You want a "bot" you can run **whenever you want** that scans every file and suggests improvements for **readability, performance, and best practices**, without heavy AI. Decisions: **report only** (never blocks the build), and **one subagent only** (no skill, no project-wide analyzer settings, no GitHub Actions for now).

The scanning itself is Microsoft's built-in **.NET source code analysis** (Roslyn analyzers), documented at
<https://learn.microsoft.com/dotnet/fundamentals/code-analysis/overview>. It ships inside the .NET SDK: **no NuGet package, no AI** does the scanning.

| Rule family | Group in the report | Examples |
|---|---|---|
| `CA18xx`, `CA1860`… | Performance | CA1822 (method can be `static`), CA1848 (`LoggerMessage` for fast logging), CA1860 (`.Count` instead of `.Any()`) |
| `CA1xxx`/`CA2xxx` design, reliability, security | Best practice | CA2016 (pass the `CancellationToken` on), CA2000 |
| `IDExxxx` code style | Readability | IDE0005 (unused `using`), IDE0059 (unused value), IDE0011 (braces) |

The AI part is small: the agent only **groups and explains** the analyzer output. It doesn't read every file itself.

## Why a subagent

- It runs in its **own context**, so the long `dotnet build` warning output stays inside it. Only a short grouped report comes back to the main chat.
- Its tools are limited to **read-only** (`Bash`, `Read`, `Grep`, `Glob`), with no `Edit`/`Write`, so it can't change code.
- It runs on **Haiku** (`model: haiku`), the cheapest model, since the work is just sorting compiler output.
- It runs in the current folder, not a worktree, because it never changes files.

**How you start it:** say "run the code-scanner agent", or `@agent-code-scanner`. Optionally name one project: "scan src/Jobly.Api".

## How it scans

It turns the analyzers on **only for that one build**, through command-line properties, so `Directory.Build.props`, `.editorconfig`, and your normal `dotnet build` stay exactly as they are:

```bash
dotnet build --no-incremental \
  -p:AnalysisLevel=latest-Recommended \      # Microsoft's "Recommended" rule set
  -p:EnforceCodeStyleInBuild=true \          # also run IDE style rules (from .editorconfig) during build
  -p:TreatWarningsAsErrors=false             # report only: findings stay warnings, the build doesn't fail
dotnet format --verify-no-changes            # lists formatting problems without changing files
```
`--no-incremental` makes it analyze every file, not just the changed ones.

## Changes

One new file: `.claude/agents/code-scanner.md`.

- **Frontmatter:** `name: code-scanner`, a `description` (it scans for readability/performance/best-practice findings when asked), `tools: Bash, Read, Grep, Glob`, `model: haiku`.
- **Instructions (body):**
  1. Run the two commands above (whole solution, or the project you named).
  2. Collect each warning: rule ID, file, line, message. Remove duplicates.
  3. **Skip rules that conflict with our conventions**, each listed with a **Why**. Known one: **CA1707** (underscores in names) in `tests/`, because our test names are `MethodName_Scenario_ExpectedResult`.
  4. Report grouped **Readability / Performance / Best practice**, most important first: `file:line`, a one-line suggested fix, and the Microsoft docs link (`https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/<id>` or `.../style-rules/<id>`).
  5. End with a short Roman Nepali summary (workflow rule).
  6. **Never edit files**, and never work around a blocked command. It reports, and you decide what to fix (as a normal task with approval).

## Packages / migrations
None.

## Approval points
- Creating the agent file (hook).
- The `dotnet build` with extra `-p:` arguments may ask each time, because only plain `dotnet build` is on the free list. If that gets annoying, we can add that exact command to `.claude/settings.json` later (a guardrail change, so it needs your approval).

## Verification
1. Ask "run the code-scanner agent": it returns grouped findings with links, and the build still says "succeeded".
2. Run plain `dotnet build` and `dotnet test`: both still pass, unchanged.
3. `git status` shows only the new agent file (and this plan).
