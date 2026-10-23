// PreToolUse hook for Bash and PowerShell commands.
// Reads the tool call as JSON on stdin and prints a permission decision:
//   deny -> the command is blocked and the reason is shown to Claude
//   ask  -> the user must approve the command
//   (no output) -> the command is on the safe list and runs
//
// Default is "ask": only commands known to be read-only (plus build/test/format/run)
// run without approval. Anything else might change something, so the user decides.
// See .claude/rules/guardrails.md for the rules this enforces.

const path = require("path");
const { readInput, projectRoot, isInsideProject, toNativePath, decide } = require("./shared");

const input = readInput();
const command = String((input.tool_input && input.tool_input.command) || "");
const root = projectRoot(input);
const cwd = input.cwd || root;

const denyReasons = [];
const askReasons = [];

// Split a command line into simple commands so each program can be checked on its own.
function splitIntoSegments(text) {
  return text
    .split(/\r?\n|;|&&|\|\||\||\$\(|`|\(|\)|\{|\}/)
    .map((segment) => segment.trim())
    .filter((segment) => segment.length > 0);
}

function wordsOf(segment) {
  // Drop leading VAR=value assignments and the PowerShell call operator "&".
  const words = segment.split(/\s+/).map((word) => word.replace(/^["']|["']$/g, ""));
  while (words.length > 0 && (/^\w+=/.test(words[0]) || words[0] === "&")) words.shift();
  return words;
}

const deletePrograms = ["rm", "rmdir", "del", "erase", "rd", "unlink", "shred", "remove-item", "ri", "clear-content", "clc"];
const networkPrograms = [
  "curl", "wget", "invoke-webrequest", "iwr", "invoke-restmethod", "irm", "ssh", "scp", "sftp",
  "ftp", "telnet", "nc", "ncat", "start-bitstransfer", "certutil", "bitsadmin", "ping", "nslookup",
];
// Database command-line tools: PostgreSQL's (psql and friends) and the old SQL Server ones.
const sqlPrograms = ["psql", "pg_dump", "pg_restore", "createdb", "dropdb", "sqlcmd", "osql", "bcp", "invoke-sqlcmd", "sqlpackage"];

// Programs that only read or display things.
const readOnlyPrograms = [
  "ls", "dir", "gci", "get-childitem", "cat", "type", "gc", "get-content", "head", "tail", "wc",
  "pwd", "get-location", "echo", "write-output", "grep", "rg", "select-string", "sls", "test-path",
  "get-item", "which", "where", "where.exe", "get-command", "sort", "uniq", "tree", "measure-object",
  "select-object", "where-object", "format-table", "format-list", "ft", "fl", "cd", "set-location",
  "sl", "pushd", "popd", "true",
];

// Returns true when a segment is known to be safe to run without asking.
function isSafeSegment(words) {
  const program = path.basename((words[0] || "").toLowerCase()).replace(/\.exe$/, "");
  const subcommand = (words[1] || "").toLowerCase();
  const rest = words.slice(1).join(" ");
  switch (program) {
    case "dotnet":
      switch (subcommand) {
        case "build":
        case "test":
        case "format":
        case "--info":
        case "--version":
        case "--list-sdks":
        case "--list-runtimes":
          return true;
        case "run":
          // Only the API project may be run without asking.
          return /--project\s+["']?\.?[\\/]?src[\\/]Jobly\.Api\b/i.test(rest);
        default:
          return false;
      }
    case "git":
      switch (subcommand) {
        case "status":
        case "log":
        case "diff":
        case "show":
        case "rev-parse":
        case "ls-files":
        case "blame":
          return true;
        case "branch":
          // Listing branches is safe; "git branch <name>" creates one.
          return words.slice(2).every((word) => /^(-a|-l|-v|-vv|-r|--list|--all|--show-current)$/.test(word));
        case "remote":
          return words.slice(2).every((word) => word === "-v");
        default:
          return false;
      }
    case "node":
      // Only the guardrail self-test runs without asking. Any other script could do anything.
      return /^\.?[\\/]?\.claude[\\/]hooks[\\/]test-guards\.js$/.test(words[1] || "") && words.length === 2;
    case "find":
      return !/\s-(exec|execdir|ok|delete)\b/.test(rest);
    case "docker": {
      // Docker is the main dev loop, so compose up/down/logs/ps run freely,
      // but only against our own compose file. Everything else asks.
      if (subcommand !== "compose") return false;
      const args = words.slice(2);
      const fileIndex = args.findIndex((word) => word === "-f" || word === "--file");
      const composeFile = fileIndex >= 0 ? (args[fileIndex + 1] || "").replace(/\\/g, "/").replace(/^\.\//, "") : "";
      if (composeFile !== "docker/docker-compose.yml") return false;
      const remaining = args.filter((_, index) => index !== fileIndex && index !== fileIndex + 1);
      switch ((remaining[0] || "").toLowerCase()) {
        case "up":
        case "logs":
        case "ps":
          return true;
        case "down":
          // "down -v" deletes the database volume; that is denied separately below.
          return !remaining.some((word) => /^(-v|--volumes|--rmi)$/.test(word));
        default:
          return false;
      }
    }
    default:
      return readOnlyPrograms.includes(program);
  }
}

for (const segment of splitIntoSegments(command)) {
  const words = wordsOf(segment);
  const program = path.basename((words[0] || "").toLowerCase()).replace(/\.exe$/, "");
  switch (true) {
    case program.length === 0:
      break;
    case deletePrograms.includes(program):
      denyReasons.push(`Deleting files is not allowed (\`${program}\`).`);
      break;
    case networkPrograms.includes(program):
      askReasons.push(`This uses the network and needs your approval (\`${program}\`).`);
      break;
    case sqlPrograms.includes(program):
      askReasons.push(`This accesses the database and needs your approval (\`${program}\`).`);
      break;
    case isSafeSegment(words):
      break;
    default:
      askReasons.push(`\`${words.slice(0, 3).join(" ")}\` is not on the read-only list and may change something.`);
      break;
  }
}

// Writing command output into a file changes that file.
if (/(^|[^\d&>])>{1,2}\s*(?!\/dev\/null\b|\$null\b|nul\b|&)\S/i.test(command)) {
  askReasons.push("This redirects output into a file.");
}

// Multi-word patterns checked against the whole command line.
const denyPatterns = [
  [/\bgit\s+clean\b/i, "`git clean` deletes untracked files."],
  [/\bgit\s+rm\b/i, "`git rm` deletes files."],
  [/\bgit\s+reset\s+--hard\b/i, "`git reset --hard` discards work."],
  [/\bgit\s+checkout\s+(\S+\s+)?--\s/i, "`git checkout --` discards changes."],
  [/\bgit\s+restore\b/i, "`git restore` discards changes."],
  [/\bgit\s+stash\s+(drop|clear)\b/i, "Dropping stashes deletes work."],
  [/\bgit\s+branch\s+(-D|-d|--delete)\b/, "Deleting branches is not allowed."],
  [/\bgit\s+push\b.*(--force\b|-f\b|--force-with-lease\b)/i, "Force-pushing is not allowed."],
  [/--no-verify\b/i, "Skipping git hooks (`--no-verify`) is not allowed."],
  [/\bdotnet\s+ef\s+migrations\s+remove\b/i, "Removing migrations is not allowed. Add a new migration instead."],
  [/\bdotnet\s+ef\s+database\s+drop\b/i, "Dropping the database is not allowed."],
  [/\bfind\b.*\s-delete\b/i, "`find -delete` deletes files."],
  [/\bfind\b.*-exec\s+rm\b/i, "`find -exec rm` deletes files."],
  [/\bStart-Process\b/i, "Starting arbitrary processes is not allowed."],
  [/\bdocker\s+compose\b.*\bdown\b.*(\s-v\b|--volumes\b)/i, "`docker compose down -v` deletes the database volume."],
  [/\bdocker\s+(system|volume|image|container|builder|network)\s+prune\b/i, "Pruning deletes Docker data."],
  [/\bdocker\s+(volume\s+rm|image\s+rm|container\s+rm|rm|rmi)\b/i, "Removing Docker containers, images, or volumes is not allowed."],
];

// Specific reasons for common commands that need approval (shown to you in the prompt).
const askPatterns = [
  [/\bgit\s+(clone|fetch|pull)\b/i, "This uses the network (git clone/fetch/pull)."],
  [/\bgit\s+commit\b/i, "Committing needs your approval."],
  [/\bgit\s+push\b/i, "Pushing uses the network and needs your approval."],
  [/\bgit\s+(merge|rebase)\b/i, "Merging or rebasing needs your approval."],
  [/\bdotnet\s+(add\b.*\bpackage\b|package\s+add\b)/i, "Adding a NuGet package needs your approval."],
  [/\bdotnet\s+(remove|package\s+remove)\b/i, "Removing a package or project reference needs your approval."],
  [/\bdotnet\s+(restore|tool\s+(install|update))\b/i, "This downloads packages from the network."],
  [/\bdotnet\s+ef\b/i, "EF Core migrations and database commands need your approval."],
  [/\bdotnet\s+user-secrets\b/i, "Changing user secrets needs your approval."],
  [/\b(Install-Package|nuget\s+install)\b/i, "Adding a NuGet package needs your approval."],
  [/\bnpm\s+(install|i|add|ci|update|uninstall|remove)\b/i, "npm commands that change packages use the network and need your approval."],
  [/\b(yarn|pnpm)\s+(add|install|remove)\b/i, "Package manager commands need your approval."],
  [/\bnpx\b/i, "`npx` downloads and runs packages and needs your approval."],
  [/\bNew-Object\s+(System\.)?Net\.WebClient\b|\[System\.Net\.Http\.HttpClient\]/i, "This uses the network."],
];

for (const [pattern, reason] of denyPatterns) {
  if (pattern.test(command)) denyReasons.push(reason);
}
for (const [pattern, reason] of askPatterns) {
  if (pattern.test(command)) askReasons.push(reason);
}

// Paths that point outside the project folder.
const tokens = command.match(/"[^"]*"|'[^']*'|\S+/g) || [];
for (const rawToken of tokens) {
  const token = rawToken.replace(/^["']|["']$/g, "").replace(/^-[^=:]*[=:]/, "");
  const looksAbsolute =
    /^[A-Za-z]:[\\/]/.test(token) ||
    /^\/[A-Za-z]\//.test(token) ||
    /^\/(tmp|etc|usr|home|mnt|var|opt|root|bin|proc)(\/|$)/.test(token);
  const pointsAtHome = /^(\$HOME|\$env:|%USERPROFILE%|%APPDATA%|%TEMP%|~(\/|\\|$))/i.test(token);
  const climbsUp = /(^|[\\/])\.\.([\\/]|$)/.test(token);

  switch (true) {
    case token === "/dev/null":
      break;
    case pointsAtHome:
      denyReasons.push(`Paths outside the project folder are not allowed (\`${token}\`).`);
      break;
    case looksAbsolute && !isInsideProject(toNativePath(token), root):
      denyReasons.push(`Paths outside the project folder are not allowed (\`${token}\`).`);
      break;
    case climbsUp && !isInsideProject(path.resolve(toNativePath(cwd), toNativePath(token)), root):
      denyReasons.push(`Paths outside the project folder are not allowed (\`${token}\`).`);
      break;
    default:
      break;
  }
}

decide(denyReasons, askReasons);
