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

public class GameButton:Button {
 public GameButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;TabStop=false;UseVisualStyleBackColor=false;ForeColor=Color.FromArgb(244,252,255);}
 protected override void OnPaint(PaintEventArgs e){
  var r=ClientRectangle;if(r.Width<4||r.Height<4)return;
  e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
  Color top=Enabled?Color.FromArgb(26,155,211):Color.FromArgb(67,90,105),bottom=Enabled?Color.FromArgb(7,82,127):Color.FromArgb(35,51,62);
  using(var brush=new LinearGradientBrush(r,top,bottom,LinearGradientMode.Vertical))e.Graphics.FillRectangle(brush,r);
  using(var edge=new Pen(Color.FromArgb(6,51,79),2))e.Graphics.DrawRectangle(edge,1,1,r.Width-2,r.Height-2);
  using(var edge=new Pen(Color.FromArgb(158,217,236)))e.Graphics.DrawRectangle(edge,3,3,r.Width-7,r.Height-7);
  TextRenderer.DrawText(e.Graphics,Text,Font,r,ForeColor,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPadding);
 }
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
 public MorbidIntro(string assetDirectory,int milliseconds){
  Text="Morbid Kitty";FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;BackColor=Color.Black;StartPosition=FormStartPosition.CenterScreen;ClientSize=new Size(430,430);
  string path=Path.Combine(assetDirectory,"MorbidKitty-Splash.png");if(File.Exists(path))art=Image.FromFile(path);
  BackgroundImage=art;BackgroundImageLayout=ImageLayout.Zoom;GamePaths.IconFor(this,assetDirectory);
  closeTimer.Interval=milliseconds;closeTimer.Tick+=(s,e)=>Close();Shown+=(s,e)=>{elapsed.Start();closeTimer.Start();};
 }
 protected override void Dispose(bool disposing){if(disposing){closeTimer.Dispose();if(art!=null)art.Dispose();}base.Dispose(disposing);}
}
