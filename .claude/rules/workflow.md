# Workflow

## For every feature

The shape of a feature: **you approve once at the start, then the AI builds without interrupting you.** Everything you would otherwise be asked about later is listed in the plan up front.

### 1. Discuss, then write the plan in `docs/plans/<feature-name>.md` as an approval manifest. Wait for explicit approval before writing code.
Ask all open questions in one go while planning. The plan must list everything the feature will do, so that your one approval covers it:

| Section | What it lists |
|---|---|
| Goal and scope | What the feature does, and what it does not do |
| Branch | The branch that will be created, for example `feature/job-search` |
| Directories and files | Every new directory and file, by layer (Domain, Application, Infrastructure, Api, client, tests, docker) |
| Packages | Every NuGet or npm package, with version and the reason. "None" if none |
| Database migrations | The planned tables, columns, keys, indexes, and the migration name, for you to review **before** any entity is written. The generated migration file appears later in `/review-changes` |
| Endpoints and DTOs | Routes, request and response DTOs, and the access of each route group. Every `.AllowAnonymous()` is listed with the reason |
| Tests | The unit and integration tests that will be written |
| Assumptions | Every decision made where the request was unclear, so you can correct it now |

**Why:** Agreeing on the design on paper is much cheaper than rewriting code. Putting the branch, files, packages, and schema in one place means one review replaces many small questions, and you can work on something else while the feature is built.

### 2. After approval, build the whole feature without stopping. Work in this order:
1. **Create the branch** with `git switch -c <name>` (or `git checkout -b <name>`), and create the directories with `mkdir`.
2. **Install all packages from the plan in one batch, first.** Package commands still need your approval (see [guardrails.md](guardrails.md)), so doing them together at the start means those prompts come once, early, and nothing is left waiting halfway through the work.
3. **Build with TDD** (step 4), one task at a time, until every item in the plan is done.
4. **Generate the migration** with `dotnet ef migrations add` once the entities and their EF Core configuration exist. It must match the schema approved in the plan. `dotnet ef database update` is not run: that is yours to decide.
5. **Finish** (steps 5 and 6): build, test, format, then `/review-changes`.
6. **Report back** with one summary: what was built, how it fits the architecture, what to check, and the Roman Nepali note. Then wait. Committing is yours to approve (step 7).

**Do not stop to ask** about names, folder choices, or small design decisions that the rules already settle. Pick the option that follows the rules and add it to the plan's Assumptions section.

**Stop and ask only when:**
- A guardrail hook blocks or asks about an action. Explain it, never work around it.
- The feature needs something that is not in the approved plan: a new package, a different schema, an extra public endpoint. Update the plan, say what changed, and ask.
- The requirement contradicts itself, or two readings of it lead to very different results.
- A test still fails after you have fixed the code properly. Never weaken the test.

**Why:** Each interruption costs you a context switch. Questions belong at the start, where they are cheap. After approval, the plan is the contract and the rules decide the small things. The stop conditions are the cases where a wrong guess is costly or a guardrail needs your judgment.

### 3. Use one branch per feature, with a short, meaningful name (for example, `feature/job-search`).
**Why:** Each feature can be reviewed, tested, and merged or dropped on its own, without mixing with other work.

### 4. Do one task at a time, test first (TDD): write a failing xUnit test, see it fail, then write the simplest code that makes it pass.
**Why:** Seeing the test fail first proves it actually tests something. Writing only enough code to pass keeps the design simple. Small steps keep each change easy to review.

### 5. A task is done only when `dotnet build` and `dotnet test` both pass with no errors. Run `dotnet format` before finishing.
**Why:** "Done" has to mean something you can verify, not "looks right to me". Formatting keeps diffs focused on real changes.

### 6. Run `/review-changes` and share the link before asking to commit or push.
It builds a private page showing the goal (from the plan), why each file changed, a side-by-side diff with notes on tricky lines, and automatic rule checks.
**Why:** You review an explained diff, not a raw one, before anything is committed or leaves your machine. Re-running it updates the same link.

### 7. Make small commits with clear messages, and only after you approve.
**Why:** Small commits are easy to review and easy to revert. You stay in control of the project history.

### Subagents work in git worktrees
When a task is split across subagents, each one works in its own git worktree: a separate folder under `.claude/worktrees/`, checked out on its own branch.
**Why:** Subagents can work in parallel without overwriting each other's files or yours. Each result arrives as its own branch, which you review with `/review-changes` and approve before it's merged.

Things to know:
- **A worktree only contains committed files.** The guardrails (`.claude/`) and `CLAUDE.md` must be committed before any subagent work, or the subagent runs without hooks or rules.
- **Worktrees start from your local `HEAD`** (`worktree.baseRef: "head"` in settings), not from GitHub, so they include local commits that aren't pushed yet.
- **Merging a subagent's branch back** is a `git merge`, so it asks you.
- **Only one worktree runs the Docker stack at a time**, because two stacks would clash on ports and container names.

### Following GitHub
Pull requests, review comments, and CI checks are followed in VS Code with the **GitHub Pull Requests** extension (publisher: GitHub), not on the website.
**Why:** Everything stays in one window next to the code.

## How to communicate

### Explain every change: what changed, why, how it fits the architecture, and what you should check.
**Why:** The goal is for you to understand the whole application, not just receive code.

### When explaining code, add a short summary in Roman Nepali (Nepali written in English letters).
Keep technical terms in English (class, constructor, `internal`, `Result<T>`), and explain the idea around them in Roman Nepali. Example: "Yo `protected` constructor lai bahira bata call garna mildaina, tesaile invalid Result banna sakdaina."
**Why:** The user understands concepts faster in their own language. English alone was getting confusing for tricky code.

### Ask about unclear requirements while planning, not while building.
Before approval, ask every open question at once. After approval, decide using the rules, record the decision under Assumptions, and keep going, unless one of the stop conditions in step 2 applies.
**Why:** A question during planning costs a minute and blocks nothing. A question halfway through a build stops the whole feature until you come back.

### Touch only files that belong to the current task.
**Why:** Unrelated changes hide inside a diff and make review harder.

### If a guardrail blocks an action, stop and explain. Never work around it.
**Why:** The guardrails exist because you decided those actions need your judgment. See [guardrails.md](guardrails.md).
