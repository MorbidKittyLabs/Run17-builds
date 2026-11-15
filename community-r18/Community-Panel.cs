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
[assembly: AssemblyVersion("18.3.0.0")]

[DataContract] public sealed class AlarmState {
 [DataMember] public string Name="Timer";
 [DataMember] public int Minutes=5;
 [DataMember] public double Remaining=300;
 [DataMember] public DateTime EndUtc;
 [DataMember] public bool Running;
}
[DataContract] public sealed class PanelState {
 [DataMember] public int X=40,Y=40;
 [DataMember] public bool UserPosition=false;
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
 bool positioning,closing;
 MorbidIntro intro;
 readonly NotifyIcon tray=new NotifyIcon();
 Label clock,notice;
 Button save;
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
  while(state.Alarms.Count<4)state.Alarms.Add(new AlarmState{Name="Timer "+(state.Alarms.Count+1)});
  Text="Morbid Kitty — STFC";Name="CommunityPanel";ClientSize=new Size(106,212);
  FormBorderStyle=FormBorderStyle.None;MaximizeBox=false;ShowInTaskbar=false;TopMost=true;
  Font=new Font("Tahoma",9);BackColor=Color.FromArgb(6,51,79);ForeColor=Color.FromArgb(244,252,255);
  StartPosition=FormStartPosition.Manual;Location=new Point(40,40);GamePaths.IconFor(this,root);
  clock=new Label{Bounds=new Rectangle(3,2,100,28),TextAlign=ContentAlignment.MiddleCenter,Font=new Font("Tahoma",10,FontStyle.Bold),Cursor=Cursors.SizeAll};
  clock.MouseDown+=(o,e)=>{if(e.Button==MouseButtons.Left){ReleaseCapture();SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero);}};
  clock.MouseUp+=(o,e)=>SavePlacement();
  clock.Text=DateTime.Now.ToString("h:mm:ss tt");Controls.Add(clock);tips.SetToolTip(clock,"Drag the clock to move the panel");
  for(int i=1;i<=8;i++){int ship=i;var button=Make(i.ToString(),3+((i-1)%4)*25,32+((i-1)/4)*28,24,26,"Select ship "+i,()=>SendShip(ship));button.AccessibleName="Select ship "+i;}
  Make("Store",3,90,100,28,"Official STFC Store in your default browser",()=>Process.Start(new ProcessStartInfo("https://home.startrekfleetcommand.com/store"){UseShellExecute=true}));
  var mic=Make("\uE720",3,120,49,28,"Windows voice typing (Win+H)",VoiceTyping);mic.Font=new Font("Segoe MDL2 Assets",11);mic.AccessibleName="Windows voice typing";
  save=Make("\uE74E",54,120,49,28,"Save this account's live settings",SaveSettings);save.Font=new Font("Segoe MDL2 Assets",11);save.AccessibleName="Save settings";
  Make("Diagnostics",3,150,100,28,"Collect diagnostic logs to a Desktop ZIP",CollectDiagnostics);
  Make("Timers",3,180,100,28,"Four saved reminder timers",ShowTimers);
  notice=new Label{Visible=false};
  var menu=new ContextMenuStrip();menu.Items.Add("Reset panel position",null,(o,e)=>{state.UserPosition=false;dirty=true;PlaceForGame();});menu.Items.Add("Close panel",null,(o,e)=>Close());ContextMenuStrip=menu;clock.ContextMenuStrip=menu;
  tray.Text="Morbid Kitty — STFC clock and timers";tray.Icon=Icon;tray.Visible=!preview;
  tray.ContextMenuStrip=new ContextMenuStrip();tray.ContextMenuStrip.Items.Add("Timers",null,(o,e)=>ShowTimers());tray.ContextMenuStrip.Items.Add("Collect diagnostics",null,(o,e)=>CollectDiagnostics());tray.ContextMenuStrip.Items.Add("Close panel",null,(o,e)=>Close());
  if(showIntro && !preview){intro=new MorbidIntro(root,3000);intro.FormClosed+=(o,e)=>{intro=null;};intro.Show();}
  ResizeEnd+=(o,e)=>SavePlacement();
  FormClosing+=(s,e)=>{closing=true;Persist();DisposeSave();tray.Dispose();if(intro!=null)intro.Close();if(timerWindow!=null)timerWindow.Close();if(hudView!=null)hudView.Dispose();if(hudMap!=null)hudMap.Dispose();};
  ui.Interval=200;ui.Tick+=(s,e)=>UpdateUi();ui.Start();RefreshGames();
 }
 bool dragging;
 protected override void WndProc(ref Message m){if(m.Msg==0x231)dragging=true;base.WndProc(ref m);if(m.Msg==0x232){dragging=false;SavePlacement();}}
 Button Make(string text,int x,int y,int width,int height,string tip,Action action) {
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
  if(bound!=null && hudMap==null)try{hudMap=MemoryMappedFile.OpenExisting("Local\\STFC.Community.Hud."+bound.Id,MemoryMappedFileRights.Read);hudView=hudMap.CreateViewAccessor(0,48,MemoryMappedFileAccess.Read);}catch{}
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
   if(first!=hudView.ReadUInt32(8)||magic!=0x31504b4d||hudView.ReadUInt32(4)!=1||pid!=bound.Id||created!=(ulong)creation||tick>now||now-tick>2000||show!=1)return false;
   uint front;GetWindowThreadProcessId(GetForegroundWindow(),out front);
   return front==bound.Id||front==Process.GetCurrentProcess().Id;
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
 void SendShip(int ship){var g=RequireGame();if(!FocusGame(g))throw new InvalidOperationException("Click STFC first, then try again.");Key((byte)(0x30+ship),false);Thread.Sleep(60);Key((byte)(0x30+ship),true);}
 void VoiceTyping(){var g=RequireGame();if(!FocusGame(g))throw new InvalidOperationException("Click STFC first, then try again.");Key(0x5B,false);Key(0x48,false);Key(0x48,true);Key(0x5B,true);}
 void SaveSettings(){
  if(saved!=null)return;var g=RequireGame();
  string prefix="Local\\STFC.Community.Settings.";
  try{
   saved=EventWaitHandle.OpenExisting(prefix+"Saved."+g.Id);failed=EventWaitHandle.OpenExisting(prefix+"Failed."+g.Id);
   saved.Reset();failed.Reset();using(var request=EventWaitHandle.OpenExisting(prefix+"Save."+g.Id))request.Set();
   save.Enabled=false;saveDeadline=DateTime.UtcNow.AddSeconds(15);Status("Saving account settings…",false);
  }catch{DisposeSave();throw new InvalidOperationException("Save is unavailable. Install the matching DLL and enter the game.");}
 }
 void DisposeSave(){if(saved!=null)saved.Dispose();if(failed!=null)failed.Dispose();saved=failed=null;save.Enabled=true;}
 void CollectDiagnostics(){
  var g=RequireGame();string collector=Path.Combine(root,"Collect-Diagnostics.ps1");
  if(!File.Exists(collector))throw new FileNotFoundException("Keep Collect-Diagnostics.ps1 beside this panel.");
  Process.Start(new ProcessStartInfo("powershell.exe","-STA -NoProfile -ExecutionPolicy Bypass -File \""+collector+"\" -GameDirectory \""+g.Directory+"\""){UseShellExecute=true});
 }
 void Persist(){state.X=Left;state.Y=Top;try{PanelStorage.Save(statePath,state);dirty=false;}catch(Exception ex){Status("Panel settings: "+ex.Message,true);}}
 public static double Remaining(AlarmState a,DateTime now){return Math.Max(0,a.Running?(a.EndUtc-now).TotalSeconds:a.Remaining);}
 public static void ResetAlarm(AlarmState a){a.Running=false;a.Remaining=a.Minutes*60;}
 void ShowTimers(){
  if(timerWindow!=null && !timerWindow.IsDisposed){timerWindow.Activate();return;}
  countdowns.Clear();startButtons.Clear();
  timerWindow=new Form{Text="Morbid Kitty — reminder timers",ClientSize=new Size(478,155),FormBorderStyle=FormBorderStyle.FixedToolWindow,TopMost=true,Font=Font,BackColor=BackColor,ForeColor=ForeColor};
  for(int i=0;i<4;i++){
   var a=state.Alarms[i];int y=9+i*35;
   var name=new TextBox{Text=a.Name,Bounds=new Rectangle(8,y,105,25)};name.TextChanged+=(s,e)=>{a.Name=name.Text;dirty=true;};timerWindow.Controls.Add(name);
   var minutes=new NumericUpDown{Minimum=1,Maximum=10080,Value=Math.Max(1,Math.Min(10080,a.Minutes)),Bounds=new Rectangle(118,y,62,25)};
   minutes.ValueChanged+=(s,e)=>{a.Minutes=(int)minutes.Value;if(!a.Running)ResetAlarm(a);dirty=true;};timerWindow.Controls.Add(minutes);
   var unit=new Label{Text="min",Bounds=new Rectangle(183,y+4,28,20)};timerWindow.Controls.Add(unit);
   var counter=new Label{Bounds=new Rectangle(214,y+4,83,21),TextAlign=ContentAlignment.MiddleCenter};countdowns.Add(counter);timerWindow.Controls.Add(counter);
   var start=new GameButton{Bounds=new Rectangle(300,y,80,26),Text=a.Running?"Pause":"Start"};startButtons.Add(start);
   start.Click+=(s,e)=>{if(a.Running){a.Remaining=Remaining(a,DateTime.UtcNow);a.Running=false;}else{if(a.Remaining<=0)ResetAlarm(a);a.EndUtc=DateTime.UtcNow.AddSeconds(a.Remaining);a.Running=true;}dirty=true;Persist();};timerWindow.Controls.Add(start);
   var reset=new GameButton{Bounds=new Rectangle(385,y,83,26),Text="Reset"};reset.Click+=(s,e)=>{ResetAlarm(a);dirty=true;Persist();};timerWindow.Controls.Add(reset);
  }
  timerWindow.FormClosed+=(s,e)=>{Persist();countdowns.Clear();startButtons.Clear();timerWindow=null;};timerWindow.Show(this);UpdateUi();
 }
 void UpdateUi(){
  if(closing)return;
  var now=DateTime.UtcNow;
  if(!preview && now>=nextBind){nextBind=now.AddSeconds(1);RefreshGames();if(bound==null && attachedPid>0){Close();Application.ExitThread();return;}}
  if(!preview){bool show=intro==null && HudVisible();if(show){PlaceForGame();if(!Visible)Show();}else{if(Visible)Hide();if(timerWindow!=null)timerWindow.Hide();}}
  clock.Text=DateTime.Now.ToString("h:mm:ss tt");
  tips.SetToolTip(clock,DateTime.Now.ToString("dddd, MMMM d, yyyy")+" • Drag to move");
  for(int i=0;i<state.Alarms.Count;i++){
   var a=state.Alarms[i];double left=Remaining(a,now);
   if(a.Running && left<=0){a.Running=false;a.Remaining=0;dirty=true;System.Media.SystemSounds.Exclamation.Play();Status(a.Name+" finished",false);}
   if(i<countdowns.Count){var span=TimeSpan.FromSeconds(Math.Ceiling(left));countdowns[i].Text=((int)span.TotalHours).ToString("00")+":"+span.Minutes.ToString("00")+":"+span.Seconds.ToString("00");startButtons[i].Text=a.Running?"Pause":"Start";}
  }
  if(saved!=null){if(saved.WaitOne(0)){DisposeSave();Status("Settings verified on disk",false);}else if(failed.WaitOne(0)){DisposeSave();Status("Settings save failed — see game log",true);}else if(now>=saveDeadline){DisposeSave();Status("Save was not confirmed",true);}}
  if(dirty)Persist();
 }
 public void WriteUiEvidence(string directory){
  Directory.CreateDirectory(directory);Show();Application.DoEvents();
  if(Width>=Height)throw new Exception("Panel must be vertical");
  if(!Controls.OfType<Button>().Any(b=>b.Text=="Diagnostics"))throw new Exception("Diagnostics button missing");
  foreach(Control c in Controls){if(!c.Visible)continue;if(!ClientRectangle.Contains(c.Bounds))throw new Exception("Control clipped: "+c.Text);}
  var buttons=Controls.OfType<Button>().ToArray();
  for(int i=0;i<buttons.Length;i++)for(int j=i+1;j<buttons.Length;j++)if(buttons[i].Bounds.IntersectsWith(buttons[j].Bounds))throw new Exception("Buttons overlap");
  using(var bmp=new Bitmap(Width,Height)){DrawToBitmap(bmp,new Rectangle(0,0,Width,Height));bmp.Save(Path.Combine(directory,"community-panel.png"));}
  ShowTimers();Application.DoEvents();
  using(var bmp=new Bitmap(timerWindow.Width,timerWindow.Height)){timerWindow.DrawToBitmap(bmp,new Rectangle(0,0,timerWindow.Width,timerWindow.Height));bmp.Save(Path.Combine(directory,"community-timers.png"));}
 }
 [STAThread] public static int Main(string[] args){
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  try{
   if(args.Length==2 && args[0]=="--verify-ui"){
    using(var panel=new CommunityPanel(0,false,true)){panel.WriteUiEvidence(args[1]);}
    var a=new AlarmState{Minutes=2,Remaining=42,Running=true,EndUtc=DateTime.UtcNow.AddSeconds(30)};
    if(Remaining(a,DateTime.UtcNow)<28 || Remaining(a,DateTime.UtcNow)>31)throw new Exception("Timer countdown regression");
    ResetAlarm(a);if(a.Running || a.Remaining!=120)throw new Exception("Timer reset regression");
    string test=Path.Combine(args[1],"state-test.json");var state=new PanelState();state.Alarms.Add(a);PanelStorage.Save(test,state);if(PanelStorage.Load(test).Alarms[0].Remaining!=120)throw new Exception("State persistence regression");
    using(var intro=new MorbidIntro(AppDomain.CurrentDomain.BaseDirectory,3000)){
     intro.Shown+=(o,e)=>{Application.DoEvents();using(var bmp=new Bitmap(intro.Width,intro.Height)){intro.DrawToBitmap(bmp,new Rectangle(0,0,intro.Width,intro.Height));bmp.Save(Path.Combine(args[1],"morbid-intro.png"));}};
     intro.ShowDialog();if(intro.ElapsedSeconds<2.8 || intro.ElapsedSeconds>6)throw new Exception("Intro duration regression");
    }
    File.WriteAllText(Path.Combine(args[1],"panel-validation.txt"),"PASS: vertical blue panel with Diagnostics; four timers; countdown/reset and state persistence; three-second intro closes automatically.\r\n");return 0;
   }
   int pid=0;for(int i=0;i+1<args.Length;i++)if(args[i]=="--attach")Int32.TryParse(args[i+1],out pid);
   bool created;using(var mutex=new Mutex(true,"Local\\MorbidKitty.Panel."+pid,out created)){
    if(!created)return 0;
    using(var panel=new CommunityPanel(pid,Array.IndexOf(args,"--intro")>=0)){
     var context=new ApplicationContext();panel.FormClosed+=(o,e)=>context.ExitThread();Application.Run(context);
    }
   }
   return 0;
  }catch(Exception ex){if(args.Length>0){File.WriteAllText("panel-error.txt",ex.ToString());}else MessageBox.Show(ex.Message,"STFC Community Panel");return 1;}
 }
}
