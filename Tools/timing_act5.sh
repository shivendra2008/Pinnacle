#!/bin/bash
# Matched Act V runs of two player builds (Docs/ACTS_4_5.md section 3): the act5 route (18 pieces of evidence, the
# Justice ending, lines skipped) at 1920x1080, before and after alternating, each after 60 s idle and once the 1-minute
# load average is under 3.5, as Tools/timing_pairs.sh does. No capture or stills. Then the per-section statistics.
#   Tools/timing_act5.sh <before.app> <after.app> <outdir> [pairs=3]
HERE="$(cd "$(dirname "$0")" && pwd)"
BASE="$1"; AFTER="$2"; R="$3"; PAIRS=${4:-3}
mkdir -p "$R"
power() { pmset -g batt | head -1 | grep -q "AC Power" && echo "mains" || echo "battery"; }
level() { pmset -g batt | grep -o '[0-9]*%' | head -1 | tr -d '%'; }
settle() {
  local k l
  sleep 60
  for k in $(seq 48); do
    l=$(sysctl -n vm.loadavg | awk '{print $2}')
    awk "BEGIN{exit !($l < 3.5)}" && break
    sleep 5
  done
  echo "  load $(sysctl -n vm.loadavg | awk '{print $2}') at $(date +%H:%M:%S), $(power) $(level)%"
}
for pair in $(seq $PAIRS); do
  if [ "$(power)" = battery ] && [ "$(level)" -lt 12 ]; then echo "battery at $(level)%: stopping after $((pair-1)) pairs"; break; fi
  for kind in base after; do
    APP="$BASE"; [ $kind = after ] && APP="$AFTER"
    settle; "$HERE/route_run.sh" "$APP" "$R/act5_${kind}_$pair" 1920 1080 PINNACLE_ROUTE=act5 PINNACLE_ROUTE_CASE=j18 PINNACLE_ROUTE_ENDING=0 PINNACLE_ROUTE_FAST=1 > /dev/null 2>&1; echo "act5 $kind $pair done"
  done
done
python3 "$HERE/act5_timing.py" "$R"/act5_*
