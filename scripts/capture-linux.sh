#!/usr/bin/env bash
set -euo pipefail

capture_dir="$PWD/artifacts/visual"
player="$PWD/build/StandaloneLinux64/FrutigerBadminton.x86_64"
mkdir -p "$capture_dir"
if [[ ! -x "$player" ]]; then sudo chmod +x "$player"; fi

# Give FMOD a silent ALSA device on runners without audio hardware. Unity may
# initialise FMOD even when -noaudio is supplied, so that flag alone is not enough.
printf 'pcm.!default { type null }\n' > "$capture_dir/asound.conf"
export ALSA_CONFIG_PATH="$capture_dir/asound.conf"
export capture_dir player
xvfb-run -a -s '-screen 0 1920x1080x24 +extension GLX' bash -c '
  set -euo pipefail
  export LIBGL_ALWAYS_SOFTWARE=1
  "$player" -screen-fullscreen 0 -screen-width 1920 -screen-height 1080 -force-glcore -noaudio -logFile "$capture_dir/player.log" &
  player_pid=$!
  trap "kill $player_pid 2>/dev/null || true" EXIT
  window=""
  for ((attempt = 0; attempt < 60; attempt++)); do
    if ! kill -0 "$player_pid" 2>/dev/null; then
      echo "The capture player exited before opening a window."
      exit 1
    fi
    window="$(xdotool search --onlyvisible --pid "$player_pid" 2>/dev/null | head -n 1 || true)"
    if [[ -n "$window" ]]; then break; fi
    sleep 1
  done
  if [[ -z "$window" ]]; then
    echo "The capture player did not open a visible window."
    exit 1
  fi
  sleep 12
  import -window root "$capture_dir/title.png"
  xdotool windowfocus "$window"
  xdotool key Return
  sleep 8
  import -window root "$capture_dir/court.png"
'
