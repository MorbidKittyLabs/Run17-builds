using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

public static class MorbidRecovery {
 static string Normalize(string path){return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);}
 static bool Same(string a,string b){return String.Equals(Normalize(a),Normalize(b),StringComparison.OrdinalIgnoreCase);}
 static string Hash(string path){using(var h=SHA256.Create())using(var f=File.OpenRead(path))return BitConverter.ToString(h.ComputeHash(f)).Replace("-","").ToLowerInvariant();}
 static string ManifestPath(string game){return Path.Combine(game,"MorbidKitty","install-manifest.json");}
 static string PanelPath(string game){return Path.Combine(game,"MorbidKitty","Community-Panel.exe");}
 static List<string> Candidates(){
  var paths=new List<string>();
  foreach(var p in Process.GetProcessesByName("Prime"))try{if(p.MainModule!=null)paths.Add(Path.GetDirectoryName(p.MainModule.FileName));}catch{}finally{p.Dispose();}
  using(var root=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall"))if(root!=null)foreach(var name in root.GetSubKeyNames())if(name.StartsWith("MorbidKittySTFC-",StringComparison.OrdinalIgnoreCase))using(var k=root.OpenSubKey(name)){var location=k==null?null:Convert.ToString(k.GetValue("InstallLocation"));if(!String.IsNullOrWhiteSpace(location))paths.Add(location);}
  var found=GamePaths.Find();if(found!=null)paths.Add(found);
  return paths.Where(p=>GamePaths.IsGame(p)&&File.Exists(ManifestPath(p))).Select(Normalize).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
 }
 static string ChooseGame(){
  var games=Candidates();if(games.Count==1)return games[0];
  using(var choose=new OpenFileDialog{Title="Choose Prime.exe for the Run17 R18 installation to recover",Filter="STFC game|Prime.exe",CheckFileExists=true}){
   if(games.Count>0)choose.InitialDirectory=games[0];
   if(choose.ShowDialog()!=DialogResult.OK)return null;
   return Path.GetDirectoryName(choose.FileName);
  }
 }
 static void Preflight(string game){
  if(!GamePaths.IsGame(game)||!File.Exists(ManifestPath(game)))throw new InvalidOperationException("The selected folder has no Morbid Kitty installation record. No files have been changed.");
  var manifest=InstallerCore.Read<InstallManifest>(ManifestPath(game));
  if(manifest==null||manifest.Files==null||!Same(manifest.GameDirectory,game))throw new InvalidDataException("The installation record does not match this game folder.");
  var dll=manifest.Files.FirstOrDefault(e=>Path.GetFileName(e.RelativePath).Equals("version.dll",StringComparison.OrdinalIgnoreCase));
  if(dll==null)throw new InvalidDataException("The installation record is missing the game DLL.");
  string current=Path.Combine(game,"version.dll");
  if(!File.Exists(current)||!String.Equals(Hash(current),dll.InstalledHash,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("The game DLL was changed after installation. Recovery has stopped without replacing it.");
  string backupRoot=Normalize(Path.Combine(game,"MorbidKitty-Backups"))+Path.DirectorySeparatorChar;
  foreach(var e in manifest.Files.Where(e=>e.HadOriginal)){
   string backup=Path.GetFullPath(e.OriginalBackup);
   if(!backup.StartsWith(backupRoot,StringComparison.OrdinalIgnoreCase)||!File.Exists(backup))throw new InvalidDataException("A required installation backup is missing. Recovery has stopped without changing the installation.");
  }
 }
 static List<Process> Matching(string name,string executable){
  var matches=new List<Process>();
  foreach(var p in Process.GetProcessesByName(name))try{if(p.MainModule!=null&&Same(p.MainModule.FileName,executable)){matches.Add(p);continue;}p.Dispose();}catch{p.Dispose();}
  return matches;
 }
 static void StopExact(string name,string executable,bool graceful){
  foreach(var p in Matching(name,executable))using(p){
   if(p.HasExited)continue;
   if(graceful){try{p.CloseMainWindow();}catch{}if(p.WaitForExit(2500))continue;}
   p.Kill();if(!p.WaitForExit(4000))throw new InvalidOperationException("Windows could not close "+name+". No rollback was attempted.");
  }
 }
 public static int Recover(string game){
  game=Normalize(game);Preflight(game);
  Console.WriteLine("Stopping the controller for this installation...");StopExact("Community-Panel",PanelPath(game),false);
  Console.WriteLine("Closing this STFC client...");StopExact("Prime",Path.Combine(game,"Prime.exe"),true);
  Console.WriteLine("Restoring the recorded pre-installation files...");return InstallerCore.Uninstall(game);
 }
 static void Require(bool value,string message){if(!value)throw new Exception(message);}
 static string Fixture(string root,string name){
  var game=Path.Combine(root,name);var tools=Path.Combine(game,"MorbidKitty");var backup=Path.Combine(game,"MorbidKitty-Backups","fixture");Directory.CreateDirectory(tools);Directory.CreateDirectory(backup);
  File.WriteAllText(Path.Combine(game,"GameAssembly.dll"),"fixture");File.Copy(Assembly.GetExecutingAssembly().Location,Path.Combine(game,"Prime.exe"));File.Copy(Assembly.GetExecutingAssembly().Location,PanelPath(game));
  File.WriteAllText(Path.Combine(game,"version.dll"),"installed DLL");var original=Path.Combine(backup,"version.dll");File.WriteAllText(original,"original DLL");
  File.WriteAllText(Path.Combine(game,"community_patch_settings.toml"),"# keep user settings\n[ui]\nui_scale=0.75\n");File.WriteAllText(Path.Combine(tools,"Panel.json"),"{\"fixture\":true}");
  var manifest=new InstallManifest{GameDirectory=game,Shortcut=Path.Combine(root,"absent-shortcut.lnk")};manifest.Files.Add(new InstallRecord{RelativePath="version.dll",HadOriginal=true,OriginalBackup=original,InstalledHash=Hash(Path.Combine(game,"version.dll"))});manifest.Files.Add(new InstallRecord{RelativePath="MorbidKitty/Community-Panel.exe",HadOriginal=false,InstalledHash=Hash(PanelPath(game))});InstallerCore.Write(ManifestPath(game),manifest);return game;
 }
 static Process StartFixture(string path){var p=Process.Start(new ProcessStartInfo(path,"--process-fixture"){UseShellExecute=false,CreateNoWindow=true});Thread.Sleep(250);return p;}
 static void SelfTest(string directory){
  Directory.CreateDirectory(directory);var target=Fixture(directory,"target");var other=Fixture(directory,"unrelated");var changed=Fixture(directory,"changed");
  File.WriteAllText(Path.Combine(changed,"version.dll"),"user changed DLL");bool refused=false;try{Recover(changed);}catch(InvalidOperationException){refused=true;}
  Require(refused&&File.ReadAllText(Path.Combine(changed,"version.dll"))=="user changed DLL","Changed DLL was not preserved");
  var incomplete=Fixture(directory,"incomplete");File.Delete(Path.Combine(incomplete,"MorbidKitty-Backups","fixture","version.dll"));refused=false;try{Recover(incomplete);}catch(InvalidDataException){refused=true;}
  Require(refused&&File.ReadAllText(Path.Combine(incomplete,"version.dll"))=="installed DLL","Missing backup altered the installation");
  using(var targetPanel=StartFixture(PanelPath(target)))using(var targetGame=StartFixture(Path.Combine(target,"Prime.exe")))using(var otherPanel=StartFixture(PanelPath(other)))using(var otherGame=StartFixture(Path.Combine(other,"Prime.exe"))){
   try{
    Require(Recover(target)==0,"Recovery unexpectedly preserved modified fixture files");
    Require(targetPanel.WaitForExit(1000)&&targetGame.WaitForExit(1000),"Target processes were not closed");
    Require(!otherPanel.HasExited&&!otherGame.HasExited,"Another game installation was interrupted");
    Require(File.ReadAllText(Path.Combine(target,"version.dll"))=="original DLL","Original DLL was not restored");
    Require(!File.Exists(PanelPath(target)),"Added companion executable was not removed");
    Require(File.ReadAllText(Path.Combine(target,"community_patch_settings.toml")).Contains("ui_scale=0.75"),"Game configuration changed");
    Require(File.ReadAllText(Path.Combine(target,"MorbidKitty","Panel.json"))=="{\"fixture\":true}","Panel settings changed");
    File.WriteAllText(Path.Combine(directory,"recovery-validation.txt"),"PASS: exact installation process scope; controller/game termination; backup restoration; settings preservation; changed-DLL refusal; missing-backup refusal; unrelated game processes remain running.\r\n");
   }finally{foreach(var p in new[]{targetPanel,targetGame,otherPanel,otherGame})try{if(!p.HasExited){p.Kill();p.WaitForExit(2000);}}catch{}}
  }
 }
 [STAThread] public static int Main(string[] args){
  if(args.Length==1&&args[0]=="--process-fixture"){Thread.Sleep(120000);return 0;}
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  try{
   if(args.Length==2&&args[0]=="--self-test"){SelfTest(Path.GetFullPath(args[1]));return 0;}
   Console.WriteLine("MORBID KITTY RUN17 R18 RECOVERY\nCloses the affected game client and rolls back this Morbid Kitty installation.\nGame configuration is preserved.\n");
   var game=args.Length==2&&args[0]=="--game"?args[1]:ChooseGame();if(game==null)return 0;
   Console.WriteLine("Game folder: "+game);int preserved=Recover(game);
   Console.WriteLine("\nRECOVERY COMPLETE. Start STFC using its usual game launcher.");
   if(preserved>0)Console.WriteLine(preserved+" changed file(s) or shortcut(s) were preserved.");
   Console.WriteLine("Press Enter to close.");Console.ReadLine();return 0;
  }catch(Exception ex){Console.Error.WriteLine("\nRecovery stopped: "+ex.Message+"\nNo account or game configuration has been reset.");if(args.Length==0){Console.WriteLine("Press Enter to close.");Console.ReadLine();}return 1;}
 }
}
