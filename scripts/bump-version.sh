#!/usr/bin/env bash
set -euo pipefail

# Usage: ./scripts/bump-version.sh 2.0.0-alpha.2

if [ -z "${1:-}" ]; then
  echo "Usage: $0 <version>"
  echo "Example: $0 2.0.0-alpha.2"
  exit 1
fi

VERSION="$1"
CSPROJ="src/Allow2.Service/Allow2.Service.csproj"

if [ ! -f "$CSPROJ" ]; then
  echo "Error: $CSPROJ not found. Run from the project root."
  exit 1
fi

# Update version in .csproj
sed -i "s|<Version>.*</Version>|<Version>${VERSION}</Version>|" "$CSPROJ"

echo "Version bumped to ${VERSION} in ${CSPROJ}"
echo ""
echo "Next steps:"
echo "  git add ${CSPROJ}"
echo "  git commit -m 'Bump version to ${VERSION}'"
echo "  git tag v${VERSION}"
