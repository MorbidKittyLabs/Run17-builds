using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public sealed class GameDesktop:IDisposable {
 [ComImport,Guid("56FDF344-FD6D-11d0-958A-006097C9A090")] class TaskbarList {}
 [ComImport,Guid("602D4995-B13A-429b-A66E-1935E44F4317"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
 interface ITaskbarList2 {
  void HrInit();void AddTab(IntPtr h);void DeleteTab(IntPtr h);void ActivateTab(IntPtr h);void SetActiveAlt(IntPtr h);
  void MarkFullscreenWindow(IntPtr h,[MarshalAs(UnmanagedType.Bool)]bool fullscreen);
 }
 static void MarkFullscreen(IntPtr h,bool full){object obj=null;try{obj=new TaskbarList();var taskbar=(ITaskbarList2)obj;taskbar.HrInit();taskbar.MarkFullscreenWindow(h,full);}catch(COMException){}finally{if(obj!=null)Marshal.FinalReleaseComObject(obj);}}

 [StructLayout(LayoutKind.Sequential)] struct Rect {public int Left,Top,Right,Bottom;}
 [StructLayout(LayoutKind.Sequential)] struct Point {public int X,Y;}
 [StructLayout(LayoutKind.Sequential)] struct Placement {public int Length,Flags,Show;public Point Min,Max;public Rect Normal;}
 [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h,int index);
 [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h,int index,int value);
 [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int hgt,uint flags);
 [DllImport("user32.dll")] static extern bool GetWindowPlacement(IntPtr h,ref Placement placement);
 [DllImport("user32.dll")] static extern bool SetWindowPlacement(IntPtr h,ref Placement placement);
 [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
 [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
 [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")] static extern bool IsHungAppWindow(IntPtr h);
 [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h,out Rect r);
 const int Decorations=0x00C00000|0x00040000|0x00080000;
 IntPtr window;int style;Placement original;Rectangle target;bool marked;
 public void Apply(IntPtr handle) {
  if(handle==IntPtr.Zero || !IsWindow(handle) || IsIconic(handle))return;
  if(window!=handle){Restore();window=handle;style=GetWindowLong(window,-16);original=new Placement{Length=Marshal.SizeOf(typeof(Placement))};if(!GetWindowPlacement(window,ref original)){window=IntPtr.Zero;throw new InvalidOperationException("Could not record the original game window.");}}
  target=Screen.FromHandle(window).Bounds;
  int now=GetWindowLong(window,-16),wanted=(now&~Decorations)|unchecked((int)0x80000000);
  if(now!=wanted)SetWindowLong(window,-16,wanted);
  if(!marked){MarkFullscreen(window,true);marked=true;}
  Rect r;if(!GetWindowRect(window,out r))return;
  if(now!=wanted || r.Left!=target.Left || r.Top!=target.Top || r.Right-r.Left!=target.Width || r.Bottom-r.Top!=target.Height)
   SetWindowPos(window,IntPtr.Zero,target.Left,target.Top,target.Width,target.Height,0x0020|0x0010|0x0004);
 }
 public void Restore(){if(window!=IntPtr.Zero && IsWindow(window)){MarkFullscreen(window,false);SetWindowLong(window,-16,style);SetWindowPlacement(window,ref original);SetWindowPos(window,IntPtr.Zero,0,0,0,0,0x0020|0x0010|0x0004|0x0001|0x0002);}window=IntPtr.Zero;marked=false;}
 public void Dispose(){Restore();}
 public static bool IsEstablished(IntPtr handle,bool borderless){
  if(handle==IntPtr.Zero||!IsWindow(handle)||!IsWindowVisible(handle)||IsIconic(handle)||IsHungAppWindow(handle))return false;
  Rect r;if(!GetWindowRect(handle,out r)||r.Right-r.Left<100||r.Bottom-r.Top<100)return false;
  if(!borderless)return true;
  var target=Screen.FromHandle(handle).Bounds;return (GetWindowLong(handle,-16)&Decorations)==0&&r.Left==target.Left&&r.Top==target.Top&&r.Right==target.Right&&r.Bottom==target.Bottom;
 }
 public static void Verify(){using(var f=new Form{Text="Borderless regression fixture",Bounds=new Rectangle(35,45,480,300)}){f.Show();Application.DoEvents();int before=GetWindowLong(f.Handle,-16);using(var d=new GameDesktop()){d.Apply(f.Handle);Application.DoEvents();if((GetWindowLong(f.Handle,-16)&Decorations)!=0)throw new Exception("Game title/frame not removed");Rect r;GetWindowRect(f.Handle,out r);var b=Screen.FromHandle(f.Handle).Bounds;if(r.Left!=b.Left||r.Top!=b.Top||r.Right-r.Left!=b.Width||r.Bottom-r.Top!=b.Height)throw new Exception("Game does not fill the monitor including taskbar area");d.Restore();if(GetWindowLong(f.Handle,-16)!=before)throw new Exception("Game style not restored");}f.Close();}}
}

public sealed class SummonHandle:Form {
 public event EventHandler Summon;
 public SummonHandle(){AutoScaleMode=AutoScaleMode.None;MinimumSize=new Size(1,1);Text="Morbid Kitty menu handle";AccessibleName="Click the green arrow to show the controller";FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;ClientSize=new Size(12,42);BackColor=Color.Magenta;TransparencyKey=Color.Magenta;ForeColor=Color.FromArgb(157,255,93);Cursor=Cursors.Hand;Font=new Font("Tahoma",14,FontStyle.Bold);var tip=new ToolTip();tip.SetToolTip(this,"Click: show controls • F8 also restores them");FormClosed+=(s,e)=>tip.Dispose();}
 protected override bool ShowWithoutActivation{get{return true;}}
 protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x08000000;return p;}}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);TextRenderer.DrawText(e.Graphics,"‹",Font,ClientRectangle,ForeColor,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPadding);}
 protected override void WndProc(ref Message m){if(m.Msg==0x0021){m.Result=new IntPtr(3);return;}if(m.Msg==0x0202||m.Msg==0x0205){if(Summon!=null)Summon(this,EventArgs.Empty);return;}base.WndProc(ref m);}
 public void Place(Rectangle game,int y){Location=new System.Drawing.Point(game.Right-Width,Math.Max(game.Top,Math.Min(y,game.Bottom-Height)));}
}

public sealed class CommunityStore {
 [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback,IntPtr value);
 delegate bool EnumProc(IntPtr h,IntPtr value);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h,uint message,IntPtr w,IntPtr l);
 [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
 readonly string profile=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"STFC-Community","StoreBrowser");
 HashSet<uint> owners=new HashSet<uint>();DateTime nextRefresh;bool opened;
 public static bool ProfileMatches(string command,string profile){return !String.IsNullOrEmpty(command) && command.IndexOf("--user-data-dir=\""+profile+"\"",StringComparison.OrdinalIgnoreCase)>=0;}
 void Refresh(){if(!opened||DateTime.UtcNow<nextRefresh)return;nextRefresh=DateTime.UtcNow.AddSeconds(1);var found=new HashSet<uint>();try{using(var query=new ManagementObjectSearcher("SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name='msedge.exe' OR Name='chrome.exe'"))using(var results=query.Get())foreach(ManagementObject p in results)using(p)if(ProfileMatches(Convert.ToString(p["CommandLine"]),profile))found.Add(Convert.ToUInt32(p["ProcessId"]));owners=found;}catch{}}
 List<IntPtr> Windows(){Refresh();var windows=new List<IntPtr>();EnumWindows((h,l)=>{uint pid;GetWindowThreadProcessId(h,out pid);if(owners.Contains(pid)&&IsWindowVisible(h))windows.Add(h);return true;},IntPtr.Zero);return windows;}
 public bool Owns(IntPtr window){Refresh();uint pid;GetWindowThreadProcessId(window,out pid);return owners.Contains(pid);}
 public void Open(Rectangle game){opened=true;var windows=Windows();if(windows.Count>0){SetForegroundWindow(windows[0]);return;}string browser=null;foreach(string folder in new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)})foreach(string rel in new[]{@"Microsoft\Edge\Application\msedge.exe",@"Google\Chrome\Application\chrome.exe"}){string path=Path.Combine(folder,rel);if(browser==null&&File.Exists(path))browser=path;}if(browser==null)throw new InvalidOperationException("The separate Store window needs Microsoft Edge or Google Chrome.");int w=Math.Max(480,game.Width*3/5),h=Math.Max(400,game.Height*3/4);string args="--app=https://home.startrekfleetcommand.com/store --user-data-dir="+GamePaths.Quote(profile)+" --no-first-run --no-default-browser-check --window-size="+w+","+h+" --window-position="+(game.Right-w-20)+","+(game.Top+30);using(var p=Process.Start(new ProcessStartInfo(browser,args){UseShellExecute=true})){if(p!=null)owners.Add((uint)p.Id);}nextRefresh=DateTime.MinValue;}
 public void Close(){nextRefresh=DateTime.MinValue;foreach(var h in Windows())PostMessage(h,0x0010,IntPtr.Zero,IntPtr.Zero);}
}
