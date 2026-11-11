"""Render original Wwise events extracted from a local VALORANT install.
Requires FlashExtract, wwiser TXTP generation (including Init.bnk), and vgmstream.
No AES keys or original game packages are downloaded by this script.
"""
import argparse, hashlib, json, re, struct, subprocess, wave
from pathlib import Path
import numpy as np

# cue: event, perspective switch (1P=1204257842, 3P=1878706709, enemy=2106886385), max seconds
CUES = {
 "phoenix_throw": ("Play_Phoenix_AbilE_BuildFlash", "", None),
 "phoenix_windup": ("Play_Phoenix_AbilE_Projectile", "", .6),
 "phoenix_pop": ("Play_Phoenix_AbilE_FlashWorld", "", None),
 "breach_throw": ("Play_Breach_Flash_Cast", "1878706709", None),
 "breach_flight": ("Play_Breach_Flash_Projectile", "", .5),
 "breach_windup": ("Play_Breach_Flash_Zone_Charge", "", .5),
 "breach_pop": ("Play_Breach_Flash_Explode", "", None),
 "kayo_throw": ("Play_Grenadier_Flash_Cast", "1878706709", None),
 "kayo_windup": ("Play_Grenadier_AbilQ_WindUp", "", .3),
 "kayo_windup_bounce": ("Play_Grenadier_AbilQ_WindUp_Lob", "", .8),
 "kayo_pop": ("Play_Grenadier_AbilQ_Flash_Explo", "", None),
 "skye_throw": ("Play_Guide_HawkFlash_Fly", "", .6),
 "skye_windup": ("Play_Guide_HawkFlash_FlashCharging", "", .3),
 "skye_pop": ("Play_Guide_HawkFlash_FlashTriggered", "", None),
 "skye_pop_nohit": ("Play_Guide_HawkFlash_FlashTrigger_Fail", "", None),
 "yoru_throw": ("Play_Stealth_AbilQ_Cast", "1878706709", None),
 "yoru_flight": ("Play_Stealth_AbilQ_ProjectileLoop", "", .65),
 "yoru_windup": ("Play_Stealth_AbilQ_ThemedWindup", "", .6),
 "yoru_pop": ("Play_Stealth_AbilQ_ThemedFlash", "", 2.5),
 "vyse_windup": ("Play_Nox_AbilE_Flash_Windup_Loop", "1878706709", .5),
 "vyse_pop": ("Play_Nox_AbilE_FlashExplode", "", None),
 "dizzy_throw": ("Play_Aggrobot_AbilE_Deploy", "2106886385", None),
 "dizzy_form": ("Play_Aggrobot_E_Zamboni_Windup_Delay_Loop", "", .65),
 "dizzy_active": ("Play_Aggrobot_AbilE_Zamboni_Seeking_Loop", "2106886385", 1),
 "dizzy_spit": ("Play_Aggrobot_E_Zamboni_Fire", "1878706709", None),
 "dizzy_incoming": ("Play_Aggrobot_AbilE_Zamboni_1P_Enemy_Target_Locked", "", None),
 "dizzy_splat": ("Play_Aggrobot_AbilE_Zamboni_1P_Enemy_Rocket_Splat", "", None),
 "dizzy_down": ("Play_Aggrobot_AbilE_Zamboni_Destroyed", "1204257842", None),
 "dizzy_bubble": ("Play_Aggrobot_AbilE_Zamboni_Fall_Splat", "", None),
 "flash_ring": ("Play_Phoenix_AbilE_FlashPlayer", "", 2.5),
}
# Old synthesis invented a second sound for these slots; keep them silent rather than double-playing an event.
SILENT = {"phoenix_flight": "projectile already plays in windup", "kayo_flight": "no separate audible projectile cue", "skye_flight": "wing event already plays on release", "vyse_throw": "placed rose activation is the windup", "vyse_flight": "rose does not fly"}

def main():
 ap=argparse.ArgumentParser();ap.add_argument("work",type=Path);ap.add_argument("out",type=Path);a=ap.parse_args()
 a.out.mkdir(parents=True,exist_ok=True)
 manifest={"source":"Local VALORANT installation, original Wwise media and event banks", "copyright":"Riot Games; not CC0 or GPL", "tools":{"wwiser":"v20260808","vgmstream":"r2168"},"cues":{},"silent":SILENT}
 for cue,(event,perspective,limit) in CUES.items():
  candidates=[p for p in (a.work/"render").glob("*.txtp") if p.stem==event or p.stem.startswith(event+" [") or p.stem.startswith(event+" {")]
  if perspective:candidates=[p for p in candidates if "="+perspective+"]" in p.name]
  if len(candidates)!=1:raise RuntimeError(f"Expected one event branch for {cue}: {candidates}")
  source=candidates[0];txt=source.read_text();variants=3 if "-R" in txt else 1
  outputs=[]
  for variant in range(variants):
   recipe=re.sub(r"(-R(\d+)>)(\d+)",lambda m:m[1]+str(variant%int(m[2])+1),txt)
   # Limit looping generators; the app's charge/active windows set the audible duration below.
   recipe=re.sub(r"#l [0-9.]+", "#l 1.0",recipe)
   tx=a.work/f"{cue}_{variant}.txtp";tx.write_text(recipe)
   temp=a.work/f"{cue}_{variant}.wav"
   run=subprocess.run([str(a.work/"vgmstream/vgmstream-cli.exe"),"-l","1","-f","0","-d","0","-W","4","-o",str(temp),str(tx)],capture_output=True,text=True,timeout=20)
   if run.returncode:raise RuntimeError(run.stdout+run.stderr)
   # Decode float first so original event layers are not clipped before gain limiting.
   raw=temp.read_bytes();pos=12;fmt=None;data=None
   while pos+8<=len(raw):
    kind=raw[pos:pos+4];size=struct.unpack_from('<I',raw,pos+4)[0];chunk=raw[pos+8:pos+8+size]
    if kind==b'fmt ':fmt=struct.unpack_from('<HHIIHH',chunk)
    elif kind==b'data':data=chunk
    pos+=8+size+(size&1)
   if fmt is None or data is None or fmt[0]!=3 or fmt[5]!=32:raise RuntimeError('Expected float WAV from decoder')
   ch,sr=fmt[1],fmt[2];x=np.frombuffer(data,dtype='<f4').astype(np.float64).reshape(-1,ch).mean(axis=1)
   if limit is not None:
    length=int(limit*sr)
    if len(x)>length:
     x=x[:length].copy();fade=min(int(.005*sr),len(x));x[-fade:]*=np.linspace(1,0,fade)
   peak=float(np.max(np.abs(x)))
   if peak<.0001:raise RuntimeError(f"Silent decoded event: {cue}")
   # Only attenuate peaks, never boost different cues to equal loudness.
   gain=min(1,.96/peak);x=x*gain
   dest=a.out/f"{cue}{'_'+str(variant+1) if variant else ''}.wav"
   with wave.open(str(dest),'wb') as f:
    f.setnchannels(1);f.setsampwidth(2);f.setframerate(sr);f.writeframes(np.round(x*32767).astype('<i2').tobytes())
   outputs.append({"file":dest.name,"seconds":len(x)/sr,"sha256":hashlib.sha256(dest.read_bytes()).hexdigest(),"sourceBankSha256":hashlib.sha256((a.work/"banks"/(event+".bnk")).read_bytes()).hexdigest(),"sourceMediaIds":re.findall(r"/([0-9]+)\.wem",recipe.split('# AUTOGENERATED')[0]),"peakGain":gain})
  manifest['cues'][cue]={"event":event,"perspective":perspective,"durationLimit":limit,"files":outputs}
 for cue in SILENT:
  dest=a.out/(cue+'.wav')
  with wave.open(str(dest),'wb') as f:f.setnchannels(1);f.setsampwidth(2);f.setframerate(48000);f.writeframes(b'\0\0'*480)
 (a.out/'provenance.json').write_text(json.dumps(manifest,indent=2)+'\n')
 print(f"Rendered {len(CUES)} original cue mappings, {sum(len(c['files']) for c in manifest['cues'].values())} clips, {len(SILENT)} suppressed duplicate cues")
if __name__=='__main__':main()
