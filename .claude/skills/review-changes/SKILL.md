---
name: review-changes
description: Build an explained, side-by-side review page of the current branch's changes (goal from docs/plans, why each file changed, inline notes, rule checks) and publish it as a private claude.ai Artifact. Use when the user asks to review, explain, or see the diff, and always before asking to commit or push.
argument-hint: "[base branch or commit, default main]"
---

# /review-changes

Produce a review page that lets the user understand every change before it leaves their machine. The page is a reading aid only: never commit, push, or change code as part of this skill.

## 1. Collect the changes (read-only git)

Use the base from the argument, or `main` if none was given. If the base doesn't exist yet (for example on the very first commit), say so and use the first commit (`git rev-list --max-parents=0 HEAD`).

```bash
git rev-parse --abbrev-ref HEAD                  # branch name
git log --oneline <base>..HEAD                   # commits on this branch
git diff --stat <base>...HEAD                    # committed changes, per-file counts
git diff <base>...HEAD                           # committed changes, full
git status --porcelain                           # uncommitted and untracked files
git diff HEAD                                    # uncommitted changes to tracked files
git ls-files --others --exclude-standard         # untracked files (read them with the Read tool; every line counts as added)
```

Mark files that only appear in the last three commands as `"uncommitted": true`.

**Never include:** `.env*` (except `.env.example`), `secrets.json`, `*.pfx`, `*.pem`, `*.key`, or anything under `.reviews/`. If a diff line looks like a key, token, or password, replace the value with `••••` and add a `fail` rule check.

## 2. Find the motive

Strip the branch prefix (`feature/`, `fix/`, …) and look for `docs/plans/<name>.md`. If it exists, take the goal from it (one or two sentences). If not, set `plan.found` to `false`. The page then shows "No plan found" in red, which is a workflow problem worth pointing out.

## 3. Explain each file

For every changed file:
- **layer**, one of: `Domain`, `Application`, `Infrastructure`, `Api`, `Tests`, `Client`, `Docker`, `Docs`, `Config`. Decide from the path.
- **why**: one to three plain sentences on what changed and why, tied to the plan's goal. Write for a junior developer.
- **notes**: short explanations attached to specific lines (`line` = line number on the `new` side, or `old` for deleted lines). Add them for anything that could confuse a junior developer: DI registrations, `CancellationToken` plumbing, Result-to-HTTP mapping, EF Core configuration, non-trivial LINQ, middleware order, log statements. Aim for a few useful notes, not one per line.
- **generated**: `true` for migration designer and snapshot files, lock files, and other tool output. These cards start collapsed.
- **Large files:** for diffs over about 150 changed lines, include the most important hunks only and set `truncated` to a sentence like `"Showing 3 of 9 hunks. Open the file in VS Code for the rest."`

## 4. Check the rules

Fill `checks` with one entry each. `status` is `pass`, `warn`, `fail`, or `na`, and `detail` is one short line:

- Tests added for new service methods and endpoints (`.claude/rules/testing.md`)
- No `async void`
- No `DateTime.Now` / `DateTime.UtcNow` (use `TimeProvider`)
- No `new HttpClient` (use `IHttpClientFactory`)
- `CancellationToken` accepted and passed through
- Expected failures use `Result<T>`, not exceptions
- Every route group declares its access, endpoints override only where they differ, and every `.AllowAnonymous()` has a comment saying why (`.claude/rules/security.md`)
- Every protected endpoint has a 401/403 test, and every update or delete of a user-owned record checks ownership in the service
- No existing migration edited
- No new NuGet/npm packages without approval
- No secrets in the diff
- No unrelated files changed
- `dotnet build` and `dotnet test` pass. Run them; both are allowed without asking.

Count new `[Fact]` and `[Theory]` attributes into `testsAdded`.

## 5. Build the page

1. Read `.claude/skills/review-changes/template.html`.
2. Replace `__TITLE__` with `<branch-name-without-prefix> review`, for example `job-search review`.
3. Replace `__DATA__` with the JSON below. **Escape every `</` as `<\/`** inside the JSON so the script tag can't close early.
4. Write the result to `.reviews/<branch-name-with-slashes-as-dashes>.html`. Always use the same path for the same branch, so republishing updates the same link.

```json
{
  "branch": "feature/job-search",
  "base": "main",
  "heading": "Job search",
  "generatedAt": "2026-10-19 14:05",
  "plan": { "found": true, "path": "docs/plans/job-search.md", "goal": "Let users search jobs by title." },
  "summary": "Two or three sentences: what this branch does end to end.",
  "testsAdded": 3,
  "commits": [{ "sha": "a1b2c3d", "message": "Add job search query" }],
  "checks": [{ "label": "No async void", "status": "pass", "detail": "No async void methods in the diff." }],
  "files": [{
    "path": "src/Jobly.Application/Jobs/JobService.cs",
    "layer": "Application",
    "status": "modified",
    "uncommitted": false,
    "generated": false,
    "additions": 24,
    "deletions": 2,
    "why": "Adds the SearchJobs use case the plan asks for.",
    "diff": "@@ -10,6 +10,28 @@ ...unified diff text for this file...",
    "notes": [{ "line": 18, "side": "new", "text": "The token is passed to EF Core so the query stops if the user leaves." }],
    "truncated": null
  }],
  "toCheck": ["Is the search case-insensitive, as the plan says?"]
}
```

`status` is `added`, `modified`, `deleted`, or `renamed`. Order files Domain → Application → Infrastructure → Api → Tests → Client → Docker → Docs → Config.

## 6. Publish

Publish `.reviews/<branch>.html` with the Artifact tool:
- On the first publish, pass `icon: "code"` and a description like `"Explained diff of feature/job-search against main."`
- On later runs, publish the same file path again, which updates the same link.

Publishing uploads the diff to claude.ai, so the user approves each publish.

Then reply with the link and three or four lines: the headline of the change, any `fail` or `warn` checks, and what to look at first.
