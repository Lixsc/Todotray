using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TodoTraySetup
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupForm());
        }
    }

    public class SetupForm : Form
    {
        private TextBox _txtInstallPath;
        private CheckBox _chkDesktop;
        private CheckBox _chkStartMenu;
        private CheckBox _chkStartup;
        private CheckBox _chkLaunch;
        private Button _btnInstall;
        private Button _btnUninstall;
        private Label _lblStatus;

        private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppRegistryName = "TodoTray";

        public SetupForm()
        {
            InitializeSetupUI();
        }

        private void InitializeSetupUI()
        {
            this.Text = "TodoTray Setup";
            this.ClientSize = new Size(460, 390);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(248, 250, 252);
            this.Font = new Font("Segoe UI", 9.5f);
            this.Icon = CreateAppIcon();

            // Header Banner
            var pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.White,
                Padding = new Padding(20, 14, 20, 14)
            };

            var pic = new PictureBox
            {
                Size = new Size(40, 40),
                Location = new Point(20, 20),
                Image = CreateHeaderBitmap()
            };

            var lblTitle = new Label
            {
                Text = "Install TodoTray",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42),
                Location = new Point(70, 16),
                AutoSize = true
            };

            var lblSub = new Label
            {
                Text = "Fast & lightweight system tray to-do manager",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(100, 116, 139),
                Location = new Point(72, 44),
                AutoSize = true
            };

            pnlHeader.Controls.Add(pic);
            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSub);

            // Install Directory
            string defaultDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "TodoTray"
            );

            var lblPath = new Label
            {
                Text = "Install Location:",
                Location = new Point(24, 100),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };

            _txtInstallPath = new TextBox
            {
                Text = defaultDir,
                Location = new Point(24, 124),
                Width = 320,
                Font = new Font("Segoe UI", 9.5f)
            };

            var btnBrowse = new Button
            {
                Text = "Browse...",
                Location = new Point(352, 122),
                Width = 84,
                Height = 28,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(241, 245, 249),
                ForeColor = Color.FromArgb(51, 65, 85),
                Cursor = Cursors.Hand
            };
            btnBrowse.FlatAppearance.BorderSize = 0;
            btnBrowse.Click += (s, e) =>
            {
                using (var fbd = new FolderBrowserDialog())
                {
                    fbd.SelectedPath = _txtInstallPath.Text;
                    if (fbd.ShowDialog() == DialogResult.OK)
                    {
                        _txtInstallPath.Text = fbd.SelectedPath;
                    }
                }
            };

            // Options Checkboxes
            _chkDesktop = new CheckBox
            {
                Text = "Create Desktop shortcut",
                Checked = true,
                Location = new Point(24, 168),
                AutoSize = true
            };

            _chkStartMenu = new CheckBox
            {
                Text = "Create Start Menu shortcut",
                Checked = true,
                Location = new Point(24, 196),
                AutoSize = true
            };

            _chkStartup = new CheckBox
            {
                Text = "Launch automatically on Windows startup",
                Checked = true,
                Location = new Point(24, 224),
                AutoSize = true
            };

            _chkLaunch = new CheckBox
            {
                Text = "Launch TodoTray immediately after install",
                Checked = true,
                Location = new Point(24, 252),
                AutoSize = true
            };

            // Status label
            _lblStatus = new Label
            {
                Text = "Ready to install.",
                Location = new Point(24, 290),
                AutoSize = true,
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = new Font("Segoe UI", 9f)
            };

            // Bottom Buttons
            var pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = Color.White,
                Padding = new Padding(20, 14, 20, 14)
            };

            _btnUninstall = new Button
            {
                Text = "Uninstall",
                Font = new Font("Segoe UI", 9f),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(88, 32),
                Location = new Point(20, 14),
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(156, 163, 175),
                Cursor = Cursors.Hand
            };
            _btnUninstall.FlatAppearance.BorderSize = 0;
            _btnUninstall.Click += (s, e) => DoUninstall();

            _btnInstall = new Button
            {
                Text = "Install TodoTray",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(130, 32),
                Location = new Point(pnlBottom.Width - 146, 14),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Color.FromArgb(37, 99, 235),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            _btnInstall.FlatAppearance.BorderSize = 0;
            _btnInstall.Click += (s, e) => DoInstall();

            pnlBottom.Controls.Add(_btnUninstall);
            pnlBottom.Controls.Add(_btnInstall);

            this.Controls.Add(pnlHeader);
            this.Controls.Add(lblPath);
            this.Controls.Add(_txtInstallPath);
            this.Controls.Add(btnBrowse);
            this.Controls.Add(_chkDesktop);
            this.Controls.Add(_chkStartMenu);
            this.Controls.Add(_chkStartup);
            this.Controls.Add(_chkLaunch);
            this.Controls.Add(_lblStatus);
            this.Controls.Add(pnlBottom);
        }

        private void DoInstall()
        {
            string targetDir = _txtInstallPath.Text.Trim();
            if (string.IsNullOrEmpty(targetDir))
            {
                MessageBox.Show("Please specify a valid install directory.", "Invalid Path", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                _btnInstall.Enabled = false;
                _lblStatus.Text = "Installing files...";
                _lblStatus.ForeColor = Color.FromArgb(37, 99, 235);
                Application.DoEvents();

                // Stop any running instances of TodoTray
                foreach (var p in Process.GetProcessesByName("TodoTray"))
                {
                    try { p.Kill(); p.WaitForExit(1500); } catch { }
                }

                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                string exePath = Path.Combine(targetDir, "TodoTray.exe");
                string configPath = Path.Combine(targetDir, "TodoTray.exe.config");

                // Extract embedded resource or copy from current directory
                ExtractResourceOrCopy("TodoTray.exe", exePath);
                ExtractResourceOrCopy("TodoTray.exe.config", configPath);

                // Create Desktop Shortcut
                if (_chkDesktop.Checked)
                {
                    string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    string scPath = Path.Combine(desktop, "TodoTray.lnk");
                    CreateWindowsShortcut(scPath, exePath, targetDir, "TodoTray - System Tray To-Do App");
                }

                // Create Start Menu Shortcut
                if (_chkStartMenu.Checked)
                {
                    string startMenu = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                        "Programs"
                    );
                    if (!Directory.Exists(startMenu)) Directory.CreateDirectory(startMenu);
                    string scPath = Path.Combine(startMenu, "TodoTray.lnk");
                    CreateWindowsShortcut(scPath, exePath, targetDir, "TodoTray - System Tray To-Do App");
                }

                // Set Windows Startup
                if (_chkStartup.Checked)
                {
                    using (var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true))
                    {
                        if (key != null)
                        {
                            key.SetValue(AppRegistryName, "\"" + exePath + "\"");
                        }
                    }
                }

                _lblStatus.Text = "Installation completed successfully! 🎉";
                _lblStatus.ForeColor = Color.FromArgb(22, 163, 74);

                if (_chkLaunch.Checked && File.Exists(exePath))
                {
                    Process.Start(exePath);
                }

                MessageBox.Show(
                    "TodoTray has been successfully installed!\n\nShortcuts have been placed on your Desktop and Start Menu.\nSummon TodoTray anytime with: Alt + Shift + T",
                    "Installation Complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );

                this.Close();
            }
            catch (Exception ex)
            {
                _lblStatus.Text = "Installation failed.";
                _lblStatus.ForeColor = Color.Red;
                MessageBox.Show("Installation failed:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _btnInstall.Enabled = true;
            }
        }

        private void DoUninstall()
        {
            var res = MessageBox.Show(
                "Are you sure you want to uninstall TodoTray and remove shortcuts?",
                "Confirm Uninstall",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );
            if (res != DialogResult.Yes) return;

            try
            {
                foreach (var p in Process.GetProcessesByName("TodoTray"))
                {
                    try { p.Kill(); p.WaitForExit(1500); } catch { }
                }

                // Remove Desktop shortcut
                string desktopShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "TodoTray.lnk");
                if (File.Exists(desktopShortcut)) File.Delete(desktopShortcut);

                // Remove Start Menu shortcut
                string startMenuShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "TodoTray.lnk");
                if (File.Exists(startMenuShortcut)) File.Delete(startMenuShortcut);

                // Remove Startup registry
                using (var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true))
                {
                    if (key != null && key.GetValue(AppRegistryName) != null)
                    {
                        key.DeleteValue(AppRegistryName, false);
                    }
                }

                // Remove installed folder
                string targetDir = _txtInstallPath.Text.Trim();
                if (Directory.Exists(targetDir))
                {
                    Directory.Delete(targetDir, true);
                }

                MessageBox.Show("TodoTray has been successfully uninstalled.", "Uninstall Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Uninstall error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void ExtractResourceOrCopy(string filename, string targetPath)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream(filename))
            {
                if (stream != null)
                {
                    using (var fs = new FileStream(targetPath, FileMode.Create, FileAccess.Write))
                    {
                        stream.CopyTo(fs);
                    }
                    return;
                }
            }

            // Fallback: check adjacent file
            string localFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filename);
            if (File.Exists(localFile))
            {
                File.Copy(localFile, targetPath, true);
            }
        }

        private static void CreateWindowsShortcut(string shortcutPath, string targetPath, string workingDir, string description)
        {
            try
            {
                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return;

                dynamic shell = Activator.CreateInstance(shellType);
                dynamic sc = shell.CreateShortcut(shortcutPath);
                sc.TargetPath = targetPath;
                sc.WorkingDirectory = workingDir;
                sc.Description = description;
                sc.IconLocation = targetPath + ",0";
                sc.Save();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Shortcut creation error: " + ex.Message);
            }
        }

        private static Bitmap CreateHeaderBitmap()
        {
            var bmp = new Bitmap(40, 40);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(Color.FromArgb(37, 99, 235)))
                {
                    g.FillEllipse(brush, 2, 2, 36, 36);
                }
                using (var pen = new Pen(Color.White, 3.5f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawLines(pen, new[]
                    {
                        new Point(11, 20),
                        new Point(17, 26),
                        new Point(29, 13)
                    });
                }
            }
            return bmp;
        }

        private static Icon CreateAppIcon()
        {
            using (var bmp = CreateHeaderBitmap())
            {
                return Icon.FromHandle(bmp.GetHicon());
            }
        }
    }
}
