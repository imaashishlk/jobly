# Workflow

## For every feature

### 1. Discuss, then write a plan in `docs/plans/<feature-name>.md`. Wait for explicit approval before writing code.
The plan covers the goal, the changes in each layer, the endpoints and DTOs, the tests, and any packages or migrations needed.
**Why:** Agreeing on the design on paper is much cheaper than rewriting code. It also gives you a document to review and learn from, one file per feature.

### 2. Use one branch per feature, with a short, meaningful name (for example, `feature/job-search`).
**Why:** Each feature can be reviewed, tested, and merged or dropped on its own, without mixing with other work.

### 3. Do one task at a time, test first (TDD): write a failing xUnit test, see it fail, then write the simplest code that makes it pass.
**Why:** Seeing the test fail first proves it actually tests something. Writing only enough code to pass keeps the design simple. Small steps keep each change easy to review.

### 4. A task is done only when `dotnet build` and `dotnet test` both pass with no errors. Run `dotnet format` before finishing.
**Why:** "Done" has to mean something you can verify, not "looks right to me". Formatting keeps diffs focused on real changes.

### 5. Run `/review-changes` and share the link before asking to commit or push.
It builds a private page showing the goal (from the plan), why each file changed, a side-by-side diff with notes on tricky lines, and automatic rule checks.
**Why:** You review an explained diff, not a raw one, before anything is committed or leaves your machine. Re-running it updates the same link.

### 6. Make small commits with clear messages, and only after you approve.
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

### If a requirement is unclear, ask instead of guessing.
**Why:** A question costs a minute. A wrong guess costs a rewrite.

### Touch only files that belong to the current task.
**Why:** Unrelated changes hide inside a diff and make review harder.

### If a guardrail blocks an action, stop and explain. Never work around it.
**Why:** The guardrails exist because you decided those actions need your judgment. See [guardrails.md](guardrails.md).
