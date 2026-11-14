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
[assembly: AssemblyTitle("STFC Community Panel")]
[assembly: AssemblyDescription("Single-account clock, timers and manual shortcuts")]
[assembly: AssemblyVersion("18.1.0.0")]

[DataContract] public sealed class AlarmState {
 [DataMember] public string Name="Timer";
 [DataMember] public int Minutes=5;
 [DataMember] public double Remaining=300;
 [DataMember] public DateTime EndUtc;
 [DataMember] public bool Running;
}
[DataContract] public sealed class PanelState {
 [DataMember] public int X=40,Y=40;
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
 ComboBox account;
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
 public CommunityPanel() {
  statePath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"STFC-Community","Panel.json");
  state=PanelStorage.Load(statePath);
  if(state==null)state=new PanelState();if(state.Alarms==null)state.Alarms=new List<AlarmState>();
  while(state.Alarms.Count<4)state.Alarms.Add(new AlarmState{Name="Timer "+(state.Alarms.Count+1)});
  Text="STFC Community Panel";Name="CommunityPanel";ClientSize=new Size(272,145);
  FormBorderStyle=FormBorderStyle.FixedToolWindow;MaximizeBox=false;TopMost=true;
  Font=new Font("Tahoma",9);BackColor=Color.FromArgb(28,26,35);ForeColor=Color.WhiteSmoke;
  StartPosition=FormStartPosition.Manual;Location=new Point(state.X,state.Y);
  var bounds=Screen.FromPoint(Location).WorkingArea;
  Location=new Point(Math.Max(bounds.Left,Math.Min(Location.X,bounds.Right-Width)),Math.Max(bounds.Top,Math.Min(Location.Y,bounds.Bottom-Height)));
  clock=new Label{Bounds=new Rectangle(6,5,260,23),TextAlign=ContentAlignment.MiddleCenter,Font=new Font("Tahoma",12,FontStyle.Bold),Cursor=Cursors.SizeAll};
  clock.MouseDown+=(s,e)=>{if(e.Button==MouseButtons.Left){ReleaseCapture();SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero);}};
  clock.Text=DateTime.Now.ToString("h:mm:ss tt");
  Controls.Add(clock);tips.SetToolTip(clock,"Local time. Drag here to move the panel.");
  account=new ComboBox{Bounds=new Rectangle(6,31,230,24),DropDownStyle=ComboBoxStyle.DropDownList};Controls.Add(account);
  var refresh=Make("↻",238,31,28,24,"Refresh the STFC window list",RefreshGames);
  refresh.AccessibleName="Refresh STFC windows";
  for(int i=1;i<=8;i++){int ship=i;var b=Make(i.ToString(),6+(i-1)*33,61,31,25,"Select ship "+i,()=>SendShip(ship));b.AccessibleName="Select ship "+i;}
  Make("Store",6,90,57,27,"Open the official STFC Store",()=>Process.Start(new ProcessStartInfo("https://home.startrekfleetcommand.com/store"){UseShellExecute=true}));
  var mic=Make("\uE720",65,90,35,27,"Windows voice typing (Win+H); select a text field in the game first",VoiceTyping);mic.Font=new Font("Segoe MDL2 Assets",11);mic.AccessibleName="Windows voice typing";
  save=Make("\uE74E",102,90,35,27,"Save this account's live settings (F12)",SaveSettings);save.Font=new Font("Segoe MDL2 Assets",11);save.AccessibleName="Save account settings";
  Make("Diag",139,90,57,27,"Collect this account's logs to a Desktop ZIP",CollectDiagnostics);
  Make("Timers",198,90,68,27,"Four independent reminder timers",ShowTimers);
  notice=new Label{Bounds=new Rectangle(6,121,260,19),TextAlign=ContentAlignment.MiddleCenter,Text="F6: single attack  •  F7: recall ships",ForeColor=Color.Silver};Controls.Add(notice);
  Move+=(s,e)=>dirty=true;
  FormClosing+=(s,e)=>{state.X=Left;state.Y=Top;Persist();DisposeSave();if(timerWindow!=null)timerWindow.Close();};
  ui.Interval=200;ui.Tick+=(s,e)=>UpdateUi();ui.Start();RefreshGames();
 }
 Button Make(string text,int x,int y,int width,int height,string tip,Action action) {
  var b=new Button{Text=text,Bounds=new Rectangle(x,y,width,height),FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(54,43,66),ForeColor=Color.WhiteSmoke,TabStop=false};
  b.FlatAppearance.BorderColor=Color.FromArgb(116,88,132);b.Click+=(s,e)=>{try{action();}catch(Exception ex){Status(ex.Message,true);}};
  Controls.Add(b);tips.SetToolTip(b,tip);return b;
 }
 void Status(string text,bool error){notice.Text=text;notice.ForeColor=error?Color.LightSalmon:Color.LightGreen;tips.SetToolTip(notice,text);}
 void RefreshGames(){
  int previous=Selected()==null?0:Selected().Id;account.Items.Clear();
  foreach(var p in Process.GetProcessesByName("Prime"))try{
   if(p.MainWindowHandle!=IntPtr.Zero && p.MainModule!=null)account.Items.Add(new GameChoice{Id=p.Id,Window=p.MainWindowHandle,Title=p.MainWindowTitle,Directory=Path.GetDirectoryName(p.MainModule.FileName)});
  }catch{}finally{p.Dispose();}
  for(int i=0;i<account.Items.Count;i++)if(((GameChoice)account.Items[i]).Id==previous)account.SelectedIndex=i;
  if(account.Items.Count==1)account.SelectedIndex=0;
  if(account.SelectedIndex<0)Status(account.Items.Count==0?"Open STFC, then press ↻":"Choose one STFC window",false);
 }
 GameChoice Selected(){return account.SelectedItem as GameChoice;}
 GameChoice RequireGame(){
  var choice=Selected();if(choice==null)throw new InvalidOperationException("Choose an STFC window first.");
  using(var p=Process.GetProcessById(choice.Id)){
   if(p.HasExited || p.MainWindowHandle!=choice.Window || !String.Equals(Path.GetDirectoryName(p.MainModule.FileName),choice.Directory,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("STFC window changed. Press ↻.");
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
  timerWindow=new Form{Text="STFC reminder timers",ClientSize=new Size(478,155),FormBorderStyle=FormBorderStyle.FixedToolWindow,TopMost=true,Font=Font,BackColor=BackColor,ForeColor=ForeColor};
  for(int i=0;i<4;i++){
   var a=state.Alarms[i];int y=9+i*35;
   var name=new TextBox{Text=a.Name,Bounds=new Rectangle(8,y,105,25)};name.TextChanged+=(s,e)=>{a.Name=name.Text;dirty=true;};timerWindow.Controls.Add(name);
   var minutes=new NumericUpDown{Minimum=1,Maximum=10080,Value=Math.Max(1,Math.Min(10080,a.Minutes)),Bounds=new Rectangle(118,y,62,25)};
   minutes.ValueChanged+=(s,e)=>{a.Minutes=(int)minutes.Value;if(!a.Running)ResetAlarm(a);dirty=true;};timerWindow.Controls.Add(minutes);
   var unit=new Label{Text="min",Bounds=new Rectangle(183,y+4,28,20)};timerWindow.Controls.Add(unit);
   var counter=new Label{Bounds=new Rectangle(214,y+4,83,21),TextAlign=ContentAlignment.MiddleCenter};countdowns.Add(counter);timerWindow.Controls.Add(counter);
   var start=new Button{Bounds=new Rectangle(300,y,80,26),Text=a.Running?"Pause":"Start"};startButtons.Add(start);
   start.Click+=(s,e)=>{if(a.Running){a.Remaining=Remaining(a,DateTime.UtcNow);a.Running=false;}else{if(a.Remaining<=0)ResetAlarm(a);a.EndUtc=DateTime.UtcNow.AddSeconds(a.Remaining);a.Running=true;}dirty=true;Persist();};timerWindow.Controls.Add(start);
   var reset=new Button{Bounds=new Rectangle(385,y,83,26),Text="Reset"};reset.Click+=(s,e)=>{ResetAlarm(a);dirty=true;Persist();};timerWindow.Controls.Add(reset);
  }
  timerWindow.FormClosed+=(s,e)=>{Persist();countdowns.Clear();startButtons.Clear();timerWindow=null;};timerWindow.Show(this);UpdateUi();
 }
 void UpdateUi(){
  var now=DateTime.UtcNow;clock.Text=DateTime.Now.ToString("h:mm:ss tt");
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
  using(var bmp=new Bitmap(Width,Height)){DrawToBitmap(bmp,new Rectangle(0,0,Width,Height));bmp.Save(Path.Combine(directory,"community-panel.png"));}
  ShowTimers();Application.DoEvents();
  using(var bmp=new Bitmap(timerWindow.Width,timerWindow.Height)){timerWindow.DrawToBitmap(bmp,new Rectangle(0,0,timerWindow.Width,timerWindow.Height));bmp.Save(Path.Combine(directory,"community-timers.png"));}
 }
 [STAThread] public static int Main(string[] args){
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  try{
   if(args.Length==2 && args[0]=="--verify-ui"){
    using(var panel=new CommunityPanel()){panel.WriteUiEvidence(args[1]);}
    var a=new AlarmState{Minutes=2,Remaining=42,Running=true,EndUtc=DateTime.UtcNow.AddSeconds(30)};
    if(Remaining(a,DateTime.UtcNow)<28 || Remaining(a,DateTime.UtcNow)>31)throw new Exception("Timer countdown regression");
    ResetAlarm(a);if(a.Running || a.Remaining!=120)throw new Exception("Timer reset regression");
    string test=Path.Combine(args[1],"state-test.json");var state=new PanelState();state.Alarms.Add(a);PanelStorage.Save(test,state);if(PanelStorage.Load(test).Alarms[0].Remaining!=120)throw new Exception("State persistence regression");
    File.WriteAllText(Path.Combine(args[1],"panel-validation.txt"),"PASS: panel renders; four timers render; timer countdown/reset and state persistence pass. Gameplay keys are sent only by explicit clicks.\r\n");return 0;
   }
   using(var panel=new CommunityPanel())Application.Run(panel);return 0;
  }catch(Exception ex){if(args.Length>0){File.WriteAllText("panel-error.txt",ex.ToString());}else MessageBox.Show(ex.Message,"STFC Community Panel");return 1;}
 }
}
