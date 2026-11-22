#!/bin/bash
# Runs one PlayRoute route in a player build and waits for it (Assets/Scripts/Playtest/PlayRoute.cs, Docs/PLAYTEST.md).
#   Tools/route_run.sh <app> <outdir> <width> <height> PINNACLE_ROUTE=market [VAR=VAL ...]
# The run's files (frames.csv, summary.txt, checks.txt, player.log, stills, capture) go to <outdir>, which is emptied
# first; its saves go to <outdir>/saves so a route never touches your own save. A route that hangs is stopped after
# 15 minutes.
APP="$1"; OUT="$2"; W="$3"; H="$4"; shift 4
rm -rf "$OUT"; mkdir -p "$OUT/saves"
env PINNACLE_PROFILE_OUT="$OUT" PINNACLE_SAVE_DIR="$OUT/saves" "$@" "$APP/Contents/MacOS/Pinnacle" -screen-fullscreen 0 -screen-width "$W" -screen-height "$H" -logFile "$OUT/player.log" >/dev/null 2>&1 &
PID=$!
# the watchdog writes nowhere, so it never holds the caller's pipe open
( for i in $(seq 900); do sleep 1; kill -0 $PID 2>/dev/null || exit 0; done; kill $PID ) >/dev/null 2>&1 &
wait $PID; RC=$?
echo "route exit $RC -> $OUT"
grep -E "^(PASS|FAIL|NOTE)" "$OUT/checks.txt" 2>/dev/null | head -80
