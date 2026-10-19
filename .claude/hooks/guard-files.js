// PreToolUse hook for Write, Edit, MultiEdit and NotebookEdit.
// Compares the file before and after the change and prints a permission decision:
//   deny -> the change is blocked and the reason is shown to Claude
//   ask  -> the user must approve the change (every allowed change ends up here)
// The reasons collected below are shown to the user in the approval prompt, so they
// can see at a glance whether the change is ordinary or touches something sensitive.
// See .claude/rules/guardrails.md for the rules this enforces.

const fs = require("fs");
const path = require("path");
const { readInput, projectRoot, isInsideProject, toNativePath, decide } = require("./shared");

const input = readInput();
const toolName = input.tool_name || "";
const toolInput = input.tool_input || {};
const root = projectRoot(input);
const filePath = path.resolve(root, toNativePath(toolInput.file_path || toolInput.notebook_path || ""));
const relativePath = path.relative(root, filePath).replace(/\\/g, "/");
const fileName = path.basename(filePath).toLowerCase();
const fileExists = fs.existsSync(filePath);
const before = fileExists ? fs.readFileSync(filePath, "utf8") : "";

const denyReasons = [];
const askReasons = [];

function applyEdit(content, oldString, newString, replaceAll) {
  switch (true) {
    case typeof oldString !== "string" || oldString.length === 0:
      return content + (newString || "");
    case replaceAll === true:
      return content.split(oldString).join(newString || "");
    default:
      return content.replace(oldString, () => newString || "");
  }
}

function contentAfterChange() {
  switch (toolName) {
    case "Write":
      return String(toolInput.content || "");
    case "Edit":
      return applyEdit(before, toolInput.old_string, toolInput.new_string, toolInput.replace_all);
    case "MultiEdit":
      return (toolInput.edits || []).reduce(
        (content, edit) => applyEdit(content, edit.old_string, edit.new_string, edit.replace_all),
        before
      );
    case "NotebookEdit":
      return before + String(toolInput.new_source || "");
    default:
      return before;
  }
}

const after = contentAfterChange();
const countMatches = (text, pattern) => (text.match(pattern) || []).length;

// 1. Stay inside the project folder.
if (!isInsideProject(filePath, root)) {
  denyReasons.push(`Writing outside the project folder is not allowed (${filePath}).`);
}

// 2. Guardrail files can only change with the user's approval.
if (/^\.claude\/(settings(\.local)?\.json|hooks\/|rules\/)/i.test(relativePath)) {
  askReasons.push("This changes the guardrails themselves and needs your approval.");
}

// 3. Existing migrations are never edited.
if (fileExists && /(^|\/)Migrations\//.test(relativePath)) {
  denyReasons.push("Editing an existing migration is not allowed. Add a new migration instead.");
}

// 4. No secret files.
const isSecretFile =
  (/^\.env(\..+)?$/.test(fileName) && !/\.example$|\.sample$|\.template$/.test(fileName)) ||
  fileName === "secrets.json" ||
  /\.(pfx|p12|pem|key)$/.test(fileName);
if (isSecretFile) {
  denyReasons.push(`Secret files must not be created or edited (${fileName}). Use user secrets or environment variables.`);
}

// 5. No secrets inside file content.
const secretPatterns = [
  [/(Server|Data Source|Host)\s*=[^"\n]*;\s*(Password|Pwd)\s*=\s*[^;"\s{][^;"]*/i, "a connection string with a password"],
  [/(AccountKey|SharedAccessKey)\s*=\s*[A-Za-z0-9+/=]{20,}/i, "a storage or service bus key"],
  [/-----BEGIN [A-Z ]*PRIVATE KEY-----/, "a private key"],
  [/\bAKIA[0-9A-Z]{16}\b/, "an AWS access key"],
  [/\bsk-[A-Za-z0-9_-]{20,}\b/, "an API key"],
  [/\bgh[pousr]_[A-Za-z0-9]{36,}\b/, "a GitHub token"],
  [/\bxox[abposr]-[A-Za-z0-9-]{10,}\b/, "a Slack token"],
];
for (const [pattern, description] of secretPatterns) {
  if (pattern.test(after) && !pattern.test(before)) {
    denyReasons.push(`The new content looks like it contains ${description}. Use user secrets or environment variables.`);
  }
}

// 6. Tests are never skipped, and removing tests needs approval.
const isTestFile = /(^|\/)tests\//i.test(relativePath) || /Tests?\.cs$/i.test(fileName);
if (isTestFile) {
  const skipPattern = /\[\s*(Fact|Theory)\s*\([^\]]*\bSkip\s*=/g;
  if (countMatches(after, skipPattern) > countMatches(before, skipPattern)) {
    denyReasons.push("Skipping tests is not allowed. Fix the code or the test instead.");
  }
  const testPattern = /\[\s*(Fact|Theory)\b/g;
  if (countMatches(after, testPattern) < countMatches(before, testPattern)) {
    askReasons.push("This removes one or more tests and needs your approval.");
  }
}

// 7. New NuGet or npm packages need approval.
switch (true) {
  case /\.(csproj|props)$/i.test(fileName): {
    const packagePattern = /<PackageReference\b|<PackageVersion\b/gi;
    if (countMatches(after, packagePattern) > countMatches(before, packagePattern)) {
      askReasons.push("This adds a NuGet package and needs your approval.");
    }
    break;
  }
  case fileName === "package.json": {
    const dependencyNames = (text) => {
      try {
        const json = JSON.parse(text || "{}");
        return new Set(Object.keys({ ...json.dependencies, ...json.devDependencies, ...json.peerDependencies }));
      } catch {
        return null;
      }
    };
    const beforeNames = dependencyNames(before) || new Set();
    const afterNames = dependencyNames(after);
    switch (true) {
      case afterNames === null:
        askReasons.push("package.json could not be checked for new packages and needs your approval.");
        break;
      case [...afterNames].some((name) => !beforeNames.has(name)):
        askReasons.push("This adds an npm package and needs your approval.");
        break;
      default:
        break;
    }
    break;
  }
  default:
    break;
}

// 8. Every file change needs approval, even an ordinary one.
askReasons.push(`${fileExists ? "Edits" : "Creates"} ${relativePath}.`);

decide(denyReasons, askReasons);
