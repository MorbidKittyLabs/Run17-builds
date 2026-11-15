using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
[assembly:AssemblyTitle("Morbid Kitty STFC Launcher")]
[assembly:AssemblyDescription("Single-account game starter")]
[assembly:AssemblyVersion("18.4.0.0")]
public static class MorbidLauncher {
 [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h,int command);
 [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
 [STAThread] public static int Main(string[] args){
  Application.EnableVisualStyles();
  try{
   string root=GamePaths.InstalledGame();if(root==null)throw new InvalidOperationException("Game files were not found. Run the Morbid Kitty installer and choose Prime.exe.");
   foreach(var p in Process.GetProcessesByName("Prime"))try{if(String.Equals(Path.GetDirectoryName(p.MainModule.FileName),root,StringComparison.OrdinalIgnoreCase)){if(p.MainWindowHandle!=IntPtr.Zero){if(IsIconic(p.MainWindowHandle))ShowWindow(p.MainWindowHandle,9);SetForegroundWindow(p.MainWindowHandle);}string panel=Path.Combine(root,"MorbidKitty","Community-Panel.exe");if(File.Exists(panel))Process.Start(new ProcessStartInfo(panel,"--attach "+p.Id+" --show"){WorkingDirectory=Path.GetDirectoryName(panel),UseShellExecute=true});return 0;}}catch{}finally{p.Dispose();}
   Process.Start(new ProcessStartInfo(Path.Combine(root,"Prime.exe")){WorkingDirectory=root,UseShellExecute=true});return 0;
  }catch(Exception ex){MessageBox.Show(ex.Message,"Morbid Kitty",MessageBoxButtons.OK,MessageBoxIcon.Information);return 1;}
 }
}
