#!/bin/sh
# Отрисовать MIDI живыми инструментами: FluidSynth и банк SoundFont.
#   sh Tools/music/render.sh вход.mid выход.wav [банк.sf2|sf3]
# Потом — эквалайзер, громкость, OGG и замер:
#   python3 Tools/music/master.py выход.wav выход.ogg
# Банк по умолчанию — FluidR3_GM (MIT; в Ubuntu — пакет fluid-soundfont-gm).
# Второй — MuseScore_General.sf3 (MIT):
#   https://ftp.osuosl.org/pub/musescore/soundfont/MuseScore_General/MuseScore_General.sf3
set -e
SF="${3:-/usr/share/sounds/sf2/FluidR3_GM.sf2}"
fluidsynth -ni -q -r 44100 -g 0.5 \
  -o synth.reverb.room-size=0.75 -o synth.reverb.damp=0.35 \
  -o synth.reverb.width=0.9 -o synth.reverb.level=0.8 \
  -o synth.chorus.active=0 \
  -F "$2" "$SF" "$1"
