// Self-test for the guardrail hooks. Run from the project root:
//   node .claude/hooks/test-guards.js
// Each case feeds a fake tool call to a hook and checks the decision it prints.

const { spawnSync } = require("child_process");
const path = require("path");

const root = path.resolve(__dirname, "..", "..");
const shellHook = path.join(__dirname, "guard-shell.js");
const filesHook = path.join(__dirname, "guard-files.js");

const shell = (command) => ({ tool_name: "Bash", cwd: root, tool_input: { command } });
const write = (file_path, content) => ({ tool_name: "Write", cwd: root, tool_input: { file_path, content } });
const edit = (file_path, old_string, new_string) => ({ tool_name: "Edit", cwd: root, tool_input: { file_path, old_string, new_string } });

// Built at runtime so this file does not itself trip the secret scanner.
const fakePasswordConnection = `{"C":"Server=.;Database=Jobly;User Id=sa;${"Pass" + "word"}=NotARealSecret1"}`;

const cases = [
  // Shell: allowed (read-only, or build/test/format/run)
  [shellHook, shell("dotnet build"), "none"],
  [shellHook, shell("dotnet test --filter HttpClientTests"), "none"],
  [shellHook, shell("dotnet format"), "none"],
  [shellHook, shell("dotnet run --project src/Jobly.Api"), "none"],
  [shellHook, shell("dotnet build /p:Configuration=Release"), "none"],
  [shellHook, shell(`ls "${path.join(root, "src")}"`), "none"],
  [shellHook, shell("echo hi > /dev/null"), "none"],
  [shellHook, shell("git status && git diff"), "none"],
  [shellHook, shell("git branch"), "none"],
  [shellHook, shell("node .claude/hooks/test-guards.js"), "none"],
  // Shell: the git commands /review-changes uses stay prompt-free
  [shellHook, shell("git diff --stat main...HEAD"), "none"],
  [shellHook, shell("git log --oneline main..HEAD"), "none"],
  [shellHook, shell("git status --porcelain"), "none"],
  [shellHook, shell("git ls-files --others --exclude-standard"), "none"],
  // Shell: anything that may change something needs approval
  [shellHook, shell("mkdir src"), "ask"],
  [shellHook, shell("New-Item -ItemType Directory src"), "ask"],
  [shellHook, shell("dotnet new sln -n Jobly"), "ask"],
  [shellHook, shell("echo hello > notes.txt"), "ask"],
  [shellHook, shell("git branch feature/jobs"), "ask"],
  [shellHook, shell("git checkout -b feature/jobs"), "ask"],
  [shellHook, shell("node some-script.js"), "ask"],
  [shellHook, shell("dotnet run --project src/Other"), "ask"],
  // Shell: Docker compose dev loop runs freely, only with our compose file
  [shellHook, shell("docker compose -f docker/docker-compose.yml up -d --build"), "none"],
  [shellHook, shell("docker compose -f docker/docker-compose.yml logs api"), "none"],
  [shellHook, shell("docker compose -f docker/docker-compose.yml ps"), "none"],
  [shellHook, shell("docker compose -f docker/docker-compose.yml down"), "none"],
  [shellHook, shell("docker compose -f other.yml up"), "ask"],
  [shellHook, shell("docker compose -f docker/docker-compose.yml exec api sh"), "ask"],
  [shellHook, shell("docker run -it ubuntu"), "ask"],
  [shellHook, shell("docker build -f docker/api/Dockerfile ."), "ask"],
  [shellHook, shell("docker compose -f docker/docker-compose.yml down -v"), "deny"],
  [shellHook, shell("docker system prune -a"), "deny"],
  [shellHook, shell("docker volume rm jobly_sql"), "deny"],
  // Shell: SQL needs approval
  [shellHook, shell("sqlcmd -S localhost -Q \"SELECT 1\""), "ask"],
  [shellHook, shell("Invoke-Sqlcmd -Query \"SELECT 1\""), "ask"],
  // Shell: deleting
  [shellHook, shell("rm -rf src"), "deny"],
  [shellHook, shell("dotnet build && rm notes.txt"), "deny"],
  [shellHook, shell("Remove-Item src -Recurse"), "deny"],
  [shellHook, shell("git clean -fd"), "deny"],
  [shellHook, shell("git reset --hard HEAD"), "deny"],
  [shellHook, shell("dotnet ef migrations remove"), "deny"],
  // Shell: network needs approval
  [shellHook, shell("curl https://example.com"), "ask"],
  [shellHook, shell("Invoke-WebRequest https://example.com"), "ask"],
  [shellHook, shell("git pull"), "ask"],
  [shellHook, shell("dotnet restore"), "ask"],
  // Shell: outside the project
  [shellHook, shell("cat /c/Users/someone/.ssh/id_rsa"), "deny"],
  [shellHook, shell("ls ../"), "deny"],
  [shellHook, shell("cd ~ && ls"), "deny"],
  // Shell: git
  [shellHook, shell("git commit -m 'Add job entity'"), "ask"],
  [shellHook, shell("git push origin feature/jobs"), "ask"],
  [shellHook, shell("git push --force"), "deny"],
  [shellHook, shell("git commit --no-verify -m x"), "deny"],
  // Shell: packages and migrations
  [shellHook, shell("dotnet add src/Jobly.Api package Moq"), "ask"],
  [shellHook, shell("npm install"), "ask"],
  [shellHook, shell("dotnet ef migrations add InitialCreate --project src/Jobly.Infrastructure"), "ask"],

  // Files: an ordinary change inside the project is allowed without asking
  [filesHook, write("src/Jobly.Domain/Job.cs", "public sealed class Job {}"), "allow"],
  [filesHook, write("docs/plans/job-search.md", "# Job search"), "allow"],
  [filesHook, write("src/Jobly.Application/Login.cs", "Password = request.Password;"), "allow"],
  [filesHook, write("src/Jobly.Api/appsettings.json", '{"C":"Server=.;Database=Jobly;Trusted_Connection=True"}'), "allow"],
  // Files: denied
  [filesHook, write("C:\\temp\\outside.cs", "x"), "deny"],
  [filesHook, write("../outside.cs", "x"), "deny"],
  [filesHook, write(".env", "KEY=1"), "deny"],
  [filesHook, write("docker/.env.local", "KEY=1"), "deny"],
  [filesHook, write("docker/.env.example", "KEY=change-me"), "allow"],
  [filesHook, write("src/Jobly.Api/appsettings.json", fakePasswordConnection), "deny"],
  [filesHook, write("tests/Jobly.Application.Tests/JobServiceTests.cs", '[Fact(Skip = "later")] public void A() {}'), "deny"],
  // Files: need approval
  [filesHook, write("src/Jobly.Api/Jobly.Api.csproj", '<Project><ItemGroup><PackageReference Include="Moq" /><PackageReference Include="Another" /></ItemGroup></Project>'), "ask"],
  [filesHook, write("client/package.json", '{"dependencies":{"axios":"1.7.0"}}'), "ask"],
  [filesHook, edit(".claude/settings.json", '"hooks"', '"hooks"'), "ask"],
];

let failures = 0;
for (const [hook, input, expected] of cases) {
  const result = spawnSync(process.execPath, [hook], {
    input: JSON.stringify(input),
    env: { ...process.env, CLAUDE_PROJECT_DIR: root },
    encoding: "utf8",
  });
  let actual = "none";
  let reason = "";
  switch (true) {
    case result.status !== 0:
      actual = `crash (exit ${result.status}): ${result.stderr.trim()}`;
      break;
    case result.stdout.trim().length > 0: {
      const output = JSON.parse(result.stdout).hookSpecificOutput;
      actual = output.permissionDecision;
      reason = output.permissionDecisionReason;
      break;
    }
    default:
      break;
  }
  const passed = actual === expected;
  failures += passed ? 0 : 1;
  const label = input.tool_input.command || `${input.tool_name} ${input.tool_input.file_path}`;
  console.log(`${passed ? "PASS" : "FAIL"}  ${expected.padEnd(5)} ${label}${passed ? "" : `\n      got: ${actual} ${reason}`}`);
}

console.log(`\n${cases.length - failures}/${cases.length} passed`);
process.exit(failures === 0 ? 0 : 1);
