using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Windows.Forms;
using Microsoft.Win32;
[assembly:AssemblyTitle("Morbid Kitty STFC Setup")]
[assembly:AssemblyDescription("Install the single-account Community R18 branch")]
[assembly:AssemblyVersion("18.4.0.0")]

[DataContract] public sealed class InstallRecord {
 [DataMember] public string RelativePath,OriginalBackup,InstalledHash;
 [DataMember] public bool HadOriginal;
}
[DataContract] public sealed class InstallManifest {
 [DataMember] public string GameDirectory,Shortcut,Version="Run17 Build18 Community";
 [DataMember] public string OriginalShortcutBackup;
 [DataMember] public bool HadOriginalShortcut;
 [DataMember] public List<InstallRecord> Files=new List<InstallRecord>();
}
[DataContract] public sealed class PayloadManifest {
 [DataMember] public Dictionary<string,string> Files=new Dictionary<string,string>();
}
public static class InstallerCore {
 public const string ShortcutName="Morbid Kitty - Star Trek Fleet Command.lnk";
 static readonly string[] Allowed={"version.dll","Community-Panel.exe","MorbidKitty-Launcher.exe","MorbidKitty-Uninstall.exe","MorbidKitty-Splash.png","MorbidKitty.ico","Collect-Diagnostics.ps1","Collect-Diagnostics.bat","README-FIRST.txt","DIAGNOSTICS.md","LICENSE","community_patch_settings.example.toml"};
 public static T Read<T>(string path){using(var f=File.OpenRead(path))return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(f);}
 public static void Write<T>(string path,T value){Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+".tmp";using(var f=File.Create(temp))new DataContractJsonSerializer(typeof(T)).WriteObject(f,value);if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}
 static string Sha(byte[] data){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(data)).Replace("-","").ToLowerInvariant();}
 static string Destination(string game,string name){if(!Allowed.Contains(name))throw new InvalidDataException("Unexpected payload file: "+name);return name=="version.dll"?Path.Combine(game,name):Path.Combine(game,"MorbidKitty",name);}
 static string Key(string game){return @"Software\Microsoft\Windows\CurrentVersion\Uninstall\MorbidKittySTFC-"+GamePaths.Hash(game.ToUpperInvariant()).Substring(0,12);}
 public static Dictionary<string,byte[]> VerifyPayload(Stream stream){
  var result=new Dictionary<string,byte[]>();
  using(var z=new ZipArchive(stream,ZipArchiveMode.Read,true)){
   var entry=z.GetEntry("PACKAGE-MANIFEST.json");if(entry==null)throw new InvalidDataException("Payload manifest missing");PayloadManifest expected;
   using(var f=entry.Open())expected=(PayloadManifest)new DataContractJsonSerializer(typeof(PayloadManifest)).ReadObject(f);
   if(expected==null || expected.Files==null || expected.Files.Count!=Allowed.Length)throw new InvalidDataException("Payload list is incomplete");
   if(z.Entries.Count!=Allowed.Length+1)throw new InvalidDataException("Unexpected archive entry");
   foreach(var name in Allowed){
    if(!expected.Files.ContainsKey(name))throw new InvalidDataException("Missing payload item: "+name);
    var item=z.GetEntry(name);if(item==null || item.Length>100*1024*1024)throw new InvalidDataException("Invalid payload item: "+name);
    byte[] bytes;using(var f=item.Open())using(var m=new MemoryStream()){f.CopyTo(m);bytes=m.ToArray();}
    if(Sha(bytes)!=expected.Files[name])throw new InvalidDataException("Payload hash mismatch: "+name);result.Add(name,bytes);
   }
  }
  return result;
 }
 public static InstallManifest Install(Stream payload,string game,string desktop,bool injectFailure=false){
  game=Path.GetFullPath(game).TrimEnd(Path.DirectorySeparatorChar);if(!GamePaths.IsGame(game))throw new InvalidOperationException("Select Prime.exe beside GameAssembly.dll.");
  if(GamePaths.Running(game))throw new InvalidOperationException("Close STFC, then click Install again.");
  var files=VerifyPayload(payload);string tools=Path.Combine(game,"MorbidKitty"),manifestPath=Path.Combine(tools,"install-manifest.json");
  InstallManifest previous=File.Exists(manifestPath)?Read<InstallManifest>(manifestPath):null;
  string backup=Path.Combine(game,"MorbidKitty-Backups",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,8));Directory.CreateDirectory(backup);
  var rollback=new List<Tuple<string,string,bool>>();var manifest=new InstallManifest{GameDirectory=game,Shortcut=Path.Combine(desktop,ShortcutName)};
  bool hadManifest=File.Exists(manifestPath);byte[] oldManifest=hadManifest?File.ReadAllBytes(manifestPath):null;
  string shortcutRollback=Path.Combine(backup,"desktop-shortcut.lnk");bool hadShortcut=File.Exists(manifest.Shortcut);if(hadShortcut)File.Copy(manifest.Shortcut,shortcutRollback);
  bool sameShortcut=previous!=null && String.Equals(previous.Shortcut,manifest.Shortcut,StringComparison.OrdinalIgnoreCase);
  manifest.HadOriginalShortcut=sameShortcut?previous.HadOriginalShortcut:hadShortcut;
  manifest.OriginalShortcutBackup=sameShortcut?previous.OriginalShortcutBackup:shortcutRollback;
  try{
   int count=0;
   foreach(var item in files){
    string destination=Destination(game,item.Key);bool existed=File.Exists(destination);string original=Path.Combine(backup,item.Key);
    if(existed)File.Copy(destination,original);rollback.Add(Tuple.Create(destination,original,existed));
    Directory.CreateDirectory(Path.GetDirectoryName(destination));string temp=destination+".installing";File.WriteAllBytes(temp,item.Value);
    if(existed)File.Replace(temp,destination,null);else File.Move(temp,destination);
    if(Sha(File.ReadAllBytes(destination))!=Sha(item.Value))throw new IOException("Installed file verification failed: "+item.Key);
    var prior=previous==null?null:previous.Files.FirstOrDefault(x=>x.RelativePath==(item.Key=="version.dll"?item.Key:"MorbidKitty/"+item.Key));
    manifest.Files.Add(new InstallRecord{RelativePath=item.Key=="version.dll"?item.Key:"MorbidKitty/"+item.Key,HadOriginal=prior!=null?prior.HadOriginal:existed,OriginalBackup=prior!=null?prior.OriginalBackup:original,InstalledHash=Sha(item.Value)});
    if(injectFailure && ++count==3)throw new IOException("Injected rollback check");
   }
   Directory.CreateDirectory(desktop);CreateShortcut(manifest.Shortcut,Path.Combine(tools,"MorbidKitty-Launcher.exe"),tools,Path.Combine(tools,"MorbidKitty.ico"));
   Write(manifestPath,manifest);
   using(var key=Registry.CurrentUser.CreateSubKey(Key(game))){
    key.SetValue("DisplayName","Morbid Kitty — STFC Community R18");key.SetValue("DisplayVersion","18.4");key.SetValue("Publisher","Morbid Kitty Labs");
    key.SetValue("InstallLocation",game);key.SetValue("DisplayIcon",Path.Combine(tools,"MorbidKitty.ico"));
    key.SetValue("UninstallString",GamePaths.Quote(Path.Combine(tools,"MorbidKitty-Uninstall.exe"))+" --uninstall --game "+GamePaths.Quote(game));key.SetValue("NoModify",1);key.SetValue("NoRepair",1);
   }
   return manifest;
  }catch{
   for(int i=rollback.Count-1;i>=0;i--){var f=rollback[i];try{if(f.Item3)File.Copy(f.Item2,f.Item1,true);else if(File.Exists(f.Item1))File.Delete(f.Item1);string temp=f.Item1+".installing";if(File.Exists(temp))File.Delete(temp);}catch{}}
   try{if(hadManifest)File.WriteAllBytes(manifestPath,oldManifest);else if(File.Exists(manifestPath))File.Delete(manifestPath);if(hadShortcut)File.Copy(shortcutRollback,manifest.Shortcut,true);else if(File.Exists(manifest.Shortcut))File.Delete(manifest.Shortcut);}catch{}
   throw;
  }
 }
 public static void CreateShortcut(string path,string target,string workingDirectory,string icon){
  object shell=null,shortcut=null;
  try{
   var type=Type.GetTypeFromProgID("WScript.Shell");shell=Activator.CreateInstance(type);
   shortcut=type.InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{path});var t=shortcut.GetType();
   t.InvokeMember("TargetPath",BindingFlags.SetProperty,null,shortcut,new object[]{target});t.InvokeMember("WorkingDirectory",BindingFlags.SetProperty,null,shortcut,new object[]{workingDirectory});
   t.InvokeMember("IconLocation",BindingFlags.SetProperty,null,shortcut,new object[]{icon+",0"});t.InvokeMember("Description",BindingFlags.SetProperty,null,shortcut,new object[]{"Start Star Trek Fleet Command with Morbid Kitty"});t.InvokeMember("Save",BindingFlags.InvokeMethod,null,shortcut,null);
  }finally{if(shortcut!=null&&Marshal.IsComObject(shortcut))Marshal.ReleaseComObject(shortcut);if(shell!=null&&Marshal.IsComObject(shell))Marshal.ReleaseComObject(shell);}
 }
 static bool ShortcutTargets(string path,string target){
  object shell=null,shortcut=null;
  try{
   var type=Type.GetTypeFromProgID("WScript.Shell");shell=Activator.CreateInstance(type);
   shortcut=type.InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{path});
   string actual=Convert.ToString(shortcut.GetType().InvokeMember("TargetPath",BindingFlags.GetProperty,null,shortcut,null));
   return String.Equals(Path.GetFullPath(actual),Path.GetFullPath(target),StringComparison.OrdinalIgnoreCase);
  }catch{return false;}
  finally{if(shortcut!=null&&Marshal.IsComObject(shortcut))Marshal.ReleaseComObject(shortcut);if(shell!=null&&Marshal.IsComObject(shell))Marshal.ReleaseComObject(shell);}
 }
 static string CheckedBackup(string game,string path){
  string backup=Path.GetFullPath(path),required=Path.GetFullPath(Path.Combine(game,"MorbidKitty-Backups"))+Path.DirectorySeparatorChar;
  if(!backup.StartsWith(required,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Backup path is outside this installation");return backup;
 }
 public static int Uninstall(string game){
  if(GamePaths.Running(game))throw new InvalidOperationException("Close STFC before uninstalling.");
  string manifestPath=Path.Combine(game,"MorbidKitty","install-manifest.json");var manifest=Read<InstallManifest>(manifestPath);int preserved=0;
  foreach(var entry in manifest.Files){
   string name=Path.GetFileName(entry.RelativePath);string destination=Destination(game,name);
   if(!File.Exists(destination))continue;
   if(Sha(File.ReadAllBytes(destination))!=entry.InstalledHash){preserved++;continue;}
   if(entry.HadOriginal)File.Copy(CheckedBackup(game,entry.OriginalBackup),destination,true);
   else File.Delete(destination);
  }
  if(File.Exists(manifest.Shortcut)&&Path.GetFileName(manifest.Shortcut)==ShortcutName){
   if(ShortcutTargets(manifest.Shortcut,Path.Combine(game,"MorbidKitty","MorbidKitty-Launcher.exe"))){
    if(manifest.HadOriginalShortcut)File.Copy(CheckedBackup(game,manifest.OriginalShortcutBackup),manifest.Shortcut,true);else File.Delete(manifest.Shortcut);
   }else preserved++;
  }
  Registry.CurrentUser.DeleteSubKeyTree(Key(game),false);File.Delete(manifestPath);
  return preserved;
 }
 public static void SelfTest(Stream payload,string directory){
  Directory.CreateDirectory(directory);var game=Path.Combine(directory,"different-location","game");var desktop=Path.Combine(directory,"Desktop");Directory.CreateDirectory(game);Directory.CreateDirectory(desktop);
  File.WriteAllText(Path.Combine(game,"Prime.exe"),"fixture");File.WriteAllText(Path.Combine(game,"GameAssembly.dll"),"fixture");File.WriteAllText(Path.Combine(game,"version.dll"),"original DLL");
  string config=Path.Combine(game,"community_patch_settings.toml");File.WriteAllText(config,"# preserve personal configuration\n[shortcuts]\naction_view='V'\n");string originalConfig=File.ReadAllText(config);
  byte[] data;using(var m=new MemoryStream()){payload.CopyTo(m);data=m.ToArray();}
  bool failed=false;try{using(var p=new MemoryStream(data))Install(p,game,desktop,true);}catch(IOException){failed=true;}
  if(!failed || File.ReadAllText(Path.Combine(game,"version.dll"))!="original DLL")throw new Exception("Rollback failed");
  using(var p=new MemoryStream(data))Install(p,game,desktop);
  if(File.ReadAllText(config)!=originalConfig || !File.Exists(Path.Combine(desktop,ShortcutName)))throw new Exception("Configuration/shortcut check failed");
  using(var p=new MemoryStream(data))Install(p,game,desktop);
  if(Uninstall(game)!=0 || File.ReadAllText(Path.Combine(game,"version.dll"))!="original DLL" || File.Exists(Path.Combine(desktop,ShortcutName)) || File.ReadAllText(config)!=originalConfig)throw new Exception("Update/uninstall restoration failed");
  string shortcutPath=Path.Combine(desktop,ShortcutName);CreateShortcut(shortcutPath,Path.Combine(game,"Prime.exe"),game,Path.Combine(game,"Prime.exe"));byte[] originalShortcut=File.ReadAllBytes(shortcutPath);
  using(var p=new MemoryStream(data))Install(p,game,desktop);
  using(var p=new MemoryStream(data))Install(p,game,desktop);
  if(Uninstall(game)!=0 || !File.ReadAllBytes(shortcutPath).SequenceEqual(originalShortcut))throw new Exception("Original shortcut restoration failed");
  using(var p=new MemoryStream(data))Install(p,game,desktop);
  CreateShortcut(shortcutPath,Path.Combine(game,"Prime.exe"),game,Path.Combine(game,"Prime.exe"));byte[] changedShortcut=File.ReadAllBytes(shortcutPath);
  if(Uninstall(game)!=1 || !File.ReadAllBytes(shortcutPath).SequenceEqual(changedShortcut))throw new Exception("Modified shortcut preservation failed");
  File.WriteAllText(Path.Combine(directory,"installer-validation.txt"),"PASS: alternate game location; checksum verification; installation rollback; Desktop shortcut; settings preservation; repeat install; original-DLL and original-shortcut restoration; modified-shortcut preservation and uninstall.\r\n");
 }
}
public sealed class SetupForm:Form {
 readonly bool remove;readonly string desktop;string game;readonly TextBox location;readonly Label status;readonly Button install;bool installed,removed,needsAdmin;
 public SetupForm(string chosen,string desktopDirectory,bool uninstall){
  game=chosen??GamePaths.Find();desktop=desktopDirectory;remove=uninstall;
  Text=uninstall?"Remove Morbid Kitty":"Install Morbid Kitty — STFC Community";ClientSize=new Size(620,370);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;
  BackColor=Color.FromArgb(6,51,79);ForeColor=Color.WhiteSmoke;Font=new Font("Tahoma",10);
  var title=new Label{Text=uninstall?"Remove this community branch":"A little Morbid for your STFC",Font=new Font("Tahoma",18,FontStyle.Bold),Bounds=new Rectangle(24,22,570,34)};Controls.Add(title);
  var explanation=new Label{Text=uninstall?"Restore the previous DLL and remove the installed shortcut and tools.":"Run17 Build 18 • One account • Diagnostics and manual shortcuts\nBrief Morbid Kitty intro, followed by the normal Community Mod loading screens.",Bounds=new Rectangle(26,70,568,55)};Controls.Add(explanation);
  Controls.Add(new Label{Text="STFC game folder",Bounds=new Rectangle(26,135,300,23)});
  location=new TextBox{ReadOnly=true,Text=game??"Choose Prime.exe to locate your game",Bounds=new Rectangle(26,161,467,29)};Controls.Add(location);
  var browse=new GameButton{Text="Browse…",Bounds=new Rectangle(500,159,93,32)};browse.Click+=(o,e)=>Browse();Controls.Add(browse);
  status=new Label{Text=uninstall?"Your personal configuration and collected logs are preserved.":"Installs the DLL, launcher, diagnostics and Morbid Kitty desktop shortcut.\nExisting game settings are preserved; the previous DLL is backed up.",Bounds=new Rectangle(26,205,568,55)};Controls.Add(status);
  install=new GameButton{Text=uninstall?"UNINSTALL":"INSTALL",Font=new Font("Tahoma",14,FontStyle.Bold),Bounds=new Rectangle(26,278,280,59)};install.Click+=(o,e)=>Perform();Controls.Add(install);
  var readme=new GameButton{Text="README / Diagnostics",Bounds=new Rectangle(320,278,273,59)};readme.Click+=(o,e)=>Readme();Controls.Add(readme);
  string icon=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"MorbidKitty.ico");if(File.Exists(icon))GamePaths.IconFor(this,AppDomain.CurrentDomain.BaseDirectory);
 }
 void Browse(){using(var dialog=new OpenFileDialog{Title="Choose STFC Prime.exe",Filter="Star Trek Fleet Command (Prime.exe)|Prime.exe"})if(dialog.ShowDialog()==DialogResult.OK){game=Path.GetDirectoryName(dialog.FileName);location.Text=game;}}
 void Perform(){
  try{
   if(removed){Close();return;}
   if(needsAdmin){Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location,(remove?"--uninstall ":"")+"--game "+GamePaths.Quote(game)+" --desktop "+GamePaths.Quote(desktop)){UseShellExecute=true,Verb="runas"});Close();return;}
   if(installed){Process.Start(new ProcessStartInfo(Path.Combine(game,"MorbidKitty","MorbidKitty-Launcher.exe")){UseShellExecute=true});Close();return;}
   if(!GamePaths.IsGame(game)){Browse();if(!GamePaths.IsGame(game))return;}
   install.Enabled=false;status.Text=remove?"Restoring the previous installation…":"Installing and verifying files…";status.Refresh();
   if(remove){int preserved=InstallerCore.Uninstall(game);status.Text=preserved==0?"Removed. Previous DLL restored; personal settings and logs kept.":"Removed. Files modified after installation were preserved.";removed=true;install.Text="CLOSE";}
   else{using(var payload=Assembly.GetExecutingAssembly().GetManifestResourceStream("Payload")){if(payload==null)throw new InvalidDataException("Installer payload unavailable");InstallerCore.Install(payload,game,desktop);}installed=true;status.Text="Installed. Your Morbid Kitty shortcut is on the desktop.";install.Text="OPEN GAME";}
  }catch(UnauthorizedAccessException){needsAdmin=true;install.Text="RETRY AS ADMIN";status.Text="Windows needs permission to write to this game folder. Click Retry as admin to continue.";}
   catch(Exception ex){status.Text=ex.Message;}
  finally{install.Enabled=true;}
 }
 void Readme(){
  try{
   var window=new Form{Text="README and diagnostic coverage",ClientSize=new Size(850,620),StartPosition=FormStartPosition.CenterParent};var tabs=new TabControl{Dock=DockStyle.Fill};window.Controls.Add(tabs);
   foreach(var name in new[]{"README-FIRST.txt","DIAGNOSTICS.md"}){
    string text=null;string installedPath=game==null?null:Path.Combine(game,"MorbidKitty",name);
    if(installedPath!=null && File.Exists(installedPath))text=File.ReadAllText(installedPath);
    else using(var payload=Assembly.GetExecutingAssembly().GetManifestResourceStream("Payload"))if(payload!=null)using(var z=new ZipArchive(payload,ZipArchiveMode.Read))using(var reader=new StreamReader(z.GetEntry(name).Open()))text=reader.ReadToEnd();
    var page=new TabPage(name);page.Controls.Add(new TextBox{Text=text??"Documentation is beside the installed launcher.",ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Both,Dock=DockStyle.Fill,Font=new Font("Consolas",10)});tabs.TabPages.Add(page);
   }
   window.ShowDialog(this);
  }catch(Exception ex){MessageBox.Show(ex.Message,Text);}
 }
 public void Evidence(string directory){Directory.CreateDirectory(directory);Show();Application.DoEvents();using(var bmp=new Bitmap(Width,Height)){DrawToBitmap(bmp,new Rectangle(0,0,Width,Height));bmp.Save(Path.Combine(directory,"morbid-installer.png"));}}
}
public static class MorbidSetup {
 [STAThread] public static int Main(string[] args){
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  bool remove=Array.IndexOf(args,"--uninstall")>=0;
#if UNINSTALL_ENTRY
  remove=true;
#endif
  string game=null,desktop=Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);for(int i=0;i+1<args.Length;i++){if(args[i]=="--game")game=args[i+1];if(args[i]=="--desktop")desktop=args[i+1];}
  try{
   if(args.Length==2 && args[0]=="--self-test"){
    using(var payload=Assembly.GetExecutingAssembly().GetManifestResourceStream("Payload"))InstallerCore.SelfTest(payload,args[1]);
    using(var form=new SetupForm(Path.Combine(args[1],"different-location","game"),Path.Combine(args[1],"Desktop"),false))form.Evidence(args[1]);return 0;
   }
   if(remove){
    if(game==null)game=GamePaths.InstalledGame();string exe=Assembly.GetExecutingAssembly().Location;
    if(game!=null && exe.StartsWith(Path.Combine(game,"MorbidKitty")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)){
     string temp=Path.Combine(Path.GetTempPath(),"MorbidKitty-Remove-"+Guid.NewGuid().ToString("N")+".exe");File.Copy(exe,temp);
     Process.Start(new ProcessStartInfo(temp,"--uninstall --game "+GamePaths.Quote(game)+" --desktop "+GamePaths.Quote(desktop)){UseShellExecute=true});return 0;
    }
   }
   Application.Run(new SetupForm(game,desktop,remove));return 0;
  }catch(Exception ex){if(args.Length>0)File.WriteAllText("installer-error.txt",ex.ToString());else MessageBox.Show(ex.Message,"Morbid Kitty setup");return 1;}
 }
}
