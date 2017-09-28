#!/bin/bash

# DO NOT USE GlobalAssemblyInfo TO GET VERSION
#
# Because the tag determines the version. AppVeyor patches GlobalAssemblyInfo according
# to the tag.
#
VERSION=$(git describe)
VERSION=$VERSION aiv=$VERSION bundle exec middleman build

