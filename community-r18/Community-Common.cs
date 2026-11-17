using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

// Use one physical-pixel coordinate space for Unity anchors and Win32 windows.
public static class DesktopDpi {
 [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr context);
 [DllImport("shcore.dll")] static extern int SetProcessDpiAwareness(int awareness);
 [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
 public static void Initialize(){try{if(SetProcessDpiAwarenessContext(new IntPtr(-4)))return;}catch(EntryPointNotFoundException){}try{if(SetProcessDpiAwareness(2)==0)return;}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}SetProcessDPIAware();}
}
public static class PixelFonts {
 public static Font Create(string family,float designPoints,FontStyle style=FontStyle.Regular){return new Font(family,designPoints*4f/3f,style,GraphicsUnit.Pixel);}
}
public static class BeveledFace {
 public static GraphicsPath Shape(Rectangle bounds,int radius){return Shape(bounds,radius,true,true);}
 public static GraphicsPath Shape(Rectangle bounds,int radius,bool roundLeft,bool roundRight){var p=new GraphicsPath();int d=Math.Min(radius*2,Math.Min(bounds.Width,bounds.Height));if(d<=0)return p;if(roundLeft)p.AddArc(bounds.Left,bounds.Top,d,d,180,90);else p.AddLine(bounds.Left,bounds.Top,bounds.Left,bounds.Top);if(roundRight){p.AddArc(bounds.Right-d,bounds.Top,d,d,270,90);p.AddArc(bounds.Right-d,bounds.Bottom-d,d,d,0,90);}else p.AddLine(bounds.Right,bounds.Top,bounds.Right,bounds.Bottom);if(roundLeft)p.AddArc(bounds.Left,bounds.Bottom-d,d,d,90,90);else p.AddLine(bounds.Left,bounds.Bottom,bounds.Left,bounds.Top);p.CloseFigure();return p;}
 public static void Paint(Graphics g,Rectangle area,string text,Font font,Color foreground,Color top,Color bottom,bool joinLeft=false,bool joinRight=false){if(area.Width<5||area.Height<5)return;g.SmoothingMode=SmoothingMode.AntiAlias;var outer=new Rectangle(1,1,area.Width-3,area.Height-3);using(var path=Shape(outer,5,!joinLeft,!joinRight))using(var brush=new LinearGradientBrush(area,top,bottom,LinearGradientMode.Vertical)){g.FillPath(brush,path);using(var pen=new Pen(Color.FromArgb(3,39,61),2))g.DrawPath(pen,path);}var inner=new Rectangle(3,3,area.Width-7,area.Height-7);using(var path=Shape(inner,3,!joinLeft,!joinRight))using(var pen=new Pen(Color.FromArgb(158,217,236)))g.DrawPath(pen,path);TextRenderer.DrawText(g,text,font,area,foreground,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPadding);}
}
public class GameButton:Button {
 public bool PreserveGameFocus {set{SetStyle(ControlStyles.Selectable,!value);TabStop=!value;}}
 public Color TopColor=Color.FromArgb(26,155,211),BottomColor=Color.FromArgb(7,82,127);
 bool joinLeft,joinRight;
 public bool JoinLeft{get{return joinLeft;}set{joinLeft=value;UpdateShape();Invalidate();}}
 public bool JoinRight{get{return joinRight;}set{joinRight=value;UpdateShape();Invalidate();}}
 void UpdateShape(){if(Width>1&&Height>1)using(var path=BeveledFace.Shape(ClientRectangle,5,!joinLeft,!joinRight)){var old=Region;Region=new Region(path);if(old!=null)old.Dispose();}}
 public GameButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;TabStop=false;UseVisualStyleBackColor=false;ForeColor=Color.FromArgb(244,252,255);}
 protected override void OnResize(EventArgs e){base.OnResize(e);UpdateShape();}
 protected override void OnPaint(PaintEventArgs e){BeveledFace.Paint(e.Graphics,ClientRectangle,Text,Font,Enabled?ForeColor:Color.Silver,Enabled?TopColor:Color.FromArgb(67,90,105),Enabled?BottomColor:Color.FromArgb(35,51,62),joinLeft,joinRight);}
}
public sealed class GameClock:Label {
 public Color TopColor=Color.FromArgb(20,130,185),BottomColor=Color.FromArgb(5,69,112);
 public GameClock(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
 protected override void OnResize(EventArgs e){base.OnResize(e);if(Width>1&&Height>1)using(var path=BeveledFace.Shape(ClientRectangle,5)){var old=Region;Region=new Region(path);if(old!=null)old.Dispose();}}
 protected override void OnPaint(PaintEventArgs e){BeveledFace.Paint(e.Graphics,ClientRectangle,Text,Font,ForeColor,TopColor,BottomColor);}
}
public static class GamePaths {
 public static bool IsGame(string path){return !String.IsNullOrWhiteSpace(path) && File.Exists(Path.Combine(path,"Prime.exe")) && File.Exists(Path.Combine(path,"GameAssembly.dll"));}
 public static string InstalledGame(){string p=AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);if(IsGame(p))return p;var parent=Directory.GetParent(p);return parent!=null && IsGame(parent.FullName)?parent.FullName:null;}
 public static string Find(){
  var paths=new List<string>();paths.Add(InstalledGame());
  foreach(var process in Process.GetProcessesByName("Prime"))try{paths.Add(Path.GetDirectoryName(process.MainModule.FileName));}catch{}finally{process.Dispose();}
  paths.Add(@"C:\Games\Star Trek Fleet Command\Star Trek Fleet Command\default\game");
  foreach(var basePath in new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)})if(!String.IsNullOrEmpty(basePath)){
   paths.Add(Path.Combine(basePath,"Star Trek Fleet Command","Star Trek Fleet Command","default","game"));
   paths.Add(Path.Combine(basePath,"Star Trek Fleet Command","default","game"));
  }
  foreach(var hive in new[]{Registry.CurrentUser,Registry.LocalMachine})foreach(var keyPath in new[]{@"Software\Microsoft\Windows\CurrentVersion\Uninstall",@"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"})try{
   using(var key=hive.OpenSubKey(keyPath))if(key!=null)foreach(var name in key.GetSubKeyNames())using(var app=key.OpenSubKey(name)){
    if(app==null)continue;var title=Convert.ToString(app.GetValue("DisplayName"));
    if(title.IndexOf("Star Trek Fleet Command",StringComparison.OrdinalIgnoreCase)<0)continue;
    string location=Convert.ToString(app.GetValue("InstallLocation"));if(String.IsNullOrEmpty(location))continue;
    paths.Add(location);paths.Add(Path.Combine(location,"default","game"));paths.Add(Path.Combine(location,"Star Trek Fleet Command","default","game"));
   }
  }catch{}
  foreach(var p in paths)if(IsGame(p))return Path.GetFullPath(p);return null;
 }
 public static string Hash(string text){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();}
 public static string Quote(string value){if(value.IndexOf('"')>=0)throw new InvalidOperationException("A path contains an invalid quote.");return "\""+value+"\"";}
 public static bool Running(string game){foreach(var p in Process.GetProcessesByName("Prime"))try{if(String.Equals(Path.GetDirectoryName(p.MainModule.FileName),game,StringComparison.OrdinalIgnoreCase))return true;}catch{}finally{p.Dispose();}return false;}
 public static void IconFor(Form form,string directory){try{form.Icon=new Icon(Path.Combine(directory,"MorbidKitty.ico"));}catch{}}
}
public sealed class MorbidIntro:Form {
 readonly Image art;readonly System.Windows.Forms.Timer closeTimer=new System.Windows.Forms.Timer();
 readonly Stopwatch elapsed=new Stopwatch();public double ElapsedSeconds {get{return elapsed.Elapsed.TotalSeconds;}}
 readonly Func<bool> windowReady;readonly Func<Rectangle> monitorBounds;readonly int minimumMilliseconds;long readySince=-1;
 public MorbidIntro(string assetDirectory,int milliseconds,Func<bool> ready=null,Func<Rectangle> monitor=null){
  minimumMilliseconds=milliseconds;windowReady=ready;monitorBounds=monitor;
  AutoScaleMode=AutoScaleMode.None;Text="Morbid Kitty";FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;BackColor=Color.Black;StartPosition=FormStartPosition.Manual;Bounds=monitor==null?Screen.FromPoint(Cursor.Position).Bounds:monitor();
  string path=Path.Combine(assetDirectory,"MorbidKitty-Splash.png");if(File.Exists(path))art=Image.FromFile(path);
  BackgroundImage=art;BackgroundImageLayout=ImageLayout.Zoom;GamePaths.IconFor(this,assetDirectory);
  closeTimer.Interval=100;closeTimer.Tick+=(s,e)=>{
   if(monitorBounds!=null){var bounds=monitorBounds();if(!bounds.IsEmpty&&bounds!=Bounds){Bounds=bounds;readySince=-1;}}
   bool readyNow=windowReady==null||windowReady();
   if(!readyNow)readySince=-1;else if(readySince<0)readySince=elapsed.ElapsedMilliseconds;
   if(elapsed.ElapsedMilliseconds>=minimumMilliseconds&&readySince>=0&&elapsed.ElapsedMilliseconds-readySince>=800)Close();
  };Shown+=(s,e)=>{elapsed.Start();closeTimer.Start();};
 }
 protected override void Dispose(bool disposing){if(disposing){closeTimer.Dispose();if(art!=null)art.Dispose();}base.Dispose(disposing);}
}

public static class PanelWake {
 public static string EventName(int pid){return "Local\\STFC.Community.Show."+pid;}
 public static void Signal(int pid){try{using(var e=System.Threading.EventWaitHandle.OpenExisting(EventName(pid)))e.Set();}catch(System.Threading.WaitHandleCannotBeOpenedException){} }
}

