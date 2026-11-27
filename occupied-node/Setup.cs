using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;

static class Setup {
  static bool selfTest;
  internal const string ResourceName = "OccupiedNode.version.dll";
  internal static string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MorbidKittyLabs", "OccupiedNode-Test");
  static string Hash(byte[] data) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant(); }
  internal static byte[] Payload() {
    using (var input = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)) {
      if (input == null) throw new InvalidOperationException("The compiled test DLL is missing from this installer.");
      using (var output = new MemoryStream()) { input.CopyTo(output); return output.ToArray(); }
    }
  }
  static void CheckNotRunning() {
    foreach (var process in Process.GetProcessesByName("prime")) {
      using (process) {
        string path;
        try { path = process.MainModule.FileName; }
        catch { throw new InvalidOperationException("Close STFC before changing the test installation."); }
        if (String.Equals(Path.GetDirectoryName(path), Root, StringComparison.OrdinalIgnoreCase))
          throw new InvalidOperationException("Close the occupied-node test game before installing or rolling back.");
      }
    }
  }
  static void CopyTree(string source, string dest) {
    var directory = new DirectoryInfo(source);
    if ((directory.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked game directories are not supported.");
    Directory.CreateDirectory(dest);
    foreach (var file in directory.GetFiles()) {
      if ((file.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked game files are not supported.");
      file.CopyTo(Path.Combine(dest, file.Name), true);
    }
    foreach (var child in directory.GetDirectories()) CopyTree(child.FullName, Path.Combine(dest, child.Name));
  }
  static void Install(string source) {
    source = Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar);
    if (String.Equals(source, Root, StringComparison.OrdinalIgnoreCase) || Root.StartsWith(source + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
      throw new InvalidOperationException("Choose the original STFC game folder as the source.");
    foreach (var name in new [] {"prime.exe", "GameAssembly.dll", "UnityPlayer.dll"})
      if (!File.Exists(Path.Combine(source, name))) throw new IOException("The selected folder is missing " + name + ". Choose the folder containing prime.exe.");
    if (!Directory.Exists(Path.Combine(source, "prime_Data"))) throw new IOException("The selected folder is missing prime_Data.");
    var payload = Payload();
    if (payload.Length < 1024 || payload[0] != 'M' || payload[1] != 'Z') throw new IOException("The embedded DLL is invalid.");
    CheckNotRunning();
    Directory.CreateDirectory(Root);
    // Copy only native game files. Existing proxies, controllers, plugins,
    // scripts and settings in the source folder never enter this test copy.
    foreach (var name in new [] {"prime.exe", "GameAssembly.dll", "UnityPlayer.dll", "baselib.dll", "UnityCrashHandler64.exe"})
      if (File.Exists(Path.Combine(source, name))) File.Copy(Path.Combine(source, name), Path.Combine(Root, name), true);
    foreach (var name in new [] {"prime_Data", "MonoBleedingEdge"})
      if (Directory.Exists(Path.Combine(source, name))) CopyTree(Path.Combine(source, name), Path.Combine(Root, name));
    // Back up only an earlier isolated test DLL; originals are never touched.
    var dll = Path.Combine(Root, "version.dll");
    var backup = Path.Combine(Root, "version.dll.previous");
    var pending = Path.Combine(Root, "version.dll.pending");
    File.WriteAllBytes(pending, payload);
    if (Hash(File.ReadAllBytes(pending)) != Hash(payload)) throw new IOException("DLL copy verification failed.");
    if (File.Exists(dll)) File.Replace(pending, dll, backup, true);
    else File.Move(pending, dll);
    File.WriteAllText(Path.Combine(Root, "OccupiedNode-Test.txt"), "Isolated occupied mining-node inspection test\r\nSource game: " + source + "\r\nDLL SHA256: " + Hash(payload) + "\r\n");
    var desktop = selfTest ? Root : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    var installed = Path.Combine(Root, "OccupiedNode-Test-Setup.exe");
    if (!String.Equals(Assembly.GetExecutingAssembly().Location, installed, StringComparison.OrdinalIgnoreCase))
      File.Copy(Assembly.GetExecutingAssembly().Location, installed, true);
    File.WriteAllText(Path.Combine(desktop, "STFC-OccupiedNode-Test-Setup.url"), "[InternetShortcut]\r\nURL=" + new Uri(installed).AbsoluteUri + "\r\nWorkingDirectory=" + Root + "\r\n");
  }
  static void Rollback() {
    CheckNotRunning();
    var dll = Path.Combine(Root, "version.dll");
    var backup = Path.Combine(Root, "version.dll.previous");
    if (File.Exists(backup)) {
      if (File.Exists(dll)) File.Replace(backup, dll, Path.Combine(Root, "version.dll.removed"), true);
      else File.Move(backup, dll);
    } else if (File.Exists(dll)) File.Move(dll, Path.Combine(Root, "version.dll.disabled-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff")));
    else throw new IOException("There is no active test DLL to roll back.");
  }
  static int SelfTest() {
    selfTest = true;
    var temp = Path.Combine(Path.GetTempPath(), "OccupiedNode-Setup-Test-" + Guid.NewGuid().ToString("N"));
    var source = Path.Combine(temp, "source");
    Root = Path.Combine(temp, "test");
    try {
      Directory.CreateDirectory(Path.Combine(source, "prime_Data"));
      foreach (var name in new [] {"prime.exe", "GameAssembly.dll", "UnityPlayer.dll", "version.dll", "DSE.exe", "community_patch_settings.toml"})
        File.WriteAllText(Path.Combine(source, name), "original-" + name);
      File.WriteAllText(Path.Combine(source, "prime_Data", "fixture.dat"), "game data");
      Install(source);
      if (File.Exists(Path.Combine(Root, "DSE.exe")) || File.Exists(Path.Combine(Root, "community_patch_settings.toml"))) throw new Exception("Isolation failed");
      if (File.ReadAllText(Path.Combine(source, "version.dll")) != "original-version.dll") throw new Exception("Source changed");
      if (Hash(File.ReadAllBytes(Path.Combine(Root, "version.dll"))) != Hash(Payload())) throw new Exception("Installed payload differs");
      if (File.ReadAllText(Path.Combine(Root, "prime_Data", "fixture.dat")) != "game data") throw new Exception("Native data missing");
      Rollback();
      if (File.Exists(Path.Combine(Root, "version.dll"))) throw new Exception("Clean rollback failed");
      Install(source);
      Install(source);
      if (!File.Exists(Path.Combine(Root, "version.dll.previous"))) throw new Exception("Backup missing");
      Rollback();
      if (Hash(File.ReadAllBytes(Path.Combine(Root, "version.dll"))) != Hash(Payload())) throw new Exception("Backup rollback differs");
      bool refused = false;
      try { Install(Root); } catch (InvalidOperationException) { refused = true; }
      if (!refused) throw new Exception("Self-copy should be refused");
      return 0;
    } catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    finally { if (Directory.Exists(temp)) Directory.Delete(temp, true); }
  }
  [STAThread] static int Main(string[] args) {
    if (args.Contains("--self-test")) return SelfTest();
    if (args.Contains("--verify-payload")) { var payload = Payload(); Console.WriteLine("Embedded test DLL: " + payload.Length + " bytes; SHA256 " + Hash(payload)); return payload.Length > 1024 && payload[0] == 'M' && payload[1] == 'Z' ? 0 : 1; }
    Application.EnableVisualStyles();
    var form = new Form { Text = "Occupied Mining Node - Isolated Test", Width = 690, Height = 345, StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false };
    var intro = new Label { Left = 20, Top = 18, Width = 640, Height = 65, Text = "Creates a separate STFC copy for one-account testing.\r\nChoose your existing game folder (contains prime.exe).\r\nOnly native game files are copied; this requires space for another game copy." };
    var path = new TextBox { Left = 20, Top = 90, Width = 525 };
    var browse = new Button { Left = 555, Top = 88, Width = 100, Text = "Browse..." };
    browse.Click += delegate { using (var picker = new FolderBrowserDialog { Description = "Choose the STFC folder containing prime.exe" }) { if (picker.ShowDialog() == DialogResult.OK) path.Text = picker.SelectedPath; } };
    var install = new Button { Left = 20, Top = 135, Width = 200, Height = 35, Text = "Install isolated test" };
    var launch = new Button { Left = 235, Top = 135, Width = 200, Height = 35, Text = "Launch test game" };
    var rollback = new Button { Left = 450, Top = 135, Width = 205, Height = 35, Text = "Roll back test DLL" };
    var status = new Label { Left = 20, Top = 190, Width = 635, Height = 100, Text = "Test folder: " + Root + "\r\nActionView (default: V): node > loot > strength > original node.\r\nYour original game files and mod settings stay untouched." };
    install.Click += async delegate {
      install.Enabled = launch.Enabled = rollback.Enabled = browse.Enabled = false;
      string source = path.Text;
      status.Text = "Copying native game files and installing the test DLL. This can take several minutes...";
      try { await Task.Run(() => Install(source)); status.Text = "Installed. Use Launch test game. Sign in to the one account you want to test.\r\nA separate desktop shortcut has been created.\r\nRepeat ActionView three times on an occupied node."; }
      catch (Exception error) { status.Text = "Install failed: " + error.Message; MessageBox.Show(error.Message, "Test installation", MessageBoxButtons.OK, MessageBoxIcon.Error); }
      finally { install.Enabled = launch.Enabled = rollback.Enabled = browse.Enabled = true; }
    };
    launch.Click += delegate {
      try { if (!File.Exists(Path.Combine(Root, "version.dll"))) throw new IOException("Install the test DLL first."); Process.Start(new ProcessStartInfo(Path.Combine(Root, "prime.exe")) { WorkingDirectory = Root, UseShellExecute = true }); }
      catch (Exception error) { MessageBox.Show(error.Message, "Launch test"); }
    };
    rollback.Click += delegate { try { Rollback(); status.Text = "The isolated test DLL was rolled back. Your original game remains untouched."; } catch (Exception error) { MessageBox.Show(error.Message, "Rollback"); } };
    form.Controls.AddRange(new Control[] {intro, path, browse, install, launch, rollback, status});
    Application.Run(form);
    return 0;
  }
}
