#!/bin/bash

# Use this to get full version
# VERSION=$(cat ../../GlobalAssemblyInfo.cs | grep AssemblyVe | cut -c 29- | rev | cut -c 4- | rev)

# This drops the last 2 dot-separated values
VERSION=$(cat ../../GlobalAssemblyInfo.cs | grep AssemblyVe | cut -c 29- | rev | cut -c 4- | rev | sed 's/\.[^.]*\.[^.]*$//')
VERSION=$VERSION aiv=$VERSION bundle exec middleman build

