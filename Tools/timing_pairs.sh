#!/bin/bash
# Matched frame-time runs of two player builds (Docs/CINEMATIC_PASS.md section 8): the market and the café routes at
# 1920x1080, before and after alternating, each run after at least 60 s idle and once the 1-minute load average is
# under 3.5 (this fanless M1 throttles over consecutive runs). No capture or stills. On battery a pair starts only above
# 12% so pairs stay whole. Then prints the per-run and pooled statistics.
#   Tools/timing_pairs.sh <before.app> <after.app> <outdir> [pairs=3]
HERE="$(cd "$(dirname "$0")" && pwd)"
BASE="$1"; AFTER="$2"; R="$3"; PAIRS=${4:-3}
mkdir -p "$R"
power() { pmset -g batt | head -1 | grep -q "AC Power" && echo "mains" || echo "battery"; }
level() { pmset -g batt | grep -o '[0-9]*%' | head -1 | tr -d '%'; }
settle() {
  local k l      # (its own counter: sharing the pair loop's variable saved pair 2 under pair 1's names)
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
    settle; "$HERE/route_run.sh" "$APP" "$R/market_${kind}_$pair" 1920 1080 PINNACLE_ROUTE=market PINNACLE_ROUTE_TRUST=note PINNACLE_ROUTE_OUTCOME=hide > /dev/null 2>&1; echo "market $kind $pair done"
    settle; "$HERE/route_run.sh" "$APP" "$R/cafe_${kind}_$pair" 1920 1080 PINNACLE_ROUTE=cafe > /dev/null 2>&1; echo "cafe $kind $pair done"
  done
done
python3 "$HERE/route_timing.py" "$R"/market_* "$R"/cafe_*
