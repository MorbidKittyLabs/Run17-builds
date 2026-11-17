using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

public static class DpiServiceVerification {
 [DllImport("user32.dll")] static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")] static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra);
 [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
 static void Pump(int ms){var watch=Stopwatch.StartNew();while(watch.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(5);}}
 static void Fonts(string output){
  using(var panel=new CommunityPanel(0,false,true)){
   panel.Show();Pump(30);
   foreach(float scale in new[]{1f,1.5f,2f}){
    typeof(CommunityPanel).GetMethod("LayoutController",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(panel,new object[]{scale});
    foreach(float dpi in new[]{96f,144f,192f})using(var image=new Bitmap(panel.Width,panel.Height)){
     image.SetResolution(dpi,dpi);using(var g=Graphics.FromImage(image))foreach(Control c in panel.Controls){
      Require(c.Font.Unit==GraphicsUnit.Pixel,"Controller font still scales as points: "+c.Text);
      string text=c is Label?"12:59:59 PM":c.Text;
      var measured=g.MeasureString(text,c.Font,Int32.MaxValue,StringFormat.GenericTypographic);
      Require(measured.Height+2<=c.Height,"Font clips vertically at DPI "+dpi+": "+text);
      Require(measured.Width+4<=c.Width,"Font clips horizontally at DPI "+dpi+": "+text+" measured="+measured.Width+" available="+c.Width);
     }
    }
   }
   typeof(CommunityPanel).GetMethod("ShowTimers",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(panel,null);Pump(20);
   var timer=(Form)typeof(CommunityPanel).GetField("timerWindow",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(panel);
   Require(timer.Controls.Cast<Control>().All(c=>c.Font.Unit==GraphicsUnit.Pixel),"Timer controls retain point fonts");
   typeof(CommunityPanel).GetField("closing",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(panel,true);panel.Close();
  }
 }
 static void BlockedProviders(){
  using(var form=new Form())using(var button=new Button{Text="Still clickable"})using(var heartbeat=new System.Windows.Forms.Timer{Interval=15})
  using(var release=new ManualResetEvent(false))using(var entered=new ManualResetEvent(false))using(var work=new UiWork<int>()){
   form.Controls.Add(button);form.Show();int ticks=0,clicks=0,callbacks=0;button.Click+=(o,e)=>clicks++;heartbeat.Tick+=(o,e)=>ticks++;heartbeat.Start();
   bool timeout=false;
   Require(work.Start(()=>{Require(Thread.CurrentThread.GetApartmentState()==ApartmentState.MTA,"Provider is not isolated on MTA");entered.Set();release.WaitOne();return 42;},TimeSpan.FromMilliseconds(120),(value,error)=>{callbacks++;timeout=error is TimeoutException;}),"Cannot start provider");
   var watch=Stopwatch.StartNew();while(watch.ElapsedMilliseconds<350){work.Pump(DateTime.UtcNow);button.PerformClick();Pump(15);}
   Require(entered.WaitOne(0)&&ticks>5&&clicks>5,"Blocked provider froze the UI heartbeat");
   Require(timeout&&callbacks==1&&work.Busy,"Timeout did not retain one bounded worker");
   Require(!work.Start(()=>1,TimeSpan.FromSeconds(1),(v,e)=>{}),"A second hung worker was created");
   release.Set();Pump(80);work.Pump(DateTime.UtcNow);Require(!work.Busy&&callbacks==1,"Late completion changed timed-out UI state");form.Close();
  }
  using(var release=new ManualResetEvent(false))using(var entered=new ManualResetEvent(false)){
   int launches=0;var store=new CommunityStore(profile=>{Require(Thread.CurrentThread.GetApartmentState()==ApartmentState.MTA,"Store query is on UI thread");entered.Set();release.WaitOne();return new CommunityStore.Owner[0];},(profile,rect)=>{Interlocked.Increment(ref launches);return null;});
   try{
    var watch=Stopwatch.StartNew();store.Open(new Rectangle(0,0,1000,700));while(watch.ElapsedMilliseconds<250){store.Pump(DateTime.UtcNow);Pump(10);}
    Require(entered.WaitOne(0)&&launches==1,"Store launch waited on process discovery");
    watch.Restart();store.Close();Require(watch.ElapsedMilliseconds<150,"Store close waited on process discovery");
    store.Open(new Rectangle(0,0,1000,700));watch.Restart();while(watch.ElapsedMilliseconds<200){store.Pump(DateTime.UtcNow);Pump(10);}
    Require(launches==2,"Blocked discovery prevented another user-requested Store launch");
   }finally{release.Set();Pump(40);store.Dispose();}
  }
 }
 static void VoiceRequestBeforeProbe(){
  int pid=Process.GetCurrentProcess().Id;string prefix="Local\\STFC.Community.Voice.";
  using(var request=new EventWaitHandle(false,EventResetMode.AutoReset,prefix+"Request."+pid))
  using(var ready=new EventWaitHandle(false,EventResetMode.ManualReset,prefix+"Ready."+pid))
  using(var failed=new EventWaitHandle(false,EventResetMode.ManualReset,prefix+"Failed."+pid))
  using(var entered=new ManualResetEvent(false))using(var release=new ManualResetEvent(false))
  using(var game=new Form{Text="Chat bridge fixture"})using(var panel=new CommunityPanel(0,false,true)){
   game.Show();panel.Owner=game;panel.Show();game.Activate();Pump(40);
   var click=game.PointToScreen(new Point(game.ClientSize.Width-20,game.ClientSize.Height-20));SetCursorPos(click.X,click.Y);mouse_event(2,0,0,0,UIntPtr.Zero);mouse_event(4,0,0,0,UIntPtr.Zero);Pump(80);
   Require(GetForegroundWindow()==game.Handle,"Chat fixture could not acquire a user-input foreground window");
   var type=typeof(CommunityPanel);var flags=BindingFlags.Instance|BindingFlags.NonPublic;
   type.GetField("bound",flags).SetValue(panel,new GameChoice{Id=pid,Window=game.Handle,Directory=Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName)});
   int probes=0;type.GetField("inspectVoiceOpen",flags).SetValue(panel,(Func<bool>)(()=>{Interlocked.Increment(ref probes);entered.Set();release.WaitOne();return true;}));
   try{
    type.GetMethod("VoiceTyping",flags).Invoke(panel,null);
    Require(request.WaitOne(0)&&probes==0,"Opening game chat waited for Windows microphone inspection");
    ready.Set();Pump(180);Require(entered.WaitOne(0),"Voice inspection did not run after native chat readiness");
    type.GetMethod("ShowTimers",flags).Invoke(panel,null);Pump(40);
    Require(((Form)type.GetField("timerWindow",flags).GetValue(panel)).Visible,"A blocked microphone provider prevented timer use");
   }finally{type.GetField("closing",flags).SetValue(panel,true);panel.Close();release.Set();Pump(40);game.Close();}
  }
 }
 [STAThread] public static int Main(string[] args){
  DesktopDpi.Initialize();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   try{string output=args.Length==0?"ui-evidence":args[0];Directory.CreateDirectory(output);Console.WriteLine("Checking pixel typography");Fonts(output);Console.WriteLine("Checking blocked service providers");BlockedProviders();Console.WriteLine("Checking immediate native chat request");VoiceRequestBeforeProbe();Console.WriteLine("Service checks complete");File.WriteAllText(Path.Combine(output,"dpi-service-validation.txt"),"PASS: pixel-font fit metrics at 96/144/192 DPI and three UI scales; MTA provider isolation; live UI heartbeat during a blocked call; bounded timeout and stale completion handling; Store open/close while process discovery is blocked; immediate native chat request and timer use while microphone inspection is blocked. Font metrics do not substitute for live STFC or a real mixed-DPI monitor test.\r\n");return 0;}
  catch(Exception e){Console.Error.WriteLine(e);File.WriteAllText("dpi-service-error.txt",e.ToString());return 1;}
 }
}
