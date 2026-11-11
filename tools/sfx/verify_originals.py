"""Release completeness/integrity guard for the original flash audio set (stdlib only)."""
import array
import hashlib
import json
import sys
import wave
from pathlib import Path

root = Path(__file__).resolve().parents[2] / "CaValAim/assets/audio/flash/originals"
manifest = json.loads((root / "provenance.json").read_text(encoding="utf-8"))
required = {"phoenix_pop", "breach_pop", "kayo_pop", "skye_pop", "skye_pop_nohit", "yoru_pop", "vyse_pop", "dizzy_splat"}
if not required.issubset(manifest["cues"]):
    raise RuntimeError("Original flash audio set is incomplete")
checked = 0
for cue, event in manifest["cues"].items():
    if not event["files"] or not event["event"].startswith("Play_"):
        raise RuntimeError(f"Missing source event or clips: {cue}")
    for clip in event["files"]:
        name = clip["file"]
        if Path(name).name != name:
            raise RuntimeError(f"Invalid clip filename: {name}")
        path = root / name
        if hashlib.sha256(path.read_bytes()).hexdigest() != clip["sha256"]:
            raise RuntimeError(f"Original clip hash mismatch: {name}")
        with wave.open(str(path), "rb") as audio:
            if audio.getnchannels() != 1 or audio.getsampwidth() != 2:
                raise RuntimeError(f"Clip must be mono PCM16: {name}")
            duration = audio.getnframes() / audio.getframerate()
            samples = array.array("h", audio.readframes(audio.getnframes()))
            if sys.byteorder != "little":
                samples.byteswap()
            peak = max(map(abs, samples), default=0)
            if peak < 4 or peak > 31460:
                raise RuntimeError(f"Silent or clipped event: {name}")
            if abs(duration - clip["seconds"]) > 1 / audio.getframerate():
                raise RuntimeError(f"Duration does not match provenance: {name}")
            if event["durationLimit"] and duration > event["durationLimit"] + 1 / audio.getframerate():
                raise RuntimeError(f"Charge/active cue exceeds gameplay window: {name}")
        checked += 1
for cue in manifest["silent"]:
    with wave.open(str(root / (cue + ".wav")), "rb") as audio:
        if any(audio.readframes(audio.getnframes())):
            raise RuntimeError(f"Duplicate/invented cue must stay suppressed: {cue}")
print(f"PASS: {checked} original flash clips, source events, hashes, mono PCM, headroom and gameplay timing")
