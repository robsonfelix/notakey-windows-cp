#!/bin/bash

cd "$(dirname "$0")"

./build_nix.sh
bundle exec middleman s3_sync

