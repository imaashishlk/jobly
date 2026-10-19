# Guardrails

What AI assistants may and may not do in this repository, why, and how each rule is enforced.

**The core rule:** the AI can look freely, but anything that changes something, touches the network, or touches SQL needs your approval. Deleting is never allowed.

There are three levels of enforcement:

| Level | Where | What it means |
|---|---|---|
| **Hook** | `.claude/hooks/*.js` | A script runs before every command and every file change. It blocks the action or asks you. This is code, not a request, and the AI cannot argue past it. |
| **Permission** | `.claude/settings.json` | Claude Code's built-in allow / ask / deny lists. A second, independent layer behind the hooks. |
| **Instruction** | `CLAUDE.md`, `.claude/rules/*.md` | Written guidance the AI is told to follow. Not technically enforced. |

## Blocked: always denied, even with approval

| Rule | Why | Enforced by |
|---|---|---|
| No deleting files or folders (`rm`, `del`, `Remove-Item`, `rmdir`, `find -delete`, …) | Deleted work may be impossible to recover. If something should go, you delete it yourself. | Hook + Permission |
| No discarding work (`git clean`, `git rm`, `git reset --hard`, `git checkout --`, `git restore`, dropping stashes, deleting branches) | These silently throw away uncommitted or unmerged work. | Hook + Permission |
| No force-push, no `--no-verify` | Force-push rewrites shared history, and `--no-verify` skips safety checks. | Hook + Permission |
| No reading or writing outside the project folder (absolute paths, `..`, `~`, `cd` out) | The AI's reach stays inside Jobly. Your other files, SSH keys, and settings are off limits. | Hook + Permission (`blockReadsOutsideWorkingDirectories`) |
| No editing existing migrations | Databases that already ran the migration would disagree with the code. | Hook + Permission |
| No removing migrations or dropping the database | Destroys schema history or data. | Hook + Permission |
| No secret files (`.env`, `secrets.json`, `.pfx`, `.pem`, `.key`) | Secrets belong in user secrets or environment variables, never in the repo. | Hook + Permission |
| No secrets in file content (connection strings with passwords, keys, tokens) | A secret committed to git stays in its history forever. | Hook |
| No skipping tests (`[Fact(Skip = …)]`) | A skipped test hides a real failure. "Done" would no longer mean "works". | Hook |
| No browser automation | It acts inside your real browser with your signed-in accounts. | Permission |
| No deleting Docker data (`compose down -v`, `volume rm`, `rm`, `rmi`, `prune`) | It deletes your local database and images. | Hook + Permission |
| No `Start-Process` | It can launch any program outside the checks above. | Hook |

## Needs your approval

| Action | Why | Enforced by |
|---|---|---|
| **Every file creation or edit** | You review every change as it happens. | Hook |
| **Every command not on the read-only list** (for example `mkdir`, `dotnet new`, `New-Item`, writing output to a file, running scripts) | Anything that might change something is your call. | Hook |
| **Network access**: `curl`, `Invoke-WebRequest`, `git pull/fetch/clone/push`, `dotnet restore`, `npm install`, WebFetch, WebSearch | Data could leave your machine, and downloaded code could run. You decide when that's acceptable. | Hook + Permission |
| **SQL Server access**: `sqlcmd`, `Invoke-Sqlcmd`, `bcp`, `sqlpackage`, `dotnet ef` | Database changes can't be undone the way file changes can. | Hook + Permission |
| `git commit`, `git merge`, `git rebase`, creating branches | You own the project history. | Hook + Permission |
| Adding or removing NuGet or npm packages | Every package is outside code we take responsibility for. | Hook + Permission |
| `dotnet user-secrets` | Secrets are yours to manage. | Hook + Permission |
| Docker commands other than compose up/down/logs/ps on `docker/docker-compose.yml` | They can run arbitrary images, use the network, and change containers. | Hook |
| Publishing a claude.ai Artifact (for example the `/review-changes` page) | It uploads content, including your code diff, to claude.ai. | Permission |
| Removing tests (fewer `[Fact]`/`[Theory]` after a change) | Removing a test can quietly hide a failure. | Hook |
| Changing the guardrails themselves (`.claude/settings*.json`, `.claude/hooks/`, `.claude/rules/`) | The AI must never be able to quietly loosen its own rules. | Hook + Permission |

## Runs without asking

These are read-only, or are commands you explicitly allowed:
- `dotnet build`, `dotnet test`, `dotnet format`, `dotnet run --project src/Jobly.Api`
- `docker compose -f docker/docker-compose.yml` with `up`, `down` (without `-v`), `logs`, or `ps`
- Read-only commands: `ls`, `cat`, `grep`, `Get-ChildItem`, `Get-Content`, `git status`, `git log`, `git diff`, `git show`, listing branches
- `node .claude/hooks/test-guards.js`
- Reading project files

`dotnet format` rewrites code formatting and `dotnet build` writes `bin/` and `obj/`. They are allowed because you listed them as freely usable.

## Instruction-only rules

These can't be reliably checked by a script, so the AI is expected to follow them:

| Rule | Why |
|---|---|
| Don't change files unrelated to the current task. | Small, focused changes are easy to review and easy to revert. |
| Plan in `docs/plans/<feature-name>.md` and get approval before writing code. | Agreeing on the design first is cheaper than rewriting code. |
| Never weaken a failing test to make it pass. | The test exists to catch exactly that failure. |
| When unsure, ask instead of guessing. | A wrong guess costs more time than a question. |

## If a guardrail blocks something

The AI must stop and explain what it was trying to do and which rule blocked it. It must never look for a workaround, such as a different command that does the same thing. You decide whether to do the action yourself or change the rule.

## Checking the guardrails

```bash
node .claude/hooks/test-guards.js
```

This runs sample commands and file changes through the hooks and prints what each one decides. Run it after any change to `.claude/hooks/`.
