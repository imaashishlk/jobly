// Helpers shared by the guardrail hooks.

const fs = require("fs");
const path = require("path");

function readInput() {
  const raw = fs.readFileSync(0, "utf8");
  return raw.trim().length > 0 ? JSON.parse(raw) : {};
}

// Converts Git Bash style paths (/d/Projects/...) to Windows paths (d:\Projects\...).
function toNativePath(value) {
  const text = String(value || "");
  const gitBashDrive = text.match(/^\/([A-Za-z])(\/.*)?$/);
  switch (true) {
    case gitBashDrive !== null && process.platform === "win32":
      return `${gitBashDrive[1]}:${(gitBashDrive[2] || "/").replace(/\//g, "\\")}`;
    default:
      return text;
  }
}

function projectRoot(input) {
  return path.resolve(toNativePath(process.env.CLAUDE_PROJECT_DIR || input.cwd || process.cwd()));
}

function normalize(value) {
  const resolved = path.resolve(toNativePath(value));
  return process.platform === "win32" ? resolved.toLowerCase() : resolved;
}

function isInsideProject(candidate, root) {
  const normalizedCandidate = normalize(candidate);
  const normalizedRoot = normalize(root);
  return normalizedCandidate === normalizedRoot || normalizedCandidate.startsWith(normalizedRoot + path.sep);
}

// Prints the decision in the format Claude Code expects. Deny wins over ask.
function decide(denyReasons, askReasons) {
  const unique = (reasons) => [...new Set(reasons)];
  let decision = null;
  let reasons = [];
  switch (true) {
    case denyReasons.length > 0:
      decision = "deny";
      reasons = unique(denyReasons);
      break;
    case askReasons.length > 0:
      decision = "ask";
      reasons = unique(askReasons);
      break;
    default:
      break;
  }
  if (decision === null) return;
  process.stdout.write(
    JSON.stringify({
      hookSpecificOutput: {
        hookEventName: "PreToolUse",
        permissionDecision: decision,
        permissionDecisionReason: `[Jobly guardrail] ${reasons.join(" ")} See .claude/rules/guardrails.md.`,
      },
    })
  );
}

module.exports = { readInput, toNativePath, projectRoot, isInsideProject, decide };
