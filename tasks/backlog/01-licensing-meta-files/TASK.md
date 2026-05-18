# Licensing & repo meta files

## Goal
Add the standard public-repo meta files (LICENSE, SECURITY, CONTRIBUTING, PR template, CODEOWNERS, CHANGELOG skeleton) so the template is legally forkable, has a clear vulnerability-reporting channel, and surfaces the "health" badges GitHub checks for.

## Scope
- Add `LICENSE` (MIT, copyright holder = repo owner) at the repo root.
- Add `SECURITY.md` at the repo root (or under `.github/`) with a vulnerability-disclosure contact and a supported-versions note.
- Add `CONTRIBUTING.md` at the repo root with branch policy, commit-message style, and the standard pre-commit checks (`dotnet build`, `dotnet test`, `dotnet format`).
- Add `.github/PULL_REQUEST_TEMPLATE.md` with a contributor checklist (build green, tests added, format applied, screenshots for UI).
- Add `.github/CODEOWNERS` mapping `*` to the maintainer.
- Add `CHANGELOG.md` at the repo root following the Keep a Changelog 1.1.0 format, with an `[Unreleased]` section and an initial `0.1.0` entry placeholder.

Out of scope:
- Adding `<VersionPrefix>` to `Directory.Build.props` (owned by task 02).
- Adding issue templates (`bug_report.yml`, `feature_request.yml`) — leave for a follow-up; PR template is enough for now.
- Updating README to link the new files (owned by task 10).
- `CODE_OF_CONDUCT.md` (optional; defer).
- `FUNDING.yml` (optional; defer).

## Edit zone
- `LICENSE` (new)
- `SECURITY.md` (new)
- `CONTRIBUTING.md` (new)
- `CHANGELOG.md` (new)
- `.github/PULL_REQUEST_TEMPLATE.md` (new)
- `.github/CODEOWNERS` (new)

## Independence guarantee
- Every file in the Edit zone is new — no existing file is modified, so no other task can collide.
- The CONTRIBUTING.md `dotnet format --verify-no-changes` instruction is correct whether or not task 02 has shipped `.editorconfig`; without it the check is a no-op, with it the check enforces style.
- The CHANGELOG `0.1.0` entry is a placeholder with no link to `<VersionPrefix>`; task 02 may later add the version property, but this task does not require it.
- If task 10 ships first and links to `LICENSE` / `SECURITY.md` from the README, those links resolve cleanly once this task lands.

This task may sit in `backlog/` for weeks. By the time it is picked up, the surrounding tree may have drifted from the snapshot the audit captured. Handle the three drift modes explicitly:

- **File already changed by another task.** Before creating any meta file, run `ls` / `git ls-files` to check whether it already exists. If it does, read it, see whether its intent matches this task, and *merge additively* (add missing sections, do not rewrite content another task put there). Never silently overwrite.
- **File moved/renamed.** If `SECURITY.md` has been placed under `.github/` instead of the root (or vice versa), keep the existing location and only adjust content. Locate by name search (`git ls-files | grep -i security`), not by hardcoded path.
- **Prerequisite work already done.** If any meta file already exists with content that satisfies this task's intent (license recognised by GitHub, security disclosure channel present, etc.), skip that step and note it in the PR description rather than re-shipping it.

### If you find related work already started
- Don't undo what's there if its intent matches this task — verify the file already meets the acceptance criterion and move on.
- If intent conflicts (e.g. someone shipped an Apache-2.0 LICENSE and this task assumed MIT), surface the conflict in the PR description; don't silently overwrite.
- Coordination happens via the PR description and the existing sticky CI comment, not via blocking dependencies between tasks.

## Steps
1. **Verify current state first.** Before editing or creating anything, list the repo root and `.github/`, and read any of the target meta files that already exist. The snippets below describe the *intent* of each file; apply that intent to whatever is on disk today. If a file already exists and already meets the acceptance criterion, skip it and note it in the PR description. If a target path has moved, locate the new home via `git ls-files` rather than re-creating at the old path.
2. Determine the copyright holder (the repo owner / org). Default to the git config user if unclear: `git config user.name`.
3. Write `LICENSE` using the [SPDX MIT template](https://spdx.org/licenses/MIT.html). Year = current year. Holder = step 2.
4. Write `SECURITY.md` with:
   - One-sentence intro ("We take security seriously…").
   - "Supported versions" table (just `main` for now).
   - "Reporting a vulnerability" section pointing to GitHub Security Advisories or a maintainer email; explicitly say "do not file public issues for security reports".
5. Write `CONTRIBUTING.md` covering:
   - Branch model (PRs against `main`).
   - Commit-message style (Conventional Commits — the repo already uses `fix:`, `perf:`, `feat:` prefixes).
   - Required pre-commit checks: `dotnet build`, `dotnet test`, `dotnet format --verify-no-changes`.
   - How to run E2E tests locally (`pwsh tests/CopilotBlazorTemplate.E2ETests/bin/Debug/net10.0/playwright.ps1 install --with-deps chromium` then `dotnet test`).
   - How to update lockfiles when adding a NuGet package.
6. Write `.github/PULL_REQUEST_TEMPLATE.md` with sections: **Summary**, **Changes**, **Checklist** (build green, tests added/updated, `dotnet format` applied, docs updated if user-facing, screenshots for UI changes).
7. Write `.github/CODEOWNERS` with a single line: `* @<repo-owner>` (replace `<repo-owner>` with the actual GitHub login; default to the value of `git config user.name` or the org name in the repo URL).
8. Write `CHANGELOG.md` following Keep a Changelog 1.1.0:
   ```markdown
   # Changelog

   All notable changes to this project will be documented in this file.

   The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
   and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

   ## [Unreleased]

   ## [0.1.0] - <YYYY-MM-DD>
   ### Added
   - Initial template scaffold.
   ```

## Acceptance criteria
Expressed as outcomes, not exact file contents — if a file already meets the outcome (because another task shipped it), the box is ticked, regardless of who wrote it.

- [ ] The repo carries a recognised open-source LICENSE at a path GitHub indexes (root or `.github/`); the license badge appears in the repo sidebar after merge.
- [ ] There is a published vulnerability-reporting channel reachable from a `SECURITY.md` or `.github/SECURITY.md` — explicitly NOT "open a public issue".
- [ ] A contributor reading `CONTRIBUTING.md` can find the three pre-commit checks (build, test, format) without scrolling past the fold.
- [ ] New PRs surface a checklist prompt (PR template renders on the compose page).
- [ ] A `CHANGELOG.md` exists with an `[Unreleased]` section, in whatever format the repo has converged on (Keep a Changelog is the default if no other format is already present).
- [ ] `CODEOWNERS` routes review requests to a real handle.
- [ ] `dotnet build` and `dotnet test` still pass (sanity check — no source code is touched by this task).

## References
- Audit cross-cutting theme CC-8: `../../../docs/audits/2026-05-18/REPORT.md` (Missing standard repo meta files).
- Repo Organization finding "Missing LICENSE file", "Missing `SECURITY.md` and `CONTRIBUTING.md`", "No issue or PR templates", "No `FUNDING.yml` / no `.github/CODEOWNERS`", "No `CHANGELOG.md` / versioning conventions": `../../../docs/audits/2026-05-18/02-repository-organization.md`.
- Security finding "Missing SECURITY.md" (CC-8 echo): `../../../docs/audits/2026-05-18/03-security.md`.
- Keep a Changelog 1.1.0 — <https://keepachangelog.com/en/1.1.0/>
- SPDX MIT — <https://spdx.org/licenses/MIT.html>
