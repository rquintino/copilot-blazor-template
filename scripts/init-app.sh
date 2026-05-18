#!/usr/bin/env bash
# Rename this template in place to a new app name.
# Usage: scripts/init-app.sh <NewName>     e.g. scripts/init-app.sh Acme.Shop
# Operates on the filesystem only. Does NOT touch git — review and commit yourself.

set -euo pipefail

OLD="CopilotBlazorTemplate"

if [[ $# -ne 1 ]]; then
  echo "usage: $0 <NewName>   (e.g. Acme.Shop)" >&2
  exit 2
fi
NEW="$1"

if ! [[ "$NEW" =~ ^[A-Z][A-Za-z0-9]*(\.[A-Z][A-Za-z0-9]*)*$ ]]; then
  echo "error: '$NEW' is not a valid .NET-style identifier (Pascal segments, dot-separated)." >&2
  echo "       examples: Acme, Acme.Shop, Contoso.Web.Portal" >&2
  exit 2
fi

if [[ "$NEW" == "$OLD" ]]; then
  echo "error: new name is identical to the template name; nothing to do." >&2
  exit 2
fi

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$REPO_ROOT"

EXCLUDES=( -not -path './.git/*' -not -path '*/bin/*' -not -path '*/obj/*' -not -path '*/node_modules/*' -not -path '*/.vs/*' -not -path '*/.idea/*' )
# This script must not rewrite itself (it contains the literal token by necessity).
SELF_BASENAME="$(basename "$0")"

# Idempotency guard
if ! grep -rIlq --exclude-dir={.git,bin,obj,node_modules,.vs,.idea} --exclude="$SELF_BASENAME" "$OLD" . 2>/dev/null \
   && ! find . -name "*${OLD}*" "${EXCLUDES[@]}" -print -quit | grep -q .; then
  echo "error: no occurrences of '$OLD' found — looks like this template was already renamed." >&2
  exit 1
fi

echo ">> Renaming '$OLD' -> '$NEW'"

echo "   [1/4] renaming directories (deepest first)"
while IFS= read -r d; do
  base="$(basename "$d")"
  parent="$(dirname "$d")"
  new_base="${base//$OLD/$NEW}"
  mv "$d" "$parent/$new_base"
done < <(find . -depth -type d -name "*${OLD}*" "${EXCLUDES[@]}")

echo "   [2/4] renaming files"
while IFS= read -r f; do
  base="$(basename "$f")"
  parent="$(dirname "$f")"
  new_base="${base//$OLD/$NEW}"
  mv "$f" "$parent/$new_base"
done < <(find . -type f -name "*${OLD}*" "${EXCLUDES[@]}")

echo "   [3/4] rewriting file contents"
# -I skips binaries; --exclude-dir avoids build/IDE noise.
mapfile -t TO_REWRITE < <(grep -rIl --exclude-dir={.git,bin,obj,node_modules,.vs,.idea} --exclude="$SELF_BASENAME" "$OLD" . || true)
if [[ ${#TO_REWRITE[@]} -gt 0 ]]; then
  NEW="$NEW" perl -i -pe 's/\Q'"$OLD"'\E/$ENV{NEW}/g' "${TO_REWRITE[@]}"
fi

echo "   [4/4] regenerating EF Core InitialCreate migration"
NEW_CORE="src/${NEW}.Core"
NEW_WEB="src/${NEW}.Web"
MIGRATIONS_DIR="$NEW_CORE/Migrations"
if [[ -d "$MIGRATIONS_DIR" ]]; then
  if ! command -v dotnet >/dev/null 2>&1; then
    echo "warning: 'dotnet' not on PATH — skipping migration regen. Run later:" >&2
    echo "         rm -rf $MIGRATIONS_DIR && dotnet ef migrations add InitialCreate -p $NEW_CORE -s $NEW_WEB -o Migrations" >&2
  elif ! dotnet ef --version >/dev/null 2>&1; then
    echo "warning: 'dotnet-ef' tool not installed — skipping migration regen. Install with:" >&2
    echo "         dotnet tool install --global dotnet-ef" >&2
  else
    rm -rf "$MIGRATIONS_DIR"
    dotnet ef migrations add InitialCreate \
      --project "$NEW_CORE" \
      --startup-project "$NEW_WEB" \
      --output-dir Migrations
  fi
fi

echo ">> Smoke build"
if command -v dotnet >/dev/null 2>&1; then
  dotnet build
else
  echo "warning: 'dotnet' not on PATH — skipping smoke build."
fi

cat <<EOF

Done. Template renamed to '$NEW'.

Next steps:
  1. git status                          # review the rename
  2. git diff --stat                     # spot-check what changed
  3. dotnet test                         # confirm the suite is green
  4. git add -A && git commit -m "chore: initialize app as $NEW"
  5. git remote set-url origin <your-repo-url>   # when ready to push
EOF
