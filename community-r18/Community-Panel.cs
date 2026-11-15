using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Windows.Forms;
using System.Reflection;
using System.IO.MemoryMappedFiles;
[assembly: AssemblyTitle("STFC Community Panel")]
[assembly: AssemblyDescription("Single-account clock, timers and manual shortcuts")]
[assembly: AssemblyVersion("18.4.0.0")]

[DataContract] public sealed class AlarmState {
 [DataMember] public string Name="Timer";
 [DataMember] public int Minutes=5;
 [DataMember] public long DurationSeconds;
 [DataMember] public double Remaining=300;
 [DataMember] public DateTime EndUtc;
 [DataMember] public bool Running;
}
[DataContract] public sealed class PanelState {
 [DataMember] public int X=40,Y=40;
 [DataMember] public bool UserPosition=false;
 [DataMember] public bool? BorderlessMode;
 public bool Borderless {get{return BorderlessMode??true;}set{BorderlessMode=value;}}
 [DataMember] public double RelativeX=0,RelativeY=0;
 [DataMember] public List<AlarmState> Alarms=new List<AlarmState>();
}
public static class PanelStorage {
 public static PanelState Load(string path) {
  try {using(var f=File.OpenRead(path))return (PanelState)new DataContractJsonSerializer(typeof(PanelState)).ReadObject(f);}
  catch {return new PanelState();}
 }
 public static void Save(string path,PanelState value) {
  Directory.CreateDirectory(Path.GetDirectoryName(path));
  string temp=path+".tmp";
  using(var f=File.Create(temp))new DataContractJsonSerializer(typeof(PanelState)).WriteObject(f,value);
  if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);
 }
}
public sealed class GameChoice {
 public int Id;public IntPtr Window;public string Title,Directory;
 public override string ToString(){return Title+" ("+Id+")";}
}
public sealed class CommunityPanel:Form {
 [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
 [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr window,int command);
 [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
 [DllImport("user32.dll")] static extern void keybd_event(byte key,byte scan,uint flags,UIntPtr extra);
 [DllImport("user32.dll")] static extern bool ReleaseCapture();
 [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd,uint message,IntPtr w,IntPtr l);
 readonly string root=AppDomain.CurrentDomain.BaseDirectory;
 readonly string statePath;
 PanelState state;
 GameChoice bound;
 readonly bool preview;
 readonly int attachedPid;
 MemoryMappedFile hudMap;MemoryMappedViewAccessor hudView;
 long creation;DateTime nextBind=DateTime.MinValue;
 bool positioning,closing,hiddenByUser,shuttingDown;
 readonly GameDesktop desktop=new GameDesktop();
 readonly CommunityStore store=new CommunityStore();
 readonly SummonHandle summon=new SummonHandle();
 readonly List<Button> shipButtons=new List<Button>();
 EventWaitHandle wake;
 bool restoreHotkey,desktopHotkey;
 uint shipMask=255;readonly Dictionary<Control,int> toolOffsets=new Dictionary<Control,int>();
 int pendingShip;uint lastShipSequence;DateTime shipDeadline;
 [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h,int id,uint modifiers,uint key);
 [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h,int id);
 [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h,uint message,IntPtr w,IntPtr l);
 MorbidIntro intro;
 readonly NotifyIcon tray=new NotifyIcon();
 Label clock,notice;
 Button save,microphone;EventWaitHandle voiceReady,voiceFailed;DateTime voiceDeadline;
 readonly ToolTip tips=new ToolTip();
 readonly System.Windows.Forms.Timer ui=new System.Windows.Forms.Timer();
 readonly List<Label> countdowns=new List<Label>();
 readonly List<Button> startButtons=new List<Button>();
 EventWaitHandle saved,failed;
 DateTime saveDeadline;
 Form timerWindow;
 bool dirty;
 public CommunityPanel(int pid=0,bool showIntro=false,bool isPreview=false) {
  preview=isPreview;attachedPid=pid;
  statePath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"STFC-Community","Panel.json");
  state=PanelStorage.Load(statePath);
  if(state==null)state=new PanelState();if(state.Alarms==null)state.Alarms=new List<AlarmState>();
  while(state.Alarms.Count<6)state.Alarms.Add(new AlarmState{Name="Timer "+(state.Alarms.Count+1)});
  Text="Morbid Kitty — STFC";Name="CommunityPanel";ClientSize=new Size(106,232);
  FormBorderStyle=FormBorderStyle.None;MaximizeBox=false;ShowInTaskbar=false;TopMost=true;
  Font=new Font("Tahoma",9);BackColor=Color.Magenta;TransparencyKey=Color.Magenta;ForeColor=Color.FromArgb(244,252,255);
  StartPosition=FormStartPosition.Manual;Location=new Point(40,40);GamePaths.IconFor(this,root);
  var brand=new Label{Text="Morbid Kitty",Bounds=new Rectangle(3,0,75,20),TextAlign=ContentAlignment.MiddleCenter,Font=new Font("Tahoma",8,FontStyle.Bold),ForeColor=Color.FromArgb(164,255,104)};Controls.Add(brand);
  var hide=Make("X",80,0,23,20,"Hide controller — F8 or right-click the edge handle restores it",HideMenu);hide.AccessibleName="Hide controller";hide.ForeColor=Color.FromArgb(255,100,115);
  clock=new GameClock{Bounds=new Rectangle(3,22,100,28),TextAlign=ContentAlignment.MiddleCenter,Font=new Font("Tahoma",10,FontStyle.Bold),Cursor=Cursors.SizeAll};
  clock.Text=DateTime.Now.ToString("h:mm:ss tt");Controls.Add(clock);tips.SetToolTip(clock,"Clock • right-drag any control to move the menu");
  for(int i=1;i<=8;i++){int ship=i;var button=Make(i.ToString(),3+((i-1)%4)*25,52+((i-1)/4)*28,24,26,"Select and locate ship "+i,()=>SendShip(ship));button.AccessibleName="Select and locate ship "+i;shipButtons.Add(button);}
  Make("Store",3,110,74,28,"Open the separate STFC Store window",()=>store.Open(GameRectangle()));
  var closeStore=Make("X",79,110,24,28,"Close Store window only",()=>store.Close());closeStore.AccessibleName="Close Store only";closeStore.ForeColor=Color.FromArgb(255,100,115);
  var mic=Make("\uE720",3,140,49,28,"Open/focus chat, then Windows voice typing",VoiceTyping);mic.Font=new Font("Segoe MDL2 Assets",11);mic.AccessibleName="Open chat and start Windows voice typing";microphone=mic;
  save=Make("\uE74E",54,140,49,28,"Save this account's live settings",SaveSettings);save.Font=new Font("Segoe MDL2 Assets",11);save.AccessibleName="Save settings";
  Make("Diagnostics",3,170,100,28,"Collect diagnostic logs to a Desktop ZIP",CollectDiagnostics);
  Make("Timers",3,200,74,28,"Six named reminders with days, hours, minutes and seconds",ShowTimers);
  var exit=Make("X",79,200,24,28,"Close STFC, Store and controller",ShutdownGame);exit.AccessibleName="Close game and controller";exit.ForeColor=Color.FromArgb(220,130,255);exit.TopColor=Color.FromArgb(134,51,175);exit.BottomColor=Color.FromArgb(65,20,99);
  notice=new Label{Visible=false};
  tray.Text="Morbid Kitty — F8 restores the controller";tray.Icon=Icon;tray.Visible=!preview;
  tray.ContextMenuStrip=new ContextMenuStrip();tray.ContextMenuStrip.Items.Add("Show controller (F8)",null,(o,e)=>ShowMenu());tray.ContextMenuStrip.Items.Add("Timers",null,(o,e)=>ShowTimers());tray.ContextMenuStrip.Items.Add("Reset panel position",null,(o,e)=>{state.UserPosition=false;dirty=true;ShowMenu();});
  tray.ContextMenuStrip.Items.Add("Restore windowed game",null,(o,e)=>{state.Borderless=false;desktop.Restore();dirty=true;});
  tray.ContextMenuStrip.Items.Add("Collect diagnostics",null,(o,e)=>CollectDiagnostics());tray.ContextMenuStrip.Items.Add("Hide controller",null,(o,e)=>HideMenu());
  summon.Summon+=(o,e)=>ShowMenu();
  foreach(Control c in Controls){AttachDrag(c);if(c.Top>=110)toolOffsets[c]=c.Top-110;}
  LayoutShips(preview?255u:0u);
  if(!preview){wake=new EventWaitHandle(false,EventResetMode.AutoReset,PanelWake.EventName(pid));var nativeHandle=Handle;}
  if(showIntro && !preview){intro=new MorbidIntro(root,3000);intro.FormClosed+=(o,e)=>{intro=null;};intro.Show();}
  ResizeEnd+=(o,e)=>SavePlacement();
  FormClosing+=(o,e)=>{if(!closing && e.CloseReason==CloseReason.UserClosing){e.Cancel=true;HideMenu();return;}closing=true;Persist();};
  ui.Interval=50;ui.Tick+=(s,e)=>UpdateUi();ui.Start();RefreshGames();
 }
 bool cleaned;
 protected override void Dispose(bool disposing){if(disposing&&!cleaned){cleaned=true;closing=true;ui.Stop();Persist();DisposeSave();DisposeVoice();desktop.Dispose();store.Close();summon.Close();summon.Dispose();if(restoreHotkey)UnregisterHotKey(Handle,814);if(desktopHotkey)UnregisterHotKey(Handle,815);if(wake!=null)wake.Dispose();tray.Dispose();tips.Dispose();ui.Dispose();if(intro!=null)intro.Close();if(timerWindow!=null)timerWindow.Close();if(hudView!=null)hudView.Dispose();if(hudMap!=null)hudMap.Dispose();}base.Dispose(disposing);}
 void UpdateRestoreHotkey(){uint front;GetWindowThreadProcessId(GetForegroundWindow(),out front);bool wanted=bound!=null&&(front==bound.Id||front==Process.GetCurrentProcess().Id||store.Owns(GetForegroundWindow()));if(wanted){if(!restoreHotkey)restoreHotkey=RegisterHotKey(Handle,814,0x4000,0x77);if(!desktopHotkey)desktopHotkey=RegisterHotKey(Handle,815,0x4000,0x78);}else{if(restoreHotkey){UnregisterHotKey(Handle,814);restoreHotkey=false;}if(desktopHotkey){UnregisterHotKey(Handle,815);desktopHotkey=false;}}}
 void ToggleDesktop(){state.Borderless=!state.Borderless;if(state.Borderless){if(bound!=null)desktop.Apply(bound.Window);}else desktop.Restore();dirty=true;}
 void LayoutShips(uint mask){shipMask=mask;int count=0;for(int i=0;i<shipButtons.Count;i++){bool present=(mask&(1u<<i))!=0;shipButtons[i].Visible=present;if(present){shipButtons[i].Location=new Point(3+(count%4)*25,52+(count/4)*28);count++;}}int y=52+((count+3)/4)*28+2;foreach(var item in toolOffsets)item.Key.Top=y+item.Value;ClientSize=new Size(106,y+122);using(var region=new Region()){region.MakeEmpty();foreach(Control c in Controls){var button=c as Button;int ship=button==null?-1:shipButtons.IndexOf(button);if(ship>=0&&(mask&(1u<<ship))==0)continue;if(c.Width>0&&c.Height>0)region.Union(c.Bounds);}var old=Region;Region=region.Clone();if(old!=null)old.Dispose();}}
 protected override bool ShowWithoutActivation{get{return true;}}
 protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x08000000;return p;}}
 bool dragging,dragArmed;Point dragMouse,dragOrigin;Control dragOwner;
 void AttachDrag(Control c){
  c.MouseDown+=(o,e)=>{if(e.Button!=MouseButtons.Right)return;dragArmed=true;dragging=false;dragMouse=Cursor.Position;dragOrigin=Location;dragOwner=c;c.Capture=true;};
  c.MouseMove+=(o,e)=>{if(!dragArmed)return;var delta=new Point(Cursor.Position.X-dragMouse.X,Cursor.Position.Y-dragMouse.Y);if(Math.Abs(delta.X)+Math.Abs(delta.Y)>=4)dragging=true;if(dragging)Location=new Point(dragOrigin.X+delta.X,dragOrigin.Y+delta.Y);};
  c.MouseUp+=(o,e)=>{if(e.Button!=MouseButtons.Right||!dragArmed)return;dragArmed=false;c.Capture=false;bool moved=dragging;dragging=false;dragOwner=null;if(moved)SavePlacement();};
 }
 protected override void WndProc(ref Message m){if(m.Msg==0x0312 && m.WParam.ToInt32()==814){ShowMenu();return;}if(m.Msg==0x0312&&m.WParam.ToInt32()==815){ToggleDesktop();return;}base.WndProc(ref m);}
 void HideMenu(){hiddenByUser=true;Hide();if(timerWindow!=null)timerWindow.Close();}
 void ShowMenu(){hiddenByUser=false;PlaceForGame();if(preview||HudVisible())Show();}
 void ExitPanel(){closing=true;Close();Application.ExitThread();}
 void ShutdownGame(){var game=RequireGame();store.Close();desktop.Restore();shuttingDown=true;PostMessage(game.Window,0x0010,IntPtr.Zero,IntPtr.Zero);}
 GameButton Make(string text,int x,int y,int width,int height,string tip,Action action) {
  var b=new GameButton{Text=text,Bounds=new Rectangle(x,y,width,height)};
  b.FlatAppearance.BorderColor=Color.FromArgb(158,217,236);b.Click+=(s,e)=>{try{action();}catch(Exception ex){Status(ex.Message,true);}};
  Controls.Add(b);tips.SetToolTip(b,tip);return b;
 }
 void Status(string text,bool error){tips.SetToolTip(clock,text);if(!preview){tray.BalloonTipTitle="Morbid Kitty";tray.BalloonTipText=text;tray.BalloonTipIcon=error?ToolTipIcon.Warning:ToolTipIcon.Info;tray.ShowBalloonTip(3000);}}
 void RefreshGames(){
  if(preview)return;
  string installation=GamePaths.InstalledGame();GameChoice found=null;int count=0;
  foreach(var p in Process.GetProcessesByName("Prime"))try{
   if((attachedPid==0 || p.Id==attachedPid) && p.MainModule!=null && String.Equals(Path.GetDirectoryName(p.MainModule.FileName),installation,StringComparison.OrdinalIgnoreCase)){
    found=new GameChoice{Id=p.Id,Window=p.MainWindowHandle,Title=p.MainWindowTitle,Directory=installation};creation=p.StartTime.ToFileTimeUtc();count++;
   }
  }catch{}finally{p.Dispose();}
  bound=count==1?found:null;
  if(bound!=null && hudMap==null)try{hudMap=MemoryMappedFile.OpenExisting("Local\\STFC.Community.Hud."+bound.Id,MemoryMappedFileRights.Read);hudView=hudMap.CreateViewAccessor(0,64,MemoryMappedFileAccess.Read);}catch{}
 }
 GameChoice Selected(){return bound;}
 [StructLayout(LayoutKind.Sequential)] struct PointNative {public int X,Y;}
 [StructLayout(LayoutKind.Sequential)] struct RectNative {public int Left,Top,Right,Bottom;}
 [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr window,out RectNative rect);
 [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr window,ref PointNative point);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
 [DllImport("kernel32.dll")] static extern ulong GetTickCount64();
 Rectangle GameRectangle(){var g=Selected();RectNative r;if(g==null||g.Window==IntPtr.Zero||!GetClientRect(g.Window,out r))return Rectangle.Empty;var origin=new PointNative();if(!ClientToScreen(g.Window,ref origin))return Rectangle.Empty;return new Rectangle(origin.X,origin.Y,r.Right-r.Left,r.Bottom-r.Top);}
 void PlaceForGame(){
  if(preview||dragging)return;var r=GameRectangle();if(r.IsEmpty)return;
  int x=state.UserPosition?(int)Math.Round(state.RelativeX*r.Width):r.Width-Width-24;
  int y=state.UserPosition?(int)Math.Round(state.RelativeY*r.Height):(int)Math.Round(r.Height*.35);
  positioning=true;Location=new Point(r.Left+Math.Max(0,Math.Min(x,r.Width-Width)),r.Top+Math.Max(0,Math.Min(y,r.Height-Height)));positioning=false;
 }
 void SavePlacement(){if(positioning||preview)return;var r=GameRectangle();if(r.IsEmpty)return;state.RelativeX=(double)(Left-r.Left)/r.Width;state.RelativeY=(double)(Top-r.Top)/r.Height;state.UserPosition=true;dirty=true;Persist();}
 bool HudVisible(){
  if(preview)return true;if(hudView==null||bound==null||bound.Window==IntPtr.Zero||IsIconic(bound.Window))return false;
  try{
   uint first=hudView.ReadUInt32(8);if((first&1)!=0)return false;
   uint magic=hudView.ReadUInt32(0),show=hudView.ReadUInt32(12),pid=hudView.ReadUInt32(32);
   ulong tick=hudView.ReadUInt64(16),created=hudView.ReadUInt64(24),now=GetTickCount64();
   if(first!=hudView.ReadUInt32(8)||magic!=0x31504b4d||hudView.ReadUInt32(4)!=2||pid!=bound.Id||created!=(ulong)creation||tick>now||now-tick>2000||show!=1)return false;
   uint front;GetWindowThreadProcessId(GetForegroundWindow(),out front);
   return front==bound.Id||front==Process.GetCurrentProcess().Id||store.Owns(GetForegroundWindow());
  }catch{return false;}
 }
 GameChoice RequireGame(){
  var choice=Selected();if(choice==null)throw new InvalidOperationException("Open the installed STFC game first.");
  using(var p=Process.GetProcessById(choice.Id)){
   if(p.HasExited || p.MainWindowHandle!=choice.Window || !String.Equals(Path.GetDirectoryName(p.MainModule.FileName),choice.Directory,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("The game window changed. Reopen the game.");
  }
  return choice;
 }
 bool FocusGame(GameChoice g){if(IsIconic(g.Window))ShowWindow(g.Window,9);SetForegroundWindow(g.Window);return GetForegroundWindow()==g.Window;}
 static void Key(byte key,bool up){keybd_event(key,0,up?2u:0u,UIntPtr.Zero);}
 void SendShip(int ship){
  if(ship<1||ship>8)return;var g=RequireGame();if(!FocusGame(g))throw new InvalidOperationException("Click STFC first, then try again.");
  try{using(var request=EventWaitHandle.OpenExisting("Local\\STFC.Community.Ship."+g.Id+"."+ship))request.Set();pendingShip=ship;shipDeadline=DateTime.UtcNow.AddSeconds(3);}
  catch(WaitHandleCannotBeOpenedException){throw new InvalidOperationException("Install the matching controller update and restart STFC to enable ship selection.");}
 }
 void DisposeVoice(){if(voiceReady!=null)voiceReady.Dispose();if(voiceFailed!=null)voiceFailed.Dispose();voiceReady=voiceFailed=null;if(microphone!=null)microphone.Enabled=true;}
 void VoiceTyping(){
  if(voiceReady!=null)return;var g=RequireGame();if(!FocusGame(g))throw new InvalidOperationException("Click STFC first, then try again.");
  try{string prefix="Local\\STFC.Community.Voice.";voiceReady=EventWaitHandle.OpenExisting(prefix+"Ready."+g.Id);voiceFailed=EventWaitHandle.OpenExisting(prefix+"Failed."+g.Id);voiceReady.Reset();voiceFailed.Reset();using(var request=EventWaitHandle.OpenExisting(prefix+"Request."+g.Id))request.Set();voiceDeadline=DateTime.UtcNow.AddSeconds(4);microphone.Enabled=false;}
  catch{DisposeVoice();throw new InvalidOperationException("Install the matching controller update and restart STFC to enable chat voice typing.");}
 }
 void SaveSettings(){
  if(saved!=null)return;var g=RequireGame();
  string prefix="Local\\STFC.Community.Settings.";
  try{
   saved=EventWaitHandle.OpenExisting(prefix+"Saved."+g.Id);failed=EventWaitHandle.OpenExisting(prefix+"Failed."+g.Id);
   saved.Reset();failed.Reset();using(var request=EventWaitHandle.OpenExisting(prefix+"Save."+g.Id))request.Set();
   save.Enabled=false;saveDeadline=DateTime.UtcNow.AddSeconds(15);Status("Saving account settings…",false);
  }catch{DisposeSave();throw new InvalidOperationException("Save is unavailable. Install the matching DLL and enter the game.");}
 }
 void DisposeSave(){if(saved!=null)saved.Dispose();if(failed!=null)failed.Dispose();saved=failed=null;if(save!=null)save.Enabled=true;}
 void CollectDiagnostics(){
  var g=RequireGame();string collector=Path.Combine(root,"Collect-Diagnostics.ps1");
  if(!File.Exists(collector))throw new FileNotFoundException("Keep Collect-Diagnostics.ps1 beside this panel.");
  Process.Start(new ProcessStartInfo("powershell.exe","-STA -NoProfile -ExecutionPolicy Bypass -File \""+collector+"\" -GameDirectory \""+g.Directory+"\""){UseShellExecute=true});
 }
 void Persist(){state.X=Left;state.Y=Top;try{PanelStorage.Save(statePath,state);dirty=false;}catch(Exception ex){Status("Panel settings: "+ex.Message,true);}}
 public static double Remaining(AlarmState a,DateTime now){return Math.Max(0,a.Running?(a.EndUtc-now).TotalSeconds:a.Remaining);}
 public static double Duration(AlarmState a){return a.DurationSeconds>0?a.DurationSeconds:a.Minutes*60;}
 public static void ResetAlarm(AlarmState a){a.Running=false;a.Remaining=Duration(a);}
 void ShowTimers(){
  if(timerWindow!=null && !timerWindow.IsDisposed){timerWindow.Show();timerWindow.Activate();return;}
  countdowns.Clear();startButtons.Clear();
  timerWindow=new Form{Text="Morbid Kitty — six reminder timers",ClientSize=new Size(504,224),StartPosition=FormStartPosition.Manual,FormBorderStyle=FormBorderStyle.FixedToolWindow,TopMost=true,Font=Font,BackColor=Color.FromArgb(6,51,79),ForeColor=ForeColor};
  var game=GameRectangle();if(game.IsEmpty)game=Screen.FromControl(this).WorkingArea;timerWindow.Location=new Point(Math.Max(game.Left,game.Right-timerWindow.Width-14),Math.Max(game.Top,game.Bottom-timerWindow.Height-14));
  string[] headings={"Name","Days","Hrs","Min","Sec","Time left"};int[] columns={8,112,152,192,232,276};
  for(int h=0;h<headings.Length;h++)timerWindow.Controls.Add(new Label{Text=headings[h],Bounds=new Rectangle(columns[h],4,h==0?98:h==5?80:38,18)});
  for(int i=0;i<6;i++){
   var a=state.Alarms[i];int y=23+i*33;
   var name=new TextBox{Text=a.Name,Bounds=new Rectangle(8,y,99,25),AccessibleName="Timer "+(i+1)+" name"};name.TextChanged+=(o,e)=>{a.Name=name.Text;dirty=true;};timerWindow.Controls.Add(name);
   long total=(long)Duration(a);var inputs=new NumericUpDown[4];int[] max={365,23,59,59};int[] values={(int)(total/86400),(int)(total/3600%24),(int)(total/60%60),(int)(total%60)};
   for(int part=0;part<4;part++){inputs[part]=new NumericUpDown{Minimum=0,Maximum=max[part],Value=Math.Max(0,Math.Min(max[part],values[part])),Bounds=new Rectangle(112+part*40,y,36,25),AccessibleName="Timer "+(i+1)+" "+headings[part+1]};timerWindow.Controls.Add(inputs[part]);}
   foreach(var input in inputs)input.ValueChanged+=(o,e)=>{a.DurationSeconds=(long)inputs[0].Value*86400+(long)inputs[1].Value*3600+(long)inputs[2].Value*60+(long)inputs[3].Value;a.Minutes=0;if(!a.Running)ResetAlarm(a);dirty=true;};
   var counter=new Label{Bounds=new Rectangle(276,y+4,80,21),TextAlign=ContentAlignment.MiddleCenter};countdowns.Add(counter);timerWindow.Controls.Add(counter);
   var start=new GameButton{Bounds=new Rectangle(362,y,65,26),Text=a.Running?"Pause":"Start"};startButtons.Add(start);
   start.Click+=(o,e)=>{if(a.Running){a.Remaining=Remaining(a,DateTime.UtcNow);a.Running=false;}else{if(a.Remaining<=0)ResetAlarm(a);if(a.Remaining<=0){Status("Enter a nonzero timer duration",true);return;}a.EndUtc=DateTime.UtcNow.AddSeconds(a.Remaining);a.Running=true;}dirty=true;Persist();};timerWindow.Controls.Add(start);
   var reset=new GameButton{Bounds=new Rectangle(432,y,64,26),Text="Reset"};reset.Click+=(o,e)=>{ResetAlarm(a);dirty=true;Persist();};timerWindow.Controls.Add(reset);
  }
  timerWindow.FormClosed+=(o,e)=>{Persist();countdowns.Clear();startButtons.Clear();timerWindow=null;};timerWindow.Show(this);UpdateUi();
 }
 void UpdateUi(){
  if(closing)return;
  var now=DateTime.UtcNow;
  if(!preview && now>=nextBind){nextBind=now.AddSeconds(1);RefreshGames();if(bound==null && (attachedPid>0||shuttingDown)){ExitPanel();return;}if(bound!=null && state.Borderless)desktop.Apply(bound.Window);}
  if(!preview){
   UpdateRestoreHotkey();
   if(wake!=null && wake.WaitOne(0))ShowMenu();
   bool eligible=intro==null && HudVisible();
   var rect=GameRectangle();if(eligible&&!rect.IsEmpty){summon.Place(rect,Top);if(!summon.Visible)summon.Show();}else summon.Hide();
   if(eligible&&!hiddenByUser){PlaceForGame();if(!Visible)Show();}else{if(Visible)Hide();if(timerWindow!=null&&!eligible)timerWindow.Hide();}
   if(hudView!=null)try{uint seq=hudView.ReadUInt32(8);if((seq&1)==0){uint mask=hudView.ReadUInt32(48),answerSeq=hudView.ReadUInt32(56);int answer=hudView.ReadInt32(52);if(seq==hudView.ReadUInt32(8)){if(mask!=shipMask)LayoutShips(mask);for(int i=0;i<shipButtons.Count;i++)shipButtons[i].Enabled=eligible&&((mask&(1u<<i))!=0);if(pendingShip>0&&answerSeq!=lastShipSequence){lastShipSequence=answerSeq;if(Math.Abs(answer)==pendingShip){if(answer<0)Status("Ship "+pendingShip+" could not be selected in the current game view",true);pendingShip=0;}}}}}catch{}
   if(pendingShip>0&&now>=shipDeadline){pendingShip=0;Status("The game did not confirm the ship request",true);}
  }
  clock.Text=DateTime.Now.ToString("h:mm:ss tt");
  tips.SetToolTip(clock,DateTime.Now.ToString("dddd, MMMM d, yyyy")+" • Right-drag to move • F8 restores");
  for(int i=0;i<state.Alarms.Count;i++){
   var a=state.Alarms[i];double left=Remaining(a,now);
   if(a.Running && left<=0){a.Running=false;a.Remaining=0;dirty=true;System.Media.SystemSounds.Exclamation.Play();Status(a.Name+" finished",false);}
   if(i<countdowns.Count){var span=TimeSpan.FromSeconds(Math.Ceiling(left));countdowns[i].Text=(span.Days>0?span.Days+"d ":"")+span.Hours.ToString("00")+":"+span.Minutes.ToString("00")+":"+span.Seconds.ToString("00");startButtons[i].Text=a.Running?"Pause":"Start";}
  }
  if(voiceReady!=null){if(voiceReady.WaitOne(0)){DisposeVoice();var g=Selected();if(g!=null&&GetForegroundWindow()==g.Window){Key(0x5B,false);Key(0x48,false);Key(0x48,true);Key(0x5B,true);}else Status("Return to STFC and press the mic button again",true);}else if(voiceFailed.WaitOne(0)||now>=voiceDeadline){DisposeVoice();Status("Chat input was not ready; open chat and try the mic again",true);}}
  if(saved!=null){if(saved.WaitOne(0)){DisposeSave();Status("Settings verified on disk",false);}else if(failed.WaitOne(0)){DisposeSave();Status("Settings save failed — see game log",true);}else if(now>=saveDeadline){DisposeSave();Status("Save was not confirmed",true);}}
  if(dirty)Persist();
 }
 void VerifyInteraction(){
  if(TransparencyKey!=Color.Magenta||Region==null||Region.IsVisible(0,0))throw new Exception("Controller background is not clear");
  if(!(clock is GameClock))throw new Exception("Clock must use a beveled box");
  HideMenu();if(Visible||IsDisposed)throw new Exception("Hide closed the controller");
  var message=Message.Create(Handle,0x0312,new IntPtr(814),IntPtr.Zero);WndProc(ref message);if(!Visible||hiddenByUser)throw new Exception("F8 did not restore hidden controller");
  HideMenu();summon.Show();SendMessage(summon.Handle,0x0205,IntPtr.Zero,IntPtr.Zero);Application.DoEvents();if(!Visible||hiddenByUser)throw new Exception("Right-edge handle did not restore hidden controller");summon.Hide();
  var oldCursor=Cursor.Position;var origin=Location;var target=shipButtons[0];Cursor.Position=target.PointToScreen(new Point(8,8));SendMessage(target.Handle,0x0204,new IntPtr(2),new IntPtr(8|(8<<16)));Cursor.Position=new Point(Cursor.Position.X+24,Cursor.Position.Y+15);SendMessage(target.Handle,0x0200,new IntPtr(2),new IntPtr(8|(8<<16)));SendMessage(target.Handle,0x0205,IntPtr.Zero,new IntPtr(8|(8<<16)));Application.DoEvents();if(Location!=new Point(origin.X+24,origin.Y+15))throw new Exception("Right-drag did not move the controller");Location=origin;Cursor.Position=oldCursor;
  LayoutShips(0x05);Application.DoEvents();if(!shipButtons[0].Visible||shipButtons[1].Visible||!shipButtons[2].Visible||shipButtons[2].Left!=28)throw new Exception("Sparse docks were not packed with original dock labels");LayoutShips(255);
  if(!new PanelState().Borderless)throw new Exception("Old panel settings must default to borderless");
  if(state.Alarms.Count<6)throw new Exception("Six timers missing");
  var a=new AlarmState{Name="Days test",DurationSeconds=2*86400+3*3600+4*60+5};ResetAlarm(a);if(a.Remaining!=183845)throw new Exception("Day/hour/minute/second duration failed");
  GameDesktop.Verify();
  if(CommunityStore.ProfileMatches("browser.exe --user-data-dir=\"C:\\other\"", "C:\\owned"))throw new Exception("Store close could target an unrelated browser");
 }
 public void WriteUiEvidence(string directory){
  Directory.CreateDirectory(directory);Show();Application.DoEvents();
  if(Width>=Height)throw new Exception("Panel must be vertical");
  if(!Controls.OfType<Button>().Any(b=>b.Text=="Diagnostics"))throw new Exception("Diagnostics button missing");
  foreach(Control c in Controls){if(!c.Visible)continue;if(!ClientRectangle.Contains(c.Bounds))throw new Exception("Control clipped: "+c.Text);}
  var buttons=Controls.OfType<Button>().ToArray();
  for(int i=0;i<buttons.Length;i++)for(int j=i+1;j<buttons.Length;j++)if(buttons[i].Bounds.IntersectsWith(buttons[j].Bounds))throw new Exception("Buttons overlap");
  LayoutShips(0x0f);
  using(var bmp=new Bitmap(Width,Height)){DrawToBitmap(bmp,new Rectangle(0,0,Width,Height));bmp.Save(Path.Combine(directory,"community-panel.png"));}
  VerifyInteraction();ShowTimers();Application.DoEvents();
  using(var bmp=new Bitmap(timerWindow.Width,timerWindow.Height)){timerWindow.DrawToBitmap(bmp,new Rectangle(0,0,timerWindow.Width,timerWindow.Height));bmp.Save(Path.Combine(directory,"community-timers.png"));}
 }
 [STAThread] public static int Main(string[] args){
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  try{
   if(args.Length==2 && args[0]=="--verify-ui"){
    using(var panel=new CommunityPanel(0,false,true)){panel.WriteUiEvidence(args[1]);panel.closing=true;}
    var a=new AlarmState{Minutes=2,Remaining=42,Running=true,EndUtc=DateTime.UtcNow.AddSeconds(30)};
    if(Remaining(a,DateTime.UtcNow)<28 || Remaining(a,DateTime.UtcNow)>31)throw new Exception("Timer countdown regression");
    ResetAlarm(a);if(a.Running || a.Remaining!=120)throw new Exception("Timer reset regression");
    string test=Path.Combine(args[1],"state-test.json");var state=new PanelState();state.Alarms.Add(a);PanelStorage.Save(test,state);if(PanelStorage.Load(test).Alarms[0].Remaining!=120)throw new Exception("State persistence regression");
    using(var intro=new MorbidIntro(AppDomain.CurrentDomain.BaseDirectory,3000)){
     intro.Shown+=(o,e)=>{Application.DoEvents();using(var bmp=new Bitmap(intro.Width,intro.Height)){intro.DrawToBitmap(bmp,new Rectangle(0,0,intro.Width,intro.Height));bmp.Save(Path.Combine(args[1],"morbid-intro.png"));}};
     intro.ShowDialog();if(intro.ElapsedSeconds<2.8 || intro.ElapsedSeconds>6)throw new Exception("Intro duration regression");
    }
    File.WriteAllText(Path.Combine(args[1],"panel-validation.txt"),"PASS: clear rounded panel and boxed clock; hide/F8/edge restoration; sparse dock layout; borderless monitor and style restoration; right-drag; six named days/hours/minutes/seconds timers; countdown/reset and state persistence; three-second intro closes automatically.\r\n");return 0;
   }
   int pid=0;for(int i=0;i+1<args.Length;i++)if(args[i]=="--attach")Int32.TryParse(args[i+1],out pid);
   if(pid==0){string game=GamePaths.InstalledGame();if(game!=null)foreach(var p in Process.GetProcessesByName("Prime"))try{if(String.Equals(Path.GetDirectoryName(p.MainModule.FileName),game,StringComparison.OrdinalIgnoreCase)){pid=p.Id;break;}}catch{}finally{p.Dispose();}}
   bool created;using(var mutex=new Mutex(true,"Local\\MorbidKitty.Panel."+pid,out created)){
    if(!created){PanelWake.Signal(pid);return 0;}
    using(var panel=new CommunityPanel(pid,Array.IndexOf(args,"--intro")>=0)){
     var context=new ApplicationContext();panel.FormClosed+=(o,e)=>context.ExitThread();Application.Run(context);
    }
   }
   return 0;
  }catch(Exception ex){if(args.Length>0){File.WriteAllText("panel-error.txt",ex.ToString());}else MessageBox.Show(ex.Message,"STFC Community Panel");return 1;}
 }
}
