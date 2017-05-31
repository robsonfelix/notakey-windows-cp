#!/bin/bash

./build_nix.sh
bundle exec middleman s3_sync

