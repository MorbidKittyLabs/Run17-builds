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
[assembly: AssemblyTitle("Morbid Kitty Controller")]
[assembly: AssemblyDescription("Single-account clock, timers and manual shortcuts")]
[assembly: AssemblyVersion("18.0.0.0")]
[assembly:AssemblyInformationalVersion("Run17 R18")]

[DataContract] public sealed class AlarmState {
 [DataMember] public string Name="Timer";
 [DataMember] public int Minutes=5;
 [DataMember] public long DurationSeconds;
 [DataMember] public double Remaining=300;
 [DataMember] public DateTime EndUtc;
 [DataMember] public bool Running;
 [DataMember] public bool Finished;
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
public sealed class TimerAudio:IDisposable {
 readonly object gate=new object();readonly string path;int pending;bool working,disposed;
 public TimerAudio(string path){this.path=path;}
 public void Enqueue(){
  if(!File.Exists(path))return;
  lock(gate){if(disposed)return;pending++;if(working)return;working=true;ThreadPool.QueueUserWorkItem(o=>PlayQueued());}
 }
 void PlayQueued(){
  try{
   using(var player=new System.Media.SoundPlayer(path)){
    player.Load();
    while(true){
     lock(gate){if(disposed||pending==0){working=false;return;}pending--;}
     player.PlaySync();
     // Pause between announcements without blocking the controller or game.
     Thread.Sleep(150);
    }
   }
  }catch{lock(gate){pending=0;working=false;}}
 }
 public void Dispose(){lock(gate){disposed=true;pending=0;}}
}
public sealed class TimerMenu:Form {
 bool moving;MouseButtons dragButton;Point dragMouse,dragOrigin;
 public TimerMenu(){
  AutoScaleMode=AutoScaleMode.None;MinimumSize=new Size(1,1);
  FormBorderStyle=FormBorderStyle.None;ControlBox=false;MinimizeBox=false;MaximizeBox=false;ShowInTaskbar=false;TopMost=true;StartPosition=FormStartPosition.Manual;
  BackColor=Color.FromArgb(6,51,79);DoubleBuffered=true;
  MouseDown+=(o,e)=>BeginMove(this,e);
  FormClosing+=(o,e)=>{moving=false;Capture=false;};
 }
 protected override void OnControlAdded(ControlEventArgs e){base.OnControlAdded(e);var control=e.Control;control.MouseDown+=(o,m)=>BeginMove(control,m);}
 void BeginMove(Control source,MouseEventArgs e){
  if(e.Button!=MouseButtons.Right&&(e.Button!=MouseButtons.Left||(source!=this&&!(source is Label))))return;
  dragButton=e.Button;dragMouse=Cursor.Position;dragOrigin=Location;moving=true;Capture=true;
 }
 protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(moving){var p=Cursor.Position;Location=new Point(dragOrigin.X+p.X-dragMouse.X,dragOrigin.Y+p.Y-dragMouse.Y);}}
 protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(moving&&e.Button==dragButton){moving=false;Capture=false;}}
 protected override void OnMouseCaptureChanged(EventArgs e){if(!Capture)moving=false;base.OnMouseCaptureChanged(e);}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);BeveledFace.Paint(e.Graphics,ClientRectangle,String.Empty,Font,ForeColor,Color.FromArgb(13,100,145),Color.FromArgb(6,51,79));}
 protected override void OnSizeChanged(EventArgs e){base.OnSizeChanged(e);if(Width>0&&Height>0)using(var path=BeveledFace.Shape(ClientRectangle,5)){var old=Region;Region=new Region(path);if(old!=null)old.Dispose();}}
}
public sealed class TimerNotice:Form {
 readonly GameButton caption,close;
 public TimerNotice(string name,Font font,Action dismiss){
  AutoScaleMode=AutoScaleMode.None;MinimumSize=new Size(1,1);
  Text="Timer finished";FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;StartPosition=FormStartPosition.Manual;BackColor=Color.FromArgb(6,51,79);Font=font;
  caption=new GameButton{PreserveGameFocus=true,JoinRight=true,Text=name,Font=font,AccessibleName="Finished timer: "+name};
  close=new GameButton{PreserveGameFocus=true,Text="X",JoinLeft=true,Font=font,ForeColor=Color.White,TopColor=Color.FromArgb(205,55,67),BottomColor=Color.FromArgb(119,24,36),AccessibleName="Dismiss finished timer"};
  close.Click+=(o,e)=>dismiss();Controls.Add(caption);Controls.Add(close);Fit(name,600);
 }
 protected override bool ShowWithoutActivation{get{return true;}}
 protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x08000000;return p;}}
 public void Fit(string name,int availableWidth,float scale=1,Font font=null){
  if(font!=null)Font=font;int height=Math.Max(14,(int)Math.Round(28*scale)),minimum=(int)Math.Round(110*scale);
  name=String.IsNullOrWhiteSpace(name)?"Timer finished":name;
  int width=Math.Max(minimum,Math.Min(Math.Max(minimum,availableWidth-height),TextRenderer.MeasureText(name,Font,Size.Empty,TextFormatFlags.SingleLine|TextFormatFlags.NoPadding).Width+(int)(20*scale)));
  caption.Font=Font;close.Font=Font;caption.Text=name;caption.AccessibleName="Finished timer: "+name;caption.Bounds=new Rectangle(0,0,width,height);close.Bounds=new Rectangle(width,0,height,height);ClientSize=new Size(width+height,height);
  using(var path=BeveledFace.Shape(ClientRectangle,5)){var old=Region;Region=new Region(path);if(old!=null)old.Dispose();}
 }
}

public sealed class DockShortcut:Form {
 readonly GameButton button;
 Font numberFont;
 public DockShortcut(int number,Action select){
  AutoScaleMode=AutoScaleMode.None;MinimumSize=new Size(1,1);
  FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;StartPosition=FormStartPosition.Manual;BackColor=Color.Magenta;TransparencyKey=Color.Magenta;
  button=new GameButton{PreserveGameFocus=true,Text=number.ToString(),Dock=DockStyle.Fill,AccessibleName="Select and locate ship "+number};button.Click+=(o,e)=>select();Controls.Add(button);
 }
 protected override bool ShowWithoutActivation{get{return true;}}
 protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x08000000;return p;}}
 public void Place(Rectangle dock,float scale){
  int size=Math.Max(20,Math.Min((int)Math.Round(22*scale),Math.Min(dock.Width,dock.Height)/2));ClientSize=new Size(size,size);
  Location=new Point(dock.Left+2,Math.Max(dock.Top,dock.Bottom-size-(int)Math.Round(14*scale)));if(numberFont==null||Math.Abs(numberFont.Size-Math.Round(Math.Max(7,9*scale)*4f/3f,2))>.01){numberFont=PixelFonts.Create("Tahoma",Math.Max(7,9*scale),FontStyle.Bold);button.Font=numberFont;}
 }
 protected override void Dispose(bool disposing){base.Dispose(disposing);if(disposing&&numberFont!=null){numberFont=null;}}
}

public sealed class CommunityPanel:Form,IMessageFilter {
 [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
 [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr window,int command);
 [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
 [DllImport("user32.dll")] static extern void keybd_event(byte key,byte scan,uint flags,UIntPtr extra);
 [DllImport("user32.dll")] static extern bool ReleaseCapture();
 [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd,uint message,IntPtr w,IntPtr l);
 readonly string root=AppDomain.CurrentDomain.BaseDirectory;
 readonly TimerAudio timerAudio=new TimerAudio(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Timer-Complete.wav"));
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
 readonly UiWork<VoiceProbe> voiceProbe=new UiWork<VoiceProbe>();
 readonly UiWork<bool> desktopWork=new UiWork<bool>();
 IntPtr desktopWantedWindow;bool desktopWantedBorderless;int desktopRequest,desktopCompleted;
 readonly SummonHandle summon=new SummonHandle();
 readonly DockShortcut[] dockShortcuts=new DockShortcut[8];
 readonly Dictionary<Control,Rectangle> controllerBounds=new Dictionary<Control,Rectangle>();
 const float ControllerSize=2f/3f;
 float uiScale=1,timerScale=1;Font timerFont;
 Rectangle placedGame;Size placedSize;bool placementDirty=true;
 EventWaitHandle wake;
 bool restoreHotkey,desktopHotkey;
  int pendingShip;uint lastShipSequence;DateTime shipDeadline;
 [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h,int id,uint modifiers,uint key);
 [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h,int id);
 [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h,uint message,IntPtr w,IntPtr l);
 MorbidIntro intro;
 readonly NotifyIcon tray=new NotifyIcon();
 Label clock;
 Button save,microphone;EventWaitHandle voiceReady,voiceFailed;DateTime voiceDeadline;bool voiceSession,voiceClosing;
 readonly ToolTip tips=new ToolTip();
 readonly System.Windows.Forms.Timer ui=new System.Windows.Forms.Timer();
 readonly List<Label> countdowns=new List<Label>();
 readonly List<Button> startButtons=new List<Button>();
 EventWaitHandle saved,failed;
 DateTime saveDeadline;
 Form timerWindow;
 readonly Dictionary<Control,Rectangle> timerBounds=new Dictionary<Control,Rectangle>();
 readonly Dictionary<int,TimerNotice> timerNotices=new Dictionary<int,TimerNotice>();
 bool dirty;
 public CommunityPanel(int pid=0,bool showIntro=false,bool isPreview=false) {
  AutoScaleMode=AutoScaleMode.None;MinimumSize=new Size(1,1);
  preview=isPreview;attachedPid=pid;
  statePath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"STFC-Community","Panel.json");
  state=PanelStorage.Load(statePath);
  if(state==null)state=new PanelState();if(state.Alarms==null)state.Alarms=new List<AlarmState>();
  while(state.Alarms.Count<6)state.Alarms.Add(new AlarmState{Name="Timer "+(state.Alarms.Count+1)});
  Text="Morbid Kitty — STFC";Name="CommunityPanel";ClientSize=new Size(106,232);
  FormBorderStyle=FormBorderStyle.None;MaximizeBox=false;ShowInTaskbar=false;TopMost=true;
  Font=PixelFonts.Create("Tahoma",9);BackColor=Color.Magenta;TransparencyKey=Color.Magenta;ForeColor=Color.FromArgb(244,252,255);
  StartPosition=FormStartPosition.Manual;Location=new Point(40,40);GamePaths.IconFor(this,root);
  clock=new GameClock{Bounds=new Rectangle(3,0,100,28),TextAlign=ContentAlignment.MiddleCenter,Font=PixelFonts.Create("Tahoma",10,FontStyle.Bold),Cursor=Cursors.SizeAll};
  clock.Text=DateTime.Now.ToString("h:mm:ss tt");Controls.Add(clock);tips.SetToolTip(clock,"Clock • right-drag any control to move the menu");
  Make("Timers",3,30,74,28,"Six named reminders with days, hours, minutes and seconds",ShowTimers);
  var hide=Make("X",79,30,24,28,"Hide controller — F8 or the green edge arrow restores it",HideMenu);hide.AccessibleName="Hide controller";hide.ForeColor=Color.White;hide.TopColor=Color.FromArgb(205,55,67);hide.BottomColor=Color.FromArgb(119,24,36);
  Make("Diagnostics",3,60,74,28,"Collect diagnostic logs to a Desktop ZIP",CollectDiagnostics);
  var exit=Make("X",79,60,24,28,"Close STFC, Store and controller",ShutdownGame);exit.AccessibleName="Close game and controller";exit.ForeColor=Color.White;exit.TopColor=Color.FromArgb(134,51,175);exit.BottomColor=Color.FromArgb(65,20,99);
  Make("Store",3,90,74,28,"Open the separate STFC Store window",()=>store.Open(GameRectangle(),error=>{if(error!=null)Status(error.Message,true);}));
  var closeStore=Make("X",79,90,24,28,"Close Store window only",()=>store.Close());closeStore.AccessibleName="Close Store only";closeStore.ForeColor=Color.White;closeStore.TopColor=Color.FromArgb(205,55,67);closeStore.BottomColor=Color.FromArgb(119,24,36);
  var mic=Make("\uE720",3,120,49,28,"Toggle chat and Windows voice typing together",VoiceTyping);mic.Font=PixelFonts.Create("Segoe MDL2 Assets",11);mic.AccessibleName="Toggle chat and Windows voice typing";microphone=mic;
  save=Make("\uE74E",54,120,49,28,"Save this account's live settings",SaveSettings);save.Font=PixelFonts.Create("Segoe MDL2 Assets",11);save.AccessibleName="Save settings";
  for(int i=0;i<8;i++){int ship=i+1;dockShortcuts[i]=new DockShortcut(ship,()=>{try{SendShip(ship);}catch(Exception ex){Status(ex.Message,true);}});}
  tray.Text="Morbid Kitty — F8 restores the controller";tray.Icon=Icon;tray.Visible=!preview;
  tray.ContextMenuStrip=new ContextMenuStrip();tray.ContextMenuStrip.Items.Add("Show controller (F8)",null,(o,e)=>ShowMenu());tray.ContextMenuStrip.Items.Add("Timers",null,(o,e)=>ShowTimers());tray.ContextMenuStrip.Items.Add("Reset panel position",null,(o,e)=>{state.UserPosition=false;dirty=true;ShowMenu();});
  tray.ContextMenuStrip.Items.Add("Restore windowed game",null,(o,e)=>{state.Borderless=false;RequestDesktop(IntPtr.Zero,false);dirty=true;});
  tray.ContextMenuStrip.Items.Add("Collect diagnostics",null,(o,e)=>CollectDiagnostics());tray.ContextMenuStrip.Items.Add("Hide controller",null,(o,e)=>HideMenu());
  summon.Summon+=(o,e)=>ShowMenu();
  foreach(Control c in Controls){AttachDrag(c);controllerBounds[c]=c.Bounds;}
  LayoutController(1);
  InstallGapDrag();
  if(!preview){wake=new EventWaitHandle(false,EventResetMode.AutoReset,PanelWake.EventName(pid));var nativeHandle=Handle;}
  if(showIntro && !preview){intro=new MorbidIntro(root,3000,()=>bound!=null&&GameDesktop.IsEstablished(bound.Window,state.Borderless),()=>bound!=null&&bound.Window!=IntPtr.Zero?Screen.FromHandle(bound.Window).Bounds:Screen.FromPoint(Cursor.Position).Bounds);intro.FormClosed+=(o,e)=>{intro=null;};intro.Show();}
  ResizeEnd+=(o,e)=>SavePlacement();
  FormClosing+=(o,e)=>{if(!closing && e.CloseReason==CloseReason.UserClosing){e.Cancel=true;HideMenu();return;}closing=true;Persist();};
  ui.Interval=50;ui.Tick+=(s,e)=>UpdateUi();ui.Start();RefreshGames();
 }
 bool cleaned;
 protected override void Dispose(bool disposing){if(disposing&&!cleaned){cleaned=true;closing=true;ui.Stop();timerAudio.Dispose();if(gapHook!=IntPtr.Zero)UnhookWindowsHookEx(gapHook);if(filterInstalled)Application.RemoveMessageFilter(this);foreach(var badge in dockShortcuts)if(badge!=null){badge.Close();badge.Dispose();}Persist();DisposeSave();DisposeVoice();voiceProbe.Dispose();desktopWork.Dispose();if(!desktopWork.Busy)ThreadPool.QueueUserWorkItem(o=>desktop.Dispose());store.Dispose();foreach(var notice in timerNotices.Values.ToArray()){notice.Close();notice.Dispose();}timerNotices.Clear();summon.Close();summon.Dispose();if(restoreHotkey)UnregisterHotKey(Handle,814);if(desktopHotkey)UnregisterHotKey(Handle,815);if(wake!=null)wake.Dispose();tray.Dispose();tips.Dispose();ui.Dispose();if(intro!=null)intro.Close();if(timerWindow!=null)timerWindow.Close();if(hudView!=null)hudView.Dispose();if(hudMap!=null)hudMap.Dispose();}base.Dispose(disposing);}
 void UpdateRestoreHotkey(){uint front;GetWindowThreadProcessId(GetForegroundWindow(),out front);bool wanted=bound!=null&&(front==bound.Id||front==Process.GetCurrentProcess().Id||(voiceSession&&WindowsVoice.Owns(front))||store.Owns(GetForegroundWindow()));if(wanted){if(!restoreHotkey)restoreHotkey=RegisterHotKey(Handle,814,0x4000,0x77);if(!desktopHotkey)desktopHotkey=RegisterHotKey(Handle,815,0x4000,0x78);}else{if(restoreHotkey){UnregisterHotKey(Handle,814);restoreHotkey=false;}if(desktopHotkey){UnregisterHotKey(Handle,815);desktopHotkey=false;}}}
 void ToggleDesktop(){state.Borderless=!state.Borderless;RequestDesktop(bound==null?IntPtr.Zero:bound.Window,state.Borderless);dirty=true;}
 void LayoutController(float scale){
  uiScale=scale;placementDirty=true;
  timerScale=Math.Max(1f,scale);timerFont=PixelFonts.Create("Tahoma",9*timerScale);
  if(timerWindow!=null)timerWindow.Font=timerFont;
  foreach(var notice in timerNotices.Values)notice.Fit("Timer finished",600,timerScale,timerFont);
  scale=Math.Max(1f,scale*ControllerSize);
  SuspendLayout();
  foreach(var pair in controllerBounds){var r=pair.Value;pair.Key.Bounds=new Rectangle((int)Math.Round(r.X*scale),(int)Math.Round(r.Y*scale),Math.Max(1,(int)Math.Round(r.Width*scale)),Math.Max(1,(int)Math.Round(r.Height*scale)));}
  Font=PixelFonts.Create("Tahoma",9*scale);
  clock.Font=PixelFonts.Create("Tahoma",10*scale,FontStyle.Bold);
  foreach(var button in new[]{microphone,save}){button.Font=PixelFonts.Create("Segoe MDL2 Assets",11*scale);}
  ClientSize=new Size((int)Math.Round(106*scale),(int)Math.Round(150*scale));
  ResumeLayout();
  using(var region=new Region()){
   region.MakeEmpty();
   foreach(Control c in Controls)if(c.Width>0&&c.Height>0)region.Union(c.Bounds);
   var old=Region;Region=region.Clone();if(old!=null)old.Dispose();
  }
 }
 delegate IntPtr MouseHook(int code,IntPtr message,IntPtr data);
 [StructLayout(LayoutKind.Sequential)] struct MouseData {public Point Point;public uint Mouse,Flags,Time;public UIntPtr Extra;}
 [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int id,MouseHook callback,IntPtr module,uint thread);
 [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
 [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);
 [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Point point);
 [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr h,uint flags);
 [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
 MouseHook gapCallback;IntPtr gapHook;bool filterInstalled;
 bool dragging;Point dragMouse,dragOrigin;
 void BeginDrag(Point p){dragMouse=p;dragOrigin=Location;dragging=true;Capture=true;}
 void EndDrag(){if(!dragging)return;dragging=false;Capture=false;SavePlacement();}
 void InstallGapDrag(){Application.AddMessageFilter(this);filterInstalled=true;gapCallback=GapDrag;gapHook=SetWindowsHookEx(14,gapCallback,GetModuleHandle(null),0);}
 IntPtr GapDrag(int code,IntPtr message,IntPtr data){
  if(code>=0&&message.ToInt32()==0x0204&&Visible&&!dragging){
   var p=((MouseData)Marshal.PtrToStructure(data,typeof(MouseData))).Point;
   if(RectangleToScreen(ClientRectangle).Contains(p)){
    var owner=GetAncestor(WindowFromPoint(p),2);
    if(owner==Handle||(bound!=null&&owner==bound.Window)){
     // Existing child controls receive right-down through the normal message loop.
     if(owner!=Handle){BeginDrag(p);return new IntPtr(1);}
    }
   }
  }
  return CallNextHookEx(gapHook,code,message,data);
 }
 protected override bool ShowWithoutActivation{get{return true;}}
 protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x08000000;return p;}}
 protected override void OnMouseCaptureChanged(EventArgs e){if(!Capture)dragging=false;base.OnMouseCaptureChanged(e);}
 void AttachDrag(Control c){}
 public bool PreFilterMessage(ref Message m){
  if(IsDisposed||!Visible)return false;
  if(dragging){
   if(m.Msg==0x0200){var p=Cursor.Position;Location=new Point(dragOrigin.X+p.X-dragMouse.X,dragOrigin.Y+p.Y-dragMouse.Y);return true;}
   if(m.Msg==0x0205){EndDrag();return true;}
   if(m.Msg==0x0100&&m.WParam.ToInt32()==0x1b){dragging=false;Capture=false;return true;}
  }
  if(m.Msg!=0x0204)return false;
  var target=Control.FromHandle(m.HWnd);if(target!=this&&(target==null||!Contains(target)))return false;
  BeginDrag(Cursor.Position);return true;
 }
 protected override void WndProc(ref Message m){if(m.Msg==0x0021){m.Result=new IntPtr(3);return;}if(m.Msg==0x0312 && m.WParam.ToInt32()==814){ShowMenu();return;}if(m.Msg==0x0312&&m.WParam.ToInt32()==815){ToggleDesktop();return;}base.WndProc(ref m);}
 void HideMenu(){dragging=false;Capture=false;hiddenByUser=true;Hide();if(timerWindow!=null)timerWindow.Close();}
 void ShowMenu(){hiddenByUser=false;PlaceForGame();if(preview||HudVisible())Show();}
 void ExitPanel(){closing=true;Close();Application.ExitThread();}
 void ShutdownGame(){var game=RequireGame();store.Close();shuttingDown=true;PostMessage(game.Window,0x0010,IntPtr.Zero,IntPtr.Zero);}
 GameButton Make(string text,int x,int y,int width,int height,string tip,Action action) {
  var b=new GameButton{PreserveGameFocus=true,Text=text,Bounds=new Rectangle(x,y,width,height),TopColor=Color.FromArgb(20,130,185),BottomColor=Color.FromArgb(5,69,112)};
  b.FlatAppearance.BorderColor=Color.FromArgb(158,217,236);b.Click+=(s,e)=>{try{if(preview&&text=="Store")return;action();}catch(Exception ex){Status(ex.Message,true);}};
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
  if(bound!=null && hudMap==null)try{hudMap=MemoryMappedFile.OpenExisting("Local\\STFC.Community.Hud."+bound.Id,MemoryMappedFileRights.Read);hudView=hudMap.CreateViewAccessor(0,200,MemoryMappedFileAccess.Read);}catch{}
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
  if(preview||dragging)return;var r=GameRectangle();if(r.IsEmpty)return;if(!placementDirty&&r==placedGame&&Size==placedSize)return;placedGame=r;placedSize=Size;placementDirty=false;
  int x=state.UserPosition?(int)Math.Round(state.RelativeX*r.Width):r.Width-Width-24;
  int y=state.UserPosition?(int)Math.Round(state.RelativeY*r.Height):(int)Math.Round(r.Height*.35);
  positioning=true;Location=new Point(r.Left+Math.Max(0,Math.Min(x,r.Width-Width)),r.Top+Math.Max(0,Math.Min(y,r.Height-Height)));positioning=false;
 }
 void SavePlacement(){if(positioning||preview)return;var r=GameRectangle();if(r.IsEmpty)return;state.RelativeX=(double)(Left-r.Left)/r.Width;state.RelativeY=(double)(Top-r.Top)/r.Height;state.UserPosition=true;placementDirty=true;dirty=true;Persist();}
 bool HudVisible(){
  if(preview)return true;if(hudView==null||bound==null||bound.Window==IntPtr.Zero||IsIconic(bound.Window))return false;
  try{
   uint first=hudView.ReadUInt32(8);if((first&1)!=0)return false;
   uint magic=hudView.ReadUInt32(0),show=hudView.ReadUInt32(12),pid=hudView.ReadUInt32(32);
   ulong tick=hudView.ReadUInt64(16),created=hudView.ReadUInt64(24),now=GetTickCount64();
   if(first!=hudView.ReadUInt32(8)||magic!=0x31504b4d||hudView.ReadUInt32(4)!=3||pid!=bound.Id||created!=(ulong)creation||tick>now||now-tick>2000||show!=1)return false;
   uint front;GetWindowThreadProcessId(GetForegroundWindow(),out front);
   return front==bound.Id||front==Process.GetCurrentProcess().Id||(voiceSession&&WindowsVoice.Owns(front))||store.Owns(GetForegroundWindow());
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
  if(voiceReady!=null||voiceProbe.Busy)return;var g=RequireGame();if(!FocusGame(g))throw new InvalidOperationException("Click STFC first, then try again.");
  microphone.Enabled=false;bool wantedClose=voiceSession;
  voiceProbe.Start(()=>{
   bool open=WindowsVoice.IsOpen(),toggle=false;bool close=wantedClose||open;
   if(close)WindowsVoice.DismissOrNeedsToggle(out toggle);
   return new VoiceProbe{Open=open,Closing=close,NeedsToggle=toggle};
  },TimeSpan.FromSeconds(3),(result,error)=>{
   if(error!=null){microphone.Enabled=true;Status(error.Message,true);return;}
   try{
    if(bound==null||bound.Id!=g.Id||!FocusGame(g)){microphone.Enabled=true;return;}
    voiceClosing=result.Closing;if(voiceClosing&&result.NeedsToggle)ToggleWindowsVoice();
    string prefix="Local\\STFC.Community.Voice.";
    voiceReady=EventWaitHandle.OpenExisting(prefix+(voiceClosing?"Closed.":"Ready.")+g.Id);voiceFailed=EventWaitHandle.OpenExisting(prefix+(voiceClosing?"CloseFailed.":"Failed.")+g.Id);voiceReady.Reset();voiceFailed.Reset();
    using(var request=EventWaitHandle.OpenExisting(prefix+(voiceClosing?"Close.":"Request.")+g.Id))request.Set();voiceDeadline=DateTime.UtcNow.AddSeconds(4);
   }catch{DisposeVoice();Status("Chat voice request is unavailable for this game process.",true);}
  });
 }
 void FinishVoiceOpen(){
  var g=Selected();if(g==null||GetForegroundWindow()!=g.Window){Status("Return to STFC and press the mic button again",true);return;}
  microphone.Enabled=false;
  voiceProbe.Start(()=>new VoiceProbe{Open=WindowsVoice.IsOpen()},TimeSpan.FromSeconds(3),(result,error)=>{
   microphone.Enabled=true;if(error!=null){Status(error.Message,true);return;}
   if(bound==null||bound.Id!=g.Id||GetForegroundWindow()!=g.Window)return;
   if(!result.Open)ToggleWindowsVoice();voiceSession=true;
  });
 }
 void RequestDesktop(IntPtr window,bool borderless){
  if(window==desktopWantedWindow&&borderless==desktopWantedBorderless&&desktopRequest>0)return;
  desktopWantedWindow=window;desktopWantedBorderless=borderless;desktopRequest++;
 }
 void PumpDesktop(DateTime now){
  desktopWork.Pump(now);
  if(desktopRequest==desktopCompleted||desktopWork.Busy||closing)return;
  int request=desktopRequest;IntPtr window=desktopWantedWindow;bool borderless=desktopWantedBorderless;
  desktopWork.Start(()=>{if(borderless)desktop.Apply(window);else desktop.Restore();if(closing)desktop.Restore();return true;},TimeSpan.FromSeconds(2),(ok,error)=>{if(error==null)desktopCompleted=request;else Status("Game window adjustment: "+error.Message,true);});
 }
 static void ToggleWindowsVoice(){Key(0x5B,false);Key(0x48,false);Key(0x48,true);Key(0x5B,true);}
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
 public static void ResetAlarm(AlarmState a){a.Running=false;a.Finished=false;a.Remaining=Duration(a);}
 public static bool CompleteAlarm(AlarmState a,DateTime now){if(!a.Running||Remaining(a,now)>0)return false;a.Running=false;a.Remaining=0;a.Finished=true;return true;}
 void CloseTimerNotice(int index){TimerNotice notice;if(!timerNotices.TryGetValue(index,out notice))return;timerNotices.Remove(index);notice.Close();notice.Dispose();}
 void DismissTimerNotice(int index){if(index<state.Alarms.Count)state.Alarms[index].Finished=false;CloseTimerNotice(index);dirty=true;Persist();}
 void UpdateTimerNotices(){
  for(int i=0;i<state.Alarms.Count;i++){
   if(!state.Alarms[i].Finished){CloseTimerNotice(i);continue;}
   if(!timerNotices.ContainsKey(i)){int index=i;timerNotices[i]=new TimerNotice(state.Alarms[i].Name,timerFont,()=>DismissTimerNotice(index));}
  }
  var game=GameRectangle();uint front;GetWindowThreadProcessId(GetForegroundWindow(),out front);
  bool show=bound!=null&&!game.IsEmpty&&!IsIconic(bound.Window)&&(front==bound.Id||front==Process.GetCurrentProcess().Id||(voiceSession&&WindowsVoice.Owns(front))||store.Owns(GetForegroundWindow()));
  var items=timerNotices.OrderBy(pair=>pair.Key).ToArray();int height=Math.Max(14,(int)Math.Round(28*timerScale)),gap=Math.Max(3,(int)Math.Round(6*timerScale));int stackHeight=items.Length*height+Math.Max(0,items.Length-1)*gap;
  for(int row=0;row<items.Length;row++){
   var item=items[row];if(!show){item.Value.Hide();continue;}
   item.Value.Fit(state.Alarms[item.Key].Name,game.Width-32,timerScale,timerFont);item.Value.Location=new Point(game.Left+(game.Width-item.Value.Width)/2,game.Top+(game.Height-stackHeight)/2+row*(height+gap));
   if(!item.Value.Visible)item.Value.Show();
  }
 }
 void ShowTimers(){
  if(timerWindow!=null && !timerWindow.IsDisposed){timerWindow.Show();timerWindow.Activate();return;}
  countdowns.Clear();startButtons.Clear();
  timerWindow=new TimerMenu{Text="Six reminder timers",ClientSize=new Size(524,234),Font=timerFont,ForeColor=ForeColor};
  string[] headings={"Name","Days","Hrs","Min","Sec","Time left"};int[] columns={8,112,152,192,232,276};
  for(int h=0;h<headings.Length;h++)timerWindow.Controls.Add(new Label{Text=headings[h],Bounds=new Rectangle(columns[h],7,h==0?98:h==5?100:38,18),BackColor=Color.Transparent,Cursor=Cursors.SizeAll});
  var closeTimers=new GameButton{PreserveGameFocus=true,Text="X",Bounds=new Rectangle(488,3,28,28),ForeColor=Color.White,TopColor=Color.FromArgb(205,55,67),BottomColor=Color.FromArgb(119,24,36),AccessibleName="Close timer menu"};
  closeTimers.Click+=(o,e)=>{if(timerWindow!=null)timerWindow.Close();};timerWindow.Controls.Add(closeTimers);
  for(int i=0;i<6;i++){
   var a=state.Alarms[i];int y=33+i*33;
   var name=new TextBox{Text=a.Name,Bounds=new Rectangle(8,y,99,25),AccessibleName="Timer "+(i+1)+" name"};name.TextChanged+=(o,e)=>{a.Name=name.Text;dirty=true;};timerWindow.Controls.Add(name);
   long total=(long)Duration(a);var inputs=new NumericUpDown[4];int[] max={365,23,59,59};int[] values={(int)(total/86400),(int)(total/3600%24),(int)(total/60%60),(int)(total%60)};
   for(int part=0;part<4;part++){inputs[part]=new NumericUpDown{Minimum=0,Maximum=max[part],Value=Math.Max(0,Math.Min(max[part],values[part])),Bounds=new Rectangle(112+part*40,y,36,25),AccessibleName="Timer "+(i+1)+" "+headings[part+1]};timerWindow.Controls.Add(inputs[part]);}
   foreach(var input in inputs)input.ValueChanged+=(o,e)=>{a.DurationSeconds=(long)inputs[0].Value*86400+(long)inputs[1].Value*3600+(long)inputs[2].Value*60+(long)inputs[3].Value;a.Minutes=0;if(!a.Running)ResetAlarm(a);dirty=true;};
   var counter=new Label{Bounds=new Rectangle(276,y+4,100,21),TextAlign=ContentAlignment.MiddleCenter,BackColor=Color.Transparent,Cursor=Cursors.SizeAll};countdowns.Add(counter);timerWindow.Controls.Add(counter);
   var start=new GameButton{PreserveGameFocus=true,Bounds=new Rectangle(382,y,65,26),Text=a.Running?"Pause":"Start"};startButtons.Add(start);
   start.Click+=(o,e)=>{if(a.Running){a.Remaining=Remaining(a,DateTime.UtcNow);a.Running=false;}else{if(a.Remaining<=0)ResetAlarm(a);if(a.Remaining<=0){Status("Enter a nonzero timer duration",true);return;}a.EndUtc=DateTime.UtcNow.AddSeconds(a.Remaining);a.Running=true;}dirty=true;Persist();};timerWindow.Controls.Add(start);
   var reset=new GameButton{PreserveGameFocus=true,Bounds=new Rectangle(452,y,64,26),Text="Reset"};reset.Click+=(o,e)=>{ResetAlarm(a);dirty=true;Persist();};timerWindow.Controls.Add(reset);
  }
  foreach(Control c in timerWindow.Controls)timerBounds[c]=c.Bounds;ScaleTimerWindow(uiScale);
  var game=GameRectangle();if(game.IsEmpty)game=Screen.FromControl(this).WorkingArea;timerWindow.Location=new Point(Math.Max(game.Left,game.Right-timerWindow.Width-14),Math.Max(game.Top,game.Bottom-timerWindow.Height-14));
  timerWindow.FormClosed+=(o,e)=>{Persist();countdowns.Clear();startButtons.Clear();timerBounds.Clear();timerWindow=null;};timerWindow.Show(this);timerWindow.Activate();UpdateUi();
 }
 void ScaleTimerWindow(float scale){
  if(timerWindow==null)return;scale=timerScale;
  timerWindow.SuspendLayout();
  foreach(var pair in timerBounds){var r=pair.Value;pair.Key.Bounds=new Rectangle((int)Math.Round(r.X*scale),(int)Math.Round(r.Y*scale),Math.Max(1,(int)Math.Round(r.Width*scale)),Math.Max(1,(int)Math.Round(r.Height*scale)));}
  timerWindow.Font=timerFont;timerWindow.ClientSize=new Size((int)Math.Round(524*scale),(int)Math.Round(234*scale));timerWindow.ResumeLayout();
  var game=GameRectangle();if(game.IsEmpty)game=Screen.FromControl(this).WorkingArea;
  timerWindow.Location=new Point(Math.Max(game.Left,Math.Min(timerWindow.Left,game.Right-timerWindow.Width)),Math.Max(game.Top,Math.Min(timerWindow.Top,game.Bottom-timerWindow.Height)));
 }
 void UpdateDockShortcuts(bool eligible,Rectangle game){
  if(hudView==null){foreach(var badge in dockShortcuts)badge.Hide();return;}
  try{
   uint seq=hudView.ReadUInt32(8);if((seq&1)!=0)return;
   uint mask=hudView.ReadUInt32(68),answerSeq=hudView.ReadUInt32(56);int answer=hudView.ReadInt32(52);float scale=hudView.ReadSingle(64);
   var rects=new Rectangle[8];
   for(int i=0;i<8;i++){long offset=72+i*16;float x=hudView.ReadSingle(offset),y=hudView.ReadSingle(offset+4),w=hudView.ReadSingle(offset+8),h=hudView.ReadSingle(offset+12);if(!Single.IsNaN(x)&&!Single.IsNaN(y)&&w>0&&h>0&&x>=0&&y>=0&&x+w<=1.01f&&y+h<=1.01f)rects[i]=new Rectangle(game.Left+(int)(x*game.Width),game.Top+(int)(y*game.Height),(int)(w*game.Width),(int)(h*game.Height));}
   if(seq!=hudView.ReadUInt32(8))return;
   if(!Single.IsNaN(scale)&&!Single.IsInfinity(scale)&&scale>=.25f&&scale<=3&&Math.Abs((scale=(float)Math.Round(scale,2))-uiScale)>.015){LayoutController(scale);ScaleTimerWindow(scale);}
   for(int i=0;i<8;i++){var badge=dockShortcuts[i];if(eligible&&(mask&(1u<<i))!=0&&!rects[i].IsEmpty){badge.Place(rects[i],uiScale);if(!badge.Visible)badge.Show();}else badge.Hide();}
   if(pendingShip>0&&answerSeq!=lastShipSequence){lastShipSequence=answerSeq;if(Math.Abs(answer)==pendingShip){if(answer<0)Status("Ship "+pendingShip+" could not be selected in the current game view",true);pendingShip=0;}}
  }catch{foreach(var badge in dockShortcuts)badge.Hide();}
 }
 void UpdateUi(){
  if(closing)return;
  if(dragging&&(GetAsyncKeyState(2)&0x8000)==0)EndDrag();
  var now=DateTime.UtcNow;
  voiceProbe.Pump(now);store.Pump(now);PumpDesktop(now);
  if(!preview && now>=nextBind){nextBind=now.AddSeconds(1);RefreshGames();if(bound==null && (attachedPid>0||shuttingDown)){ExitPanel();return;}if(bound!=null)RequestDesktop(bound.Window,state.Borderless);}
  if(!preview){
   UpdateRestoreHotkey();
   if(wake!=null && wake.WaitOne(0))ShowMenu();
   bool eligible=intro==null && HudVisible();
   var rect=GameRectangle();if(eligible&&hiddenByUser&&!rect.IsEmpty){summon.Place(rect,Top);if(!summon.Visible)summon.Show();}else summon.Hide();
   if(eligible&&!hiddenByUser){PlaceForGame();if(!Visible)Show();}else{if(Visible)Hide();if(timerWindow!=null&&!eligible)timerWindow.Hide();}if(eligible&&timerWindow!=null&&!timerWindow.Visible)timerWindow.Show(this);
   UpdateDockShortcuts(eligible,rect);
   if(pendingShip>0&&now>=shipDeadline){pendingShip=0;Status("The game did not confirm the ship request",true);}
  }
  clock.Text=DateTime.Now.ToString("h:mm:ss tt");
  tips.SetToolTip(clock,DateTime.Now.ToString("dddd, MMMM d, yyyy")+" • Right-drag to move • F8 restores");
  for(int i=0;i<state.Alarms.Count;i++){
   var a=state.Alarms[i];double left=Remaining(a,now);
   if(CompleteAlarm(a,now)){dirty=true;if(!preview)timerAudio.Enqueue();}
   if(i<countdowns.Count){var span=TimeSpan.FromSeconds(Math.Ceiling(left));countdowns[i].Text=(span.Days>0?span.Days+"d ":"")+span.Hours.ToString("00")+":"+span.Minutes.ToString("00")+":"+span.Seconds.ToString("00");startButtons[i].Text=a.Running?"Pause":"Start";}
  }
  if(!preview)UpdateTimerNotices();
  if(voiceReady!=null){if(voiceReady.WaitOne(0)){DisposeVoice();if(voiceClosing){voiceSession=false;}else{FinishVoiceOpen();}}else if(voiceFailed.WaitOne(0)||now>=voiceDeadline){DisposeVoice();Status(voiceClosing?"Chat close was not confirmed; use its close button and try again":"Chat input was not ready; open chat and try the mic again",true);}}
  if(saved!=null){if(saved.WaitOne(0)){DisposeSave();Status("Settings verified on disk",false);}else if(failed.WaitOne(0)){DisposeSave();Status("Settings save failed — see game log",true);}else if(now>=saveDeadline){DisposeSave();Status("Save was not confirmed",true);}}
  if(dirty)Persist();
 }
 void VerifyInteraction(){
  if(TransparencyKey!=Color.Magenta||Region==null||Region.IsVisible(0,0))throw new Exception("Controller background is not clear");
  if(!(clock is GameClock))throw new Exception("Clock must use a beveled box");
  HideMenu();if(Visible||IsDisposed)throw new Exception("Hide closed the controller");
  var message=Message.Create(Handle,0x0312,new IntPtr(814),IntPtr.Zero);WndProc(ref message);if(!Visible||hiddenByUser)throw new Exception("F8 did not restore hidden controller");
  HideMenu();summon.Show();SendMessage(summon.Handle,0x0205,IntPtr.Zero,IntPtr.Zero);Application.DoEvents();if(!Visible||hiddenByUser)throw new Exception("Right-edge handle did not restore hidden controller");summon.Hide();
  LayoutController(1.25f);if(Width!=106||clock.Height!=28)throw new Exception("Controller size baseline failed");LayoutController(1);
  using(var badge=new DockShortcut(3,()=>{})){badge.Place(new Rectangle(50,70,100,90),1);if(badge.Left!=52||badge.Top!=124)throw new Exception("Dock lower-corner anchor placement failed");}
  if(!new PanelState().Borderless)throw new Exception("Old panel settings must default to borderless");
  if(state.Alarms.Count<6)throw new Exception("Six timers missing");
  var a=new AlarmState{Name="Days test",DurationSeconds=2*86400+3*3600+4*60+5};ResetAlarm(a);if(a.Remaining!=183845)throw new Exception("Day/hour/minute/second duration failed");
  File.AppendAllText("desktop-progress.txt","before desktop verify\r\n");GameDesktop.Verify();File.AppendAllText("desktop-progress.txt","after desktop verify\r\n");
  if(CommunityStore.ProfileMatches("browser.exe --user-data-dir=\"C:\\other\"", "C:\\owned"))throw new Exception("Store close could target an unrelated browser");
 }
 public void WriteUiEvidence(string directory){
  Directory.CreateDirectory(directory);File.AppendAllText(Path.Combine(directory,"panel-progress.txt"),"show panel\r\n");Show();Application.DoEvents();
  if(Width>=Height)throw new Exception("Panel must be vertical: "+Size+" client="+ClientSize+" scale="+uiScale);
  if(!Controls.OfType<Button>().Any(b=>b.Text=="Diagnostics"))throw new Exception("Diagnostics button missing");
  foreach(Control c in Controls){if(!c.Visible)continue;if(!ClientRectangle.Contains(c.Bounds))throw new Exception("Control clipped: "+c.Text);}
  var buttons=Controls.OfType<Button>().ToArray();
  for(int i=0;i<buttons.Length;i++)for(int j=i+1;j<buttons.Length;j++)if(buttons[i].Bounds.IntersectsWith(buttons[j].Bounds))throw new Exception("Buttons overlap");
  using(var bmp=new Bitmap(Width,Height)){DrawToBitmap(bmp,new Rectangle(0,0,Width,Height));bmp.MakeTransparent(TransparencyKey);bmp.Save(Path.Combine(directory,"community-panel.png"));}
  File.AppendAllText(Path.Combine(directory,"panel-progress.txt"),"verify interactions\r\n");VerifyInteraction();File.AppendAllText(Path.Combine(directory,"panel-progress.txt"),"show timers\r\n");ShowTimers();Application.DoEvents();
  using(var bmp=new Bitmap(timerWindow.Width,timerWindow.Height)){timerWindow.DrawToBitmap(bmp,new Rectangle(0,0,timerWindow.Width,timerWindow.Height));bmp.Save(Path.Combine(directory,"community-timers.png"));}
 }
 [STAThread] public static int Main(string[] args){
  DesktopDpi.Initialize();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  try{
   if(args.Length==2 && args[0]=="--verify-ui"){
    using(var panel=new CommunityPanel(0,false,true)){panel.WriteUiEvidence(args[1]);panel.closing=true;}
    var a=new AlarmState{Minutes=2,Remaining=42,Running=true,EndUtc=DateTime.UtcNow.AddSeconds(30)};
    if(Remaining(a,DateTime.UtcNow)<28 || Remaining(a,DateTime.UtcNow)>31)throw new Exception("Timer countdown regression");
    ResetAlarm(a);if(a.Running || a.Remaining!=120)throw new Exception("Timer reset regression");
    a.EndUtc=DateTime.UtcNow.AddSeconds(-1);a.Running=true;if(!CompleteAlarm(a,DateTime.UtcNow)||!a.Finished||CompleteAlarm(a,DateTime.UtcNow))throw new Exception("Timer expiry state regression");
    using(var alert=new TimerNotice("Build reminder",SystemFonts.MessageBoxFont,()=>{})){
     alert.Show();Application.DoEvents();using(var bmp=new Bitmap(alert.Width,alert.Height)){alert.DrawToBitmap(bmp,new Rectangle(0,0,alert.Width,alert.Height));bmp.Save(Path.Combine(args[1],"timer-alert.png"));}
     alert.Close();
    }
    ResetAlarm(a);if(a.Finished)throw new Exception("Timer reset kept finished state");
    string test=Path.Combine(args[1],"state-test.json");var state=new PanelState();state.Alarms.Add(a);PanelStorage.Save(test,state);if(PanelStorage.Load(test).Alarms[0].Remaining!=120)throw new Exception("State persistence regression");
    var splashWatch=Stopwatch.StartNew();
    using(var intro=new MorbidIntro(AppDomain.CurrentDomain.BaseDirectory,3000,()=>splashWatch.ElapsedMilliseconds>=4000)){
     intro.Shown+=(o,e)=>{Application.DoEvents();using(var bmp=new Bitmap(intro.Width,intro.Height)){intro.DrawToBitmap(bmp,new Rectangle(0,0,intro.Width,intro.Height));bmp.Save(Path.Combine(args[1],"morbid-intro.png"));}};
     intro.ShowDialog();if(intro.ElapsedSeconds<4.7 || intro.ElapsedSeconds>10)throw new Exception("Splash closed before the game-window readiness gate settled");
    }
    File.WriteAllText(Path.Combine(args[1],"panel-validation.txt"),"PASS: clear rounded panel and boxed clock; hide/F8/edge restoration; dock badge positioning and shared scale; borderless monitor and style restoration; right-drag; six named days/hours/minutes/seconds timers; countdown/reset and state persistence; full-monitor black splash waits for stable game-window readiness.\r\n");return 0;
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
  }catch(Exception ex){if(args.Length>0){File.WriteAllText("panel-error.txt",ex.ToString());}else MessageBox.Show(ex.Message,"Morbid Kitty Controller");return 1;}
 }
}

