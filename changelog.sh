#!/bin/bash

OLDVERSION="$1"
NEWVERSION="$2"

if [ -z "$OLDVERSION" ]; then
  OLDVERSION=$(cat VERSION)
fi

if [ -z "$NEWVERSION" ]; then
  NEWVERSION="HEAD"
fi

git log --pretty=oneline --abbrev-commit $OLDVERSION..$NEWVERSION

