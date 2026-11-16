using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

public static class MouseInputVerification {
 [StructLayout(LayoutKind.Sequential)] struct Mouse {public int X,Y;public uint Data,Flags,Time;public UIntPtr Extra;}
 [StructLayout(LayoutKind.Explicit,Size=32)] struct Union {[FieldOffset(0)]public Mouse Mouse;}
 [StructLayout(LayoutKind.Sequential)] struct Input {public uint Type;public Union Value;}
 [DllImport("user32.dll",SetLastError=true)] static extern uint SendInput(uint count,Input[] input,int size);
 [DllImport("user32.dll")] static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Point p);
 [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr h,uint flags);
 static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
 static void Pump(int milliseconds=140){var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<milliseconds){Application.DoEvents();Thread.Sleep(5);}}
 static void MouseEvent(uint flags){var input=new Input{Value=new Union{Mouse=new Mouse{Flags=flags}}};Require(SendInput(1,new[]{input},Marshal.SizeOf(typeof(Input)))==1,"Windows did not accept mouse input: "+Marshal.GetLastWin32Error());Pump();}
 static void Move(Point p){SetCursorPos(p.X,p.Y);Pump();}
 static void Click(Control c){Move(c.PointToScreen(new Point(c.Width/2,c.Height/2)));MouseEvent(2);MouseEvent(4);}
 static object Call(CommunityPanel panel,string method,params object[] args){return typeof(CommunityPanel).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(panel,args);}
 static Form Timers(CommunityPanel panel){return (Form)typeof(CommunityPanel).GetField("timerWindow",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(panel);}
 [STAThread] public static int Main(string[] args){
  DesktopDpi.Initialize();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  try{
   var directory=args.Length>0?args[0]:"input-evidence";Directory.CreateDirectory(directory);
   using(var game=new Form{Text="Native game input fixture",StartPosition=FormStartPosition.Manual,Bounds=new Rectangle(50,50,900,650)})
   using(var native=new Button{Text="Native dock",Bounds=new Rectangle(30,30,150,45)})
   using(var panel=new CommunityPanel(0,false,true)){
    int nativeClicks=0;native.Click+=(o,e)=>nativeClicks++;game.Controls.Add(native);game.Show();panel.Location=new Point(550,220);panel.Show();game.Activate();SetForegroundWindow(game.Handle);Pump();
    Require(GetForegroundWindow()==game.Handle,"Fixture game did not become foreground");
    foreach(float scale in new[]{1f,1.25f,1.5f}){
     Call(panel,"LayoutController",scale);panel.Location=new Point(550,220);Call(panel,"ShowMenu");Pump();
     foreach(string name in new[]{"Diagnostics","Store","Close Store only","Save settings","Toggle chat and Windows voice typing","Close game and controller"}){
      var button=panel.Controls.OfType<Button>().Single(b=>b.Text==name||b.AccessibleName==name);int clicked=0;EventHandler onClick=(o,e)=>clicked++;button.Click+=onClick;
      var center=button.PointToScreen(new Point(button.Width/2,button.Height/2));Require(GetAncestor(WindowFromPoint(center),2)==panel.Handle,"Button hit-test is blocked: "+name);
      Click(button);button.Click-=onClick;Require(clicked==1,"Physical click did not reach "+name+" at scale "+scale);
      Require(GetForegroundWindow()==game.Handle,"Controller stole game focus: "+name);
     }
     Click(panel.Controls.OfType<Button>().Single(b=>b.Text=="Timers"));Require(Timers(panel)!=null&&Timers(panel).Visible,"Physical Timers click did not open editor");
     var timer=Timers(panel);Click(timer.Controls.OfType<Button>().Single(b=>b.AccessibleName=="Close timer menu"));Require(Timers(panel)==null,"Physical timer X did not close editor");game.Activate();SetForegroundWindow(game.Handle);Pump();
     Click(native);Require(nativeClicks>0,"Native game click was intercepted");int before=nativeClicks;Click(native);Require(nativeClicks==before+1,"Native game click was intercepted");
     Click(panel.Controls.OfType<Button>().Single(b=>b.AccessibleName=="Hide controller"));Require(!panel.Visible,"Physical controller X did not hide panel");Call(panel,"ShowMenu");Pump();
     var origin=panel.Location;var clock=panel.Controls.OfType<Label>().Single();var point=clock.PointToScreen(new Point(clock.Width/2,clock.Height/2));Move(point);MouseEvent(8);Move(new Point(point.X+30,point.Y+20));MouseEvent(16);Pump();Require(panel.Location==new Point(origin.X+30,origin.Y+20),"Physical right-drag failed");Require(!panel.Capture,"Right-drag retained mouse capture");
     int shipClicks=0;using(var badge=new DockShortcut(6,()=>shipClicks++)){badge.Place(new Rectangle(200,400,80,85),scale);badge.Show();game.Activate();SetForegroundWindow(game.Handle);Pump();Click(badge.Controls[0]);Require(shipClicks==1,"Physical dock-number click failed");badge.Close();}
     panel.Location=new Point(550,220);
    }
    typeof(CommunityPanel).GetField("closing",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(panel,true);panel.Close();game.Close();
   }
   File.WriteAllText(Path.Combine(directory,"mouse-input-validation.txt"),"PASS: Windows SendInput clicks on every controller button, timer editor close, controller hide, underlying game controls, dock-number shortcut, focus preservation and right-drag/capture release at 100%, 125% and 150% game UI scales. This is a Windows fixture, not a live STFC result.\r\n");return 0;
  }catch(Exception e){File.WriteAllText("mouse-input-error.txt",e.ToString());Console.Error.WriteLine(e);return 1;}
 }
}
