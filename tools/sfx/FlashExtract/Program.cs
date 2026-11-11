using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider.Usmap;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Versions;
if (args.Length == 0) { Console.Error.WriteLine("Usage: FlashExtract <work-dir> [event-filter] [game-paks-dir]"); return; }
var work = Path.GetFullPath(args[0]);
var provider = new DefaultFileProvider(args.Length > 2 ? args[2] : @"C:\Riot Games\VALORANT\live\ShooterGame\Content\Paks", SearchOption.TopDirectoryOnly, new VersionContainer(EGame.GAME_Valorant), StringComparer.OrdinalIgnoreCase);
provider.MappingsContainer = new FileUsmapTypeMappingsProvider(Path.Combine(work,"valorant.usmap"));
provider.Initialize();
provider.SubmitKey(new FGuid(),new FAesKey(File.ReadAllText(Path.Combine(work,"aes.txt")).Trim()));
Directory.CreateDirectory(Path.Combine(work,"banks"));
var init=provider.Files.Keys.FirstOrDefault(k=>k.EndsWith("/WwiseAudio/Init.bnk",StringComparison.OrdinalIgnoreCase));
if(init!=null)File.WriteAllBytes(Path.Combine(work,"banks","Init.bnk"),provider.Files[init].Read());
var files=provider.Files.Keys.Where(k=>k.Contains("WwiseAudio",StringComparison.OrdinalIgnoreCase)&&k.EndsWith(".uasset")).Order().ToArray();
File.WriteAllLines(Path.Combine(work,"audio-assets.txt"), files);
File.WriteAllLines(Path.Combine(work,"audio-raw.txt"), provider.Files.Keys.Where(k=>k.EndsWith(".wem")||k.EndsWith(".bnk")));
Console.WriteLine($"Indexed {files.Length} audio assets");
if(args.Length<2)return;
int failures = 0;
foreach(var file in files.Where(f=>args[1].Split('|').Any(pattern=>f.Contains(pattern,StringComparison.OrdinalIgnoreCase)))) {
 try {
 var exports=provider.LoadPackage(file).GetExports().ToArray();
 foreach(var export in exports) {
 Console.WriteLine($"{file}: {export.ExportType} {export.Name}");
 File.WriteAllText(Path.Combine(work,export.Name+".json"),Newtonsoft.Json.JsonConvert.SerializeObject(export,Newtonsoft.Json.Formatting.Indented));
 if(export.ExportType=="AkAudioEvent") {
 var doc=Newtonsoft.Json.Linq.JObject.Parse(Newtonsoft.Json.JsonConvert.SerializeObject(export));
 foreach(var bank in doc.SelectTokens("$..SoundBanks[*]")) {
 var name=bank["SoundBankPathName"]?.ToString(); if(name==null)continue;
 var path=provider.Files.Keys.FirstOrDefault(k=>k.EndsWith(name,StringComparison.OrdinalIgnoreCase));
 if(path==null)continue;
 Directory.CreateDirectory(Path.Combine(work,"banks"));
 File.WriteAllBytes(Path.Combine(work,"banks",Path.GetFileName(name)),provider.Files[path].Read());
 }
 foreach(var mediaNode in doc.SelectTokens("$..Media[*]")) {
 string? mediaPath=mediaNode["MediaPathName"]?.ToString(); if(mediaPath==null)continue;
 var match=provider.Files.Keys.FirstOrDefault(k=>k.EndsWith(mediaPath,StringComparison.OrdinalIgnoreCase));
 if(match==null){failures++;Console.Error.WriteLine("MISSING "+mediaPath);continue;}
 var dataRaw=provider.Files[match].Read();
 var rawOut=Path.Combine(work,"wem",Path.GetFileNameWithoutExtension(file)+"_"+mediaNode["MediaId"]+".wem");
 Directory.CreateDirectory(Path.GetDirectoryName(rawOut)!);File.WriteAllBytes(rawOut,dataRaw);
 Console.WriteLine("EXPORTED "+rawOut+" "+mediaNode["DebugName"]);
 }
 }

 }
 }catch(Exception e){failures++;Console.Error.WriteLine($"FAILED {file}: {e.Message}");}
}

Environment.ExitCode = failures == 0 ? 0 : 1;
