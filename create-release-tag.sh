#!/bin/bash

set -o errexit #abort if any command fails

function ensure_git_clean {
  echo "==> Verifying repository is clean"
  if [[ $(git status --porcelain) ]]; then
    echo "ERROR: repository not in a clean state"
    exit 1
  fi

  echo "==> Verifying local branch is not ahead of remote"
  GITBRANCH=$(git rev-parse --abbrev-ref HEAD)
  GITAHEAD="$(git log origin/$GITBRANCH..$GITBRANCH)"
  if [ "$?" != "0" ]; then
    echo "ERROR: failed to verify against upstream branch $GITBRANCH"
    exit 1
  fi

  if [ "$GITBRANCH" != "master" ]; then
    echo "ERROR: it appears you are on a non-master branch. This is not allowed"
    exit 1
  fi

  if [ ! -z "$GITAHEAD" ]; then
    echo "ERROR: it appears you have git changes that have not been pushed upstream. Building not allowed."
    exit 1
  fi
}

ensure_git_clean

OLDVERSION=$(cat VERSION)
if [ -z "$1" ]; then
  NEWVERSION=$(./increment_version.sh -p $OLDVERSION)
else
  NEWVERSION="$1"
fi

echo "Old version: $OLDVERSION"
echo "New version: $NEWVERSION"

EXPECTED_CHANGELOG="changelogs/$NEWVERSION.md"
if [ ! -f "$EXPECTED_CHANGELOG" ]; then
  echo "ERROR: changelog not found (expected file $EXPECTED_CHANGELOG)"
  echo ""
  read -p "Press any key to view commits between $OLDVERSION and HEAD ... "
  ./changelog.sh $OLDVERSION
  echo ""
  echo "Please use this information to write a changelog, and place it in ./$EXPECTED_CHANGELOG"
  echo "(use './changelog.sh $OLDVERSION' to see the commits since the previous version)"
  exit 1
fi

echo "=> Validating release hierarchy"
if [ -z "$(git describe | grep $OLDVERSION)" ]; then
  echo "ERROR: Can not increment ('git describe' indicates state is not directly descendent from previous version or release candidate version)"
  exit 1
else
  echo "Direct descendant from previous version"
fi

if git rev-parse $NEWVERSION >/dev/null 2>&1; then
  echo "ERROR: New version tag exists already"
  exit 1
fi

ASSEMBLYVERSION="$NEWVERSION.0"

# appveyor build will take build version from assembly version and check it against tag
sed -i -E "s/\(Version(\"\)\([0-9]*\.\)\{1,\}\([0-9]*\)\")/Version(\"$ASSEMBLYVERSION\")/g" GlobalAssemblyInfo.cs 
rm GlobalAssemblyInfo.cs-E

echo $NEWVERSION > VERSION
GIT_MESSAGE="Version bump from $OLDVERSION to $NEWVERSION"

 git commit VERSION GlobalAssemblyInfo.cs -m "$GIT_MESSAGE"
 git push

 git tag -a "$NEWVERSION" -m "$GIT_MESSAGE"
 git push --tags

echo "=> DONE"
echo ""
echo ""
echo "New git tag pushed: $NEWVERSION with message \"$GIT_MESSAGE\""
echo ""

echo "=> Publishing the latest documentation"
docs/manual/publish.sh


