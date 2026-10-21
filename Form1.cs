using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace IDK
{
    public partial class Form1 : Form
    {
        private enum FilterMode
        {
            All,
            Active,
            Completed
        }

        // Win32 API for Cue Banners and Global Hotkeys
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, [MarshalAs(UnmanagedType.LPWStr)] string lParam);
        private const int EM_SETCUEBANNER = 0x1501;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int HOTKEY_ID = 9001;
        private const int MOD_ALT = 0x0001;
        private const int MOD_SHIFT = 0x0004;
        private const int WM_HOTKEY = 0x0312;

        private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppRegistryName = "TodoTray";

        private readonly TodoRepository _repo = new TodoRepository();
        private List<TodoItem> _todos = new List<TodoItem>();
        private FilterMode _currentFilter = FilterMode.All;
        private TodoPriority _nextPriority = TodoPriority.Normal;

        private NotifyIcon _notifyIcon;
        private ContextMenuStrip _trayMenu;
        private Icon _appIcon;
        private bool _isExiting = false;
        private bool _hasShownBalloon = false;
        private bool _enableHourlyNudge = true;
        private string _lastNotifiedTodoId = null;

        // Timers
        private Timer _reminderTimer;
        private Timer _hourlyNudgeTimer;

        // UI Controls
        private Panel _headerPanel;
        private Label _lblTitle;
        private Label _lblStats;
        private Button _btnPin;
        private Label _lblHotkeyHint;
        private Panel _progressContainer;
        private Panel _progressBar;
        private Label _lblProgress;

        private Panel _inputContainer;
        private TextBox _txtNewTodo;
        private Button _btnPriorityToggle;
        private Button _btnAdd;

        private Panel _filterPanel;
        private Button _btnFilterAll;
        private Button _btnFilterActive;
        private Button _btnFilterCompleted;
        private TextBox _txtSearch;
        private Button _btnClearCompleted;

        private ListBox _lstTodos;
        private Label _lblEmpty;
        private Panel _bottomPanel;
        private LinkLabel _lnkStorage;
        private Button _btnMinimize;
        private ToolStripMenuItem _menuStartup;
        private ToolStripMenuItem _menuHourlyNudge;

        public Form1()
        {
            InitializeComponent();

            this.KeyPreview = true;
            this.KeyDown += Form1_KeyDown;

            _appIcon = CreateAppIcon();
            this.Icon = _appIcon;

            BuildCustomUI();
            SetupTray();
            SetupTimers();

            _todos = _repo.Load();
            RefreshTodoList();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // Global Hotkey: Alt + Shift + T summons or hides TodoTray
            RegisterHotKey(this.Handle, HOTKEY_ID, MOD_ALT | MOD_SHIFT, (int)Keys.T);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            UnregisterHotKey(this.Handle, HOTKEY_ID);
            base.OnHandleDestroyed(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
            {
                ToggleWindowVisibility();
            }
            base.WndProc(ref m);
        }

        private static Icon CreateAppIcon()
        {
            using (var bmp = new Bitmap(32, 32))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(Color.FromArgb(37, 99, 235)))
                {
                    g.FillEllipse(brush, 1, 1, 30, 30);
                }
                using (var pen = new Pen(Color.White, 3.2f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawLines(pen, new[]
                    {
                        new Point(9, 16),
                        new Point(14, 21),
                        new Point(23, 11)
                    });
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        private void SetupTimers()
        {
            // Checks for pending task reminders every 15 seconds
            _reminderTimer = new Timer();
            _reminderTimer.Interval = 15000;
            _reminderTimer.Tick += (s, e) => CheckReminders();
            _reminderTimer.Start();

            // Periodic gentle nudge every 1 hour (if enabled and tasks remain)
            _hourlyNudgeTimer = new Timer();
            _hourlyNudgeTimer.Interval = 3600000; // 1 hr
            _hourlyNudgeTimer.Tick += (s, e) =>
            {
                if (_enableHourlyNudge && (!this.Visible || this.WindowState == FormWindowState.Minimized))
                {
                    int active = _todos.Count(t => !t.IsCompleted);
                    if (active > 0)
                    {
                        _notifyIcon.ShowBalloonTip(
                            4000,
                            "TodoTray Hourly Nudge",
                            string.Format("You have {0} active task{1} waiting. Keep up the momentum!", active, active == 1 ? "" : "s"),
                            ToolTipIcon.Info
                        );
                    }
                }
            };
            _hourlyNudgeTimer.Start();
        }

        private void CheckReminders()
        {
            DateTime now = DateTime.Now;
            foreach (var item in _todos)
            {
                if (!item.IsCompleted && item.HasReminder && !item.ReminderFired && item.ReminderTime.HasValue)
                {
                    if (item.ReminderTime.Value <= now)
                    {
                        item.ReminderFired = true;
                        _lastNotifiedTodoId = item.Id;
                        _repo.Save(_todos);

                        _notifyIcon.ShowBalloonTip(
                            6000,
                            "TodoTray Reminder ⏰",
                            string.Format("Time for: \"{0}\"", item.Title),
                            ToolTipIcon.Warning
                        );

                        RefreshTodoList();
                        break;
                    }
                }
            }
        }

        private void BuildCustomUI()
        {
            this.Text = "TodoTray";
            this.BackColor = Color.FromArgb(248, 250, 252);
            this.ForeColor = Color.FromArgb(30, 41, 59);
            this.ClientSize = new Size(480, 640);
            this.MinimumSize = new Size(420, 500);
            this.Font = new Font("Segoe UI", 9.75f, FontStyle.Regular);

            // Top Header Panel
            _headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 185,
                Padding = new Padding(18, 14, 18, 10),
                BackColor = Color.White
            };

            _lblTitle = new Label
            {
                Text = "TodoTray",
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42),
                AutoSize = true,
                Location = new Point(18, 12)
            };

            _lblHotkeyHint = new Label
            {
                Text = "⚡ Alt+Shift+T anywhere",
                Font = new Font("Segoe UI", 8.25f, FontStyle.Regular),
                ForeColor = Color.FromArgb(100, 116, 139),
                AutoSize = true,
                Location = new Point(135, 18)
            };

            _btnPin = new Button
            {
                Text = "📌 Pin",
                Font = new Font("Segoe UI", 8.5f),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(72, 26),
                Location = new Point(_headerPanel.Width - 92, 12),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Cursor = Cursors.Hand,
                BackColor = Color.FromArgb(241, 245, 249),
                ForeColor = Color.FromArgb(71, 85, 105)
            };
            _btnPin.FlatAppearance.BorderSize = 0;
            _btnPin.Click += (s, e) =>
            {
                this.TopMost = !this.TopMost;
                UpdatePinButtonStyle();
            };

            _lblStats = new Label
            {
                Text = "0 active • 0 completed",
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(100, 116, 139),
                AutoSize = true,
                Location = new Point(19, 44)
            };

            // Progress Bar Container
            _progressContainer = new Panel
            {
                Location = new Point(18, 68),
                Size = new Size(440, 7),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.FromArgb(226, 232, 240)
            };
            _progressBar = new Panel
            {
                Dock = DockStyle.Left,
                Width = 0,
                BackColor = Color.FromArgb(37, 99, 235)
            };
            _progressContainer.Controls.Add(_progressBar);

            _lblProgress = new Label
            {
                Text = "0% done",
                Font = new Font("Segoe UI", 8.25f),
                ForeColor = Color.FromArgb(100, 116, 139),
                AutoSize = true,
                Location = new Point(19, 78)
            };

            // Input Area
            _inputContainer = new Panel
            {
                Location = new Point(18, 98),
                Size = new Size(440, 36),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            _txtNewTodo = new TextBox
            {
                Font = new Font("Segoe UI", 10.5f),
                Location = new Point(0, 3),
                Width = _inputContainer.Width - 170,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _txtNewTodo.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    AddNewTodo();
                }
            };
            SendMessage(_txtNewTodo.Handle, EM_SETCUEBANNER, 0, "What needs doing? (Press Enter)");

            _btnPriorityToggle = new Button
            {
                Text = "⚪ Normal",
                Font = new Font("Segoe UI", 8.5f),
                Size = new Size(82, 30),
                Location = new Point(_inputContainer.Width - 164, 2),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(241, 245, 249),
                ForeColor = Color.FromArgb(71, 85, 105),
                Cursor = Cursors.Hand
            };
            _btnPriorityToggle.FlatAppearance.BorderSize = 0;
            _btnPriorityToggle.Click += (s, e) => CycleNextPriority();

            _btnAdd = new Button
            {
                Text = "+ Add",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                BackColor = Color.FromArgb(37, 99, 235),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(76, 30),
                Location = new Point(_inputContainer.Width - 78, 2),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Cursor = Cursors.Hand
            };
            _btnAdd.FlatAppearance.BorderSize = 0;
            _btnAdd.Click += (s, e) => AddNewTodo();

            _inputContainer.Controls.Add(_txtNewTodo);
            _inputContainer.Controls.Add(_btnPriorityToggle);
            _inputContainer.Controls.Add(_btnAdd);

            // Filter & Search Bar
            _filterPanel = new Panel
            {
                Location = new Point(18, 142),
                Size = new Size(440, 32),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            _btnFilterAll = CreateFilterButton("All", FilterMode.All, 0);
            _btnFilterActive = CreateFilterButton("Active", FilterMode.Active, 58);
            _btnFilterCompleted = CreateFilterButton("Done", FilterMode.Completed, 126);

            _txtSearch = new TextBox
            {
                Font = new Font("Segoe UI", 9f),
                Location = new Point(198, 4),
                Width = 140,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _txtSearch.TextChanged += (s, e) => RefreshTodoList();
            SendMessage(_txtSearch.Handle, EM_SETCUEBANNER, 0, "🔍 Search...");

            _btnClearCompleted = new Button
            {
                Text = "Clear Done",
                Font = new Font("Segoe UI", 8.25f),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(148, 163, 184),
                Size = new Size(86, 26),
                Location = new Point(_filterPanel.Width - 88, 2),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Cursor = Cursors.Hand
            };
            _btnClearCompleted.FlatAppearance.BorderSize = 0;
            _btnClearCompleted.Click += (s, e) => ClearCompleted();

            _filterPanel.Controls.Add(_btnFilterAll);
            _filterPanel.Controls.Add(_btnFilterActive);
            _filterPanel.Controls.Add(_btnFilterCompleted);
            _filterPanel.Controls.Add(_txtSearch);
            _filterPanel.Controls.Add(_btnClearCompleted);

            _headerPanel.Controls.Add(_lblTitle);
            _headerPanel.Controls.Add(_lblHotkeyHint);
            _headerPanel.Controls.Add(_btnPin);
            _headerPanel.Controls.Add(_lblStats);
            _headerPanel.Controls.Add(_progressContainer);
            _headerPanel.Controls.Add(_lblProgress);
            _headerPanel.Controls.Add(_inputContainer);
            _headerPanel.Controls.Add(_filterPanel);

            // Bottom Panel
            _bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 44,
                Padding = new Padding(16, 8, 16, 8),
                BackColor = Color.White
            };

            _lnkStorage = new LinkLabel
            {
                Text = "📁 Storage: todos.json",
                Font = new Font("Segoe UI", 8.5f),
                LinkColor = Color.FromArgb(37, 99, 235),
                ActiveLinkColor = Color.FromArgb(29, 78, 216),
                AutoSize = true,
                Location = new Point(14, 12),
                Cursor = Cursors.Hand
            };
            _lnkStorage.LinkClicked += (s, e) =>
            {
                try
                {
                    string folder = Path.GetDirectoryName(_repo.StoragePath);
                    if (Directory.Exists(folder))
                    {
                        System.Diagnostics.Process.Start("explorer.exe", folder);
                    }
                }
                catch { }
            };

            _btnMinimize = new Button
            {
                Text = "Hide to Tray",
                Font = new Font("Segoe UI", 8.5f),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(241, 245, 249),
                ForeColor = Color.FromArgb(51, 65, 85),
                Size = new Size(96, 26),
                Location = new Point(_bottomPanel.Width - 110, 8),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Cursor = Cursors.Hand
            };
            _btnMinimize.FlatAppearance.BorderSize = 0;
            _btnMinimize.Click += (s, e) => this.Hide();

            _bottomPanel.Controls.Add(_lnkStorage);
            _bottomPanel.Controls.Add(_btnMinimize);

            // Empty state label
            _lblEmpty = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 11f, FontStyle.Regular),
                ForeColor = Color.FromArgb(148, 163, 184),
                BackColor = Color.FromArgb(248, 250, 252),
                Visible = false
            };

            // ListBox for tasks
            _lstTodos = new ListBox
            {
                Dock = DockStyle.Fill,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 52,
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(248, 250, 252),
                IntegralHeight = false
            };
            _lstTodos.DrawItem += LstTodos_DrawItem;
            _lstTodos.MouseDown += LstTodos_MouseDown;
            _lstTodos.DoubleClick += LstTodos_DoubleClick;
            _lstTodos.KeyDown += LstTodos_KeyDown;

            // Context Menu for list items
            _lstTodos.ContextMenuStrip = CreateItemContextMenu();

            // Add controls to Form
            this.Controls.Add(_lstTodos);
            this.Controls.Add(_lblEmpty);
            this.Controls.Add(_bottomPanel);
            this.Controls.Add(_headerPanel);

            UpdateFilterButtonStyles();
        }

        private ContextMenuStrip CreateItemContextMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Toggle Complete", null, (s, e) => ToggleSelectedCompletion());
            menu.Items.Add("Edit Task...", null, (s, e) => EditSelectedTask());
            menu.Items.Add(new ToolStripSeparator());

            // Reminder submenu
            var reminderMenu = new ToolStripMenuItem("⏰ Set Reminder");
            reminderMenu.DropDownItems.Add("In 15 minutes", null, (s, e) => SetReminderOnSelected(TimeSpan.FromMinutes(15)));
            reminderMenu.DropDownItems.Add("In 30 minutes", null, (s, e) => SetReminderOnSelected(TimeSpan.FromMinutes(30)));
            reminderMenu.DropDownItems.Add("In 1 hour", null, (s, e) => SetReminderOnSelected(TimeSpan.FromHours(1)));
            reminderMenu.DropDownItems.Add("In 3 hours", null, (s, e) => SetReminderOnSelected(TimeSpan.FromHours(3)));
            reminderMenu.DropDownItems.Add("Tomorrow 9:00 AM", null, (s, e) => SetReminderTomorrowMorning());
            reminderMenu.DropDownItems.Add("Custom Date & Time...", null, (s, e) => SetCustomReminderOnSelected());
            reminderMenu.DropDownItems.Add(new ToolStripSeparator());
            reminderMenu.DropDownItems.Add("Remove Reminder", null, (s, e) => ClearReminderOnSelected());
            menu.Items.Add(reminderMenu);

            // Priority submenu
            var priorityMenu = new ToolStripMenuItem("⭐ Set Priority");
            priorityMenu.DropDownItems.Add("🔥 High", null, (s, e) => SetPriorityOnSelected(TodoPriority.High));
            priorityMenu.DropDownItems.Add("⚡ Medium", null, (s, e) => SetPriorityOnSelected(TodoPriority.Medium));
            priorityMenu.DropDownItems.Add("⚪ Normal", null, (s, e) => SetPriorityOnSelected(TodoPriority.Normal));
            menu.Items.Add(priorityMenu);

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Delete", null, (s, e) => DeleteSelectedTask());
            return menu;
        }

        private void UpdatePinButtonStyle()
        {
            if (this.TopMost)
            {
                _btnPin.Text = "📌 Pinned";
                _btnPin.BackColor = Color.FromArgb(224, 231, 255);
                _btnPin.ForeColor = Color.FromArgb(37, 99, 235);
                _btnPin.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            }
            else
            {
                _btnPin.Text = "📌 Pin";
                _btnPin.BackColor = Color.FromArgb(241, 245, 249);
                _btnPin.ForeColor = Color.FromArgb(71, 85, 105);
                _btnPin.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            }
        }

        private void CycleNextPriority()
        {
            if (_nextPriority == TodoPriority.Normal)
            {
                _nextPriority = TodoPriority.Medium;
                _btnPriorityToggle.Text = "⚡ Med";
                _btnPriorityToggle.BackColor = Color.FromArgb(254, 243, 199);
                _btnPriorityToggle.ForeColor = Color.FromArgb(180, 83, 9);
            }
            else if (_nextPriority == TodoPriority.Medium)
            {
                _nextPriority = TodoPriority.High;
                _btnPriorityToggle.Text = "🔥 High";
                _btnPriorityToggle.BackColor = Color.FromArgb(254, 226, 226);
                _btnPriorityToggle.ForeColor = Color.FromArgb(185, 28, 28);
            }
            else
            {
                _nextPriority = TodoPriority.Normal;
                _btnPriorityToggle.Text = "⚪ Normal";
                _btnPriorityToggle.BackColor = Color.FromArgb(241, 245, 249);
                _btnPriorityToggle.ForeColor = Color.FromArgb(71, 85, 105);
            }
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                this.Hide();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.N)
            {
                _txtNewTodo.Focus();
                _txtNewTodo.SelectAll();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.F)
            {
                _txtSearch.Focus();
                _txtSearch.SelectAll();
                e.Handled = true;
            }
        }

        private Button CreateFilterButton(string text, FilterMode mode, int xPos)
        {
            var btn = new Button
            {
                Text = text,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(54, 26),
                Location = new Point(xPos, 2),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += (s, e) =>
            {
                _currentFilter = mode;
                UpdateFilterButtonStyles();
                RefreshTodoList();
            };
            return btn;
        }

        private void UpdateFilterButtonStyles()
        {
            ApplyFilterStyle(_btnFilterAll, _currentFilter == FilterMode.All);
            ApplyFilterStyle(_btnFilterActive, _currentFilter == FilterMode.Active);
            ApplyFilterStyle(_btnFilterCompleted, _currentFilter == FilterMode.Completed);
        }

        private void ApplyFilterStyle(Button btn, bool active)
        {
            if (active)
            {
                btn.BackColor = Color.FromArgb(224, 231, 255);
                btn.ForeColor = Color.FromArgb(37, 99, 235);
                btn.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            }
            else
            {
                btn.BackColor = Color.Transparent;
                btn.ForeColor = Color.FromArgb(100, 116, 139);
                btn.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            }
        }

        private void SetupTray()
        {
            _trayMenu = new ContextMenuStrip();
            var openItem = new ToolStripMenuItem("Open TodoTray", null, (s, e) => ShowAndActivate())
            {
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            _trayMenu.Items.Add(openItem);
            _trayMenu.Items.Add("Quick Add Task...", null, (s, e) => QuickAddPrompt());
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add("Clear Completed", null, (s, e) => ClearCompleted());
            _trayMenu.Items.Add(new ToolStripSeparator());

            _menuHourlyNudge = new ToolStripMenuItem("Hourly Reminder Nudges", null, (s, e) =>
            {
                _enableHourlyNudge = !_enableHourlyNudge;
                _menuHourlyNudge.Checked = _enableHourlyNudge;
            })
            {
                Checked = _enableHourlyNudge
            };
            _trayMenu.Items.Add(_menuHourlyNudge);

            _menuStartup = new ToolStripMenuItem("Start with Windows", null, (s, e) =>
            {
                bool newState = !_menuStartup.Checked;
                SetStartup(newState);
                _menuStartup.Checked = newState;
            })
            {
                Checked = IsStartupEnabled()
            };
            _trayMenu.Items.Add(_menuStartup);

            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add("Exit", null, (s, e) =>
            {
                _isExiting = true;
                Application.Exit();
            });

            _notifyIcon = new NotifyIcon
            {
                Text = "TodoTray",
                Icon = _appIcon,
                ContextMenuStrip = _trayMenu,
                Visible = true
            };

            _notifyIcon.DoubleClick += (s, e) => ToggleWindowVisibility();

            _notifyIcon.BalloonTipClicked += (s, e) =>
            {
                ShowAndActivate();
                if (!string.IsNullOrEmpty(_lastNotifiedTodoId))
                {
                    var target = _todos.FirstOrDefault(t => t.Id == _lastNotifiedTodoId);
                    if (target != null && _lstTodos.Items.Contains(target))
                    {
                        _lstTodos.SelectedItem = target;
                    }
                }
            };
        }

        private void ToggleWindowVisibility()
        {
            if (this.Visible && this.WindowState != FormWindowState.Minimized)
            {
                this.Hide();
            }
            else
            {
                ShowAndActivate();
            }
        }

        private bool IsStartupEnabled()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false))
                {
                    return key != null && key.GetValue(AppRegistryName) != null;
                }
            }
            catch
            {
                return false;
            }
        }

        private void SetStartup(bool enable)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true))
                {
                    if (key != null)
                    {
                        if (enable)
                        {
                            key.SetValue(AppRegistryName, "\"" + Application.ExecutablePath + "\"");
                        }
                        else
                        {
                            key.DeleteValue(AppRegistryName, false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not update Windows startup setting:\n" + ex.Message,
                    "TodoTray Settings",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
            }
        }

        public void ShowAndActivate()
        {
            this.Show();
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.WindowState = FormWindowState.Normal;
            }
            this.BringToFront();
            this.Activate();
            _txtNewTodo.Focus();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_isExiting && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();

                if (!_hasShownBalloon)
                {
                    _hasShownBalloon = true;
                    _notifyIcon.ShowBalloonTip(
                        2500,
                        "TodoTray Running in Background",
                        "TodoTray is still in your system tray! Press Alt+Shift+T anytime to summon it.",
                        ToolTipIcon.Info
                    );
                }
                return;
            }

            if (_reminderTimer != null)
            {
                _reminderTimer.Stop();
                _reminderTimer.Dispose();
            }

            if (_hourlyNudgeTimer != null)
            {
                _hourlyNudgeTimer.Stop();
                _hourlyNudgeTimer.Dispose();
            }

            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }

            base.OnFormClosing(e);
        }

        private void AddNewTodo()
        {
            string text = _txtNewTodo.Text.Trim();
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var item = new TodoItem(text, _nextPriority);
            _todos.Insert(0, item);
            _repo.Save(_todos);

            _txtNewTodo.Clear();
            RefreshTodoList();
        }

        private void QuickAddPrompt()
        {
            using (var prompt = new Form())
            {
                prompt.Width = 380;
                prompt.Height = 160;
                prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
                prompt.Text = "TodoTray - Quick Add";
                prompt.StartPosition = FormStartPosition.CenterScreen;
                prompt.MaximizeBox = false;
                prompt.MinimizeBox = false;
                prompt.ShowInTaskbar = false;
                prompt.Icon = _appIcon;

                var textLabel = new Label { Left = 16, Top = 14, Text = "Task description:", AutoSize = true, Font = new Font("Segoe UI", 9f) };
                var textBox = new TextBox { Left = 16, Top = 36, Width = 330, Font = new Font("Segoe UI", 10f) };

                var cmbPriority = new ComboBox { Left = 16, Top = 74, Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
                cmbPriority.Items.AddRange(new object[] { "⚪ Normal", "⚡ Medium", "🔥 High" });
                cmbPriority.SelectedIndex = 0;

                var confirmation = new Button { Text = "Add Task", Left = 246, Width = 100, Top = 72, DialogResult = DialogResult.OK, BackColor = Color.FromArgb(37, 99, 235), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
                confirmation.FlatAppearance.BorderSize = 0;

                prompt.Controls.Add(textLabel);
                prompt.Controls.Add(textBox);
                prompt.Controls.Add(cmbPriority);
                prompt.Controls.Add(confirmation);
                prompt.AcceptButton = confirmation;

                if (prompt.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(textBox.Text))
                {
                    var p = (TodoPriority)cmbPriority.SelectedIndex;
                    _todos.Insert(0, new TodoItem(textBox.Text.Trim(), p));
                    _repo.Save(_todos);
                    RefreshTodoList();
                }
            }
        }

        private void ClearCompleted()
        {
            int removed = _todos.RemoveAll(t => t.IsCompleted);
            if (removed > 0)
            {
                _repo.Save(_todos);
                RefreshTodoList();
            }
        }

        private void ToggleSelectedCompletion()
        {
            if (_lstTodos.SelectedItem is TodoItem item)
            {
                bool wasCompleted = item.IsCompleted;
                item.IsCompleted = !item.IsCompleted;
                _repo.Save(_todos);
                RefreshTodoList();

                // Celebration if user completed the final remaining task!
                if (!wasCompleted && _todos.Count > 0 && _todos.All(t => t.IsCompleted))
                {
                    _notifyIcon.ShowBalloonTip(
                        3500,
                        "TodoTray 🎉",
                        "All tasks completed! Fantastic job clearing your list!",
                        ToolTipIcon.Info
                    );
                }
            }
        }

        private void DeleteSelectedTask()
        {
            if (_lstTodos.SelectedItem is TodoItem item)
            {
                _todos.Remove(item);
                _repo.Save(_todos);
                RefreshTodoList();
            }
        }

        private void EditSelectedTask()
        {
            if (_lstTodos.SelectedItem is TodoItem item)
            {
                using (var prompt = new Form())
                {
                    prompt.Width = 360;
                    prompt.Height = 150;
                    prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
                    prompt.Text = "TodoTray - Edit Task";
                    prompt.StartPosition = FormStartPosition.CenterParent;
                    prompt.MaximizeBox = false;
                    prompt.MinimizeBox = false;
                    prompt.ShowInTaskbar = false;
                    prompt.Icon = _appIcon;

                    var textLabel = new Label { Left = 16, Top = 14, Text = "Update task description:", AutoSize = true, Font = new Font("Segoe UI", 9f) };
                    var textBox = new TextBox { Left = 16, Top = 36, Width = 312, Text = item.Title, Font = new Font("Segoe UI", 10f) };
                    var confirmation = new Button { Text = "Save", Left = 248, Width = 80, Top = 72, DialogResult = DialogResult.OK, BackColor = Color.FromArgb(37, 99, 235), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
                    confirmation.FlatAppearance.BorderSize = 0;

                    prompt.Controls.Add(textLabel);
                    prompt.Controls.Add(textBox);
                    prompt.Controls.Add(confirmation);
                    prompt.AcceptButton = confirmation;

                    if (prompt.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(textBox.Text))
                    {
                        item.Title = textBox.Text.Trim();
                        _repo.Save(_todos);
                        RefreshTodoList();
                    }
                }
            }
        }

        private void SetPriorityOnSelected(TodoPriority priority)
        {
            if (_lstTodos.SelectedItem is TodoItem item)
            {
                item.SetPriority(priority);
                _repo.Save(_todos);
                RefreshTodoList();
            }
        }

        private void SetReminderOnSelected(TimeSpan delay)
        {
            if (_lstTodos.SelectedItem is TodoItem item)
            {
                item.SetReminder(DateTime.Now.Add(delay));
                _repo.Save(_todos);
                RefreshTodoList();

                _notifyIcon.ShowBalloonTip(
                    2000,
                    "TodoTray Reminder Set ⏰",
                    string.Format("Reminder scheduled for: {0:h\\:mm tt}", item.ReminderTime),
                    ToolTipIcon.Info
                );
            }
        }

        private void SetReminderTomorrowMorning()
        {
            if (_lstTodos.SelectedItem is TodoItem item)
            {
                var target = DateTime.Today.AddDays(1).AddHours(9); // Tomorrow 9 AM
                item.SetReminder(target);
                _repo.Save(_todos);
                RefreshTodoList();

                _notifyIcon.ShowBalloonTip(
                    2000,
                    "TodoTray Reminder Set ⏰",
                    "Reminder set for tomorrow at 9:00 AM.",
                    ToolTipIcon.Info
                );
            }
        }

        private void SetCustomReminderOnSelected()
        {
            if (!(_lstTodos.SelectedItem is TodoItem item)) return;

            using (var form = new Form())
            {
                form.Text = "Set Reminder - TodoTray";
                form.Size = new Size(330, 180);
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.Icon = _appIcon;

                var lbl = new Label { Text = "Pick reminder date and time:", Location = new Point(16, 16), AutoSize = true, Font = new Font("Segoe UI", 9f) };
                var dtp = new DateTimePicker
                {
                    Location = new Point(16, 42),
                    Width = 280,
                    Format = DateTimePickerFormat.Custom,
                    CustomFormat = "yyyy-MM-dd   hh:mm tt",
                    MinDate = DateTime.Now,
                    Value = item.ReminderTime.HasValue && item.ReminderTime.Value > DateTime.Now ? item.ReminderTime.Value : DateTime.Now.AddHours(1)
                };

                var btnOk = new Button { Text = "Set", Location = new Point(136, 92), Width = 75, DialogResult = DialogResult.OK, BackColor = Color.FromArgb(37, 99, 235), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
                btnOk.FlatAppearance.BorderSize = 0;
                var btnCancel = new Button { Text = "Cancel", Location = new Point(220, 92), Width = 75, DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat };

                form.Controls.AddRange(new Control[] { lbl, dtp, btnOk, btnCancel });
                form.AcceptButton = btnOk;

                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    item.SetReminder(dtp.Value);
                    _repo.Save(_todos);
                    RefreshTodoList();

                    _notifyIcon.ShowBalloonTip(
                        2000,
                        "TodoTray Reminder Set ⏰",
                        string.Format("Reminder set for: {0:MMM dd, h\\:mm tt}", dtp.Value),
                        ToolTipIcon.Info
                    );
                }
            }
        }

        private void ClearReminderOnSelected()
        {
            if (_lstTodos.SelectedItem is TodoItem item)
            {
                item.ClearReminder();
                _repo.Save(_todos);
                RefreshTodoList();
            }
        }

        private void RefreshTodoList()
        {
            int total = _todos.Count;
            int activeCount = _todos.Count(t => !t.IsCompleted);
            int doneCount = _todos.Count(t => t.IsCompleted);

            _lblStats.Text = string.Format("{0} active • {1} completed", activeCount, doneCount);

            // Update progress bar
            int percent = total > 0 ? (int)Math.Round((double)doneCount / total * 100) : 0;
            _lblProgress.Text = string.Format("{0}% done ({1}/{2})", percent, doneCount, total);
            int barWidth = (int)Math.Round((_progressContainer.Width * percent) / 100.0);
            _progressBar.Width = Math.Max(0, Math.Min(_progressContainer.Width, barWidth));
            _progressBar.BackColor = percent == 100 ? Color.FromArgb(34, 197, 94) : Color.FromArgb(37, 99, 235);

            // Update tray tooltip (limit to 63 chars max for NotifyIcon)
            string trayText = string.Format("TodoTray ({0} active)", activeCount);
            if (trayText.Length > 63) trayText = trayText.Substring(0, 63);
            _notifyIcon.Text = trayText;

            // Search filter
            string searchTerm = _txtSearch.Text.Trim();

            // Filter items for display
            IEnumerable<TodoItem> query = _todos;
            if (_currentFilter == FilterMode.Active)
            {
                query = query.Where(t => !t.IsCompleted);
            }
            else if (_currentFilter == FilterMode.Completed)
            {
                query = query.Where(t => t.IsCompleted);
            }

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(t => t.Title.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            // Order by Priority (High to Normal) then CreatedAt for active items
            var itemsToDisplay = query
                .OrderBy(t => t.IsCompleted)
                .ThenByDescending(t => t.Priority)
                .ThenByDescending(t => t.CreatedAt)
                .ToList();

            var selectedItem = _lstTodos.SelectedItem as TodoItem;

            if (itemsToDisplay.Count == 0)
            {
                _lstTodos.Visible = false;
                _lblEmpty.Visible = true;
                _lblEmpty.BringToFront();

                if (!string.IsNullOrEmpty(searchTerm))
                    _lblEmpty.Text = string.Format("No tasks matching \"{0}\"", searchTerm);
                else if (_currentFilter == FilterMode.Completed)
                    _lblEmpty.Text = "No completed tasks yet.";
                else if (_currentFilter == FilterMode.Active)
                    _lblEmpty.Text = "All caught up! No active tasks. 🎉";
                else
                    _lblEmpty.Text = "No tasks yet!\nAdd your first task above. ✍️";
            }
            else
            {
                _lblEmpty.Visible = false;
                _lstTodos.Visible = true;
                _lstTodos.BringToFront();

                _lstTodos.BeginUpdate();
                _lstTodos.Items.Clear();
                foreach (var item in itemsToDisplay)
                {
                    _lstTodos.Items.Add(item);
                }
                if (selectedItem != null && _lstTodos.Items.Contains(selectedItem))
                {
                    _lstTodos.SelectedItem = selectedItem;
                }
                _lstTodos.EndUpdate();
            }
        }

        private void LstTodos_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _lstTodos.Items.Count)
            {
                return;
            }

            var item = (TodoItem)_lstTodos.Items[e.Index];
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Clear background
            using (var bgBrush = new SolidBrush(Color.FromArgb(248, 250, 252)))
            {
                g.FillRectangle(bgBrush, e.Bounds);
            }

            // Card rectangle
            var cardRect = new Rectangle(e.Bounds.X + 12, e.Bounds.Y + 3, e.Bounds.Width - 24, e.Bounds.Height - 6);
            bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

            Color cardBg = isSelected ? Color.FromArgb(238, 242, 255) : Color.White;
            Color cardBorder = isSelected ? Color.FromArgb(165, 180, 252) : Color.FromArgb(226, 232, 240);

            using (var path = GetRoundedRectangle(cardRect, 6))
            {
                using (var brush = new SolidBrush(cardBg))
                {
                    g.FillPath(brush, path);
                }
                using (var pen = new Pen(cardBorder, 1f))
                {
                    g.DrawPath(pen, path);
                }
            }

            // Checkbox
            int checkSize = 18;
            var checkRect = new Rectangle(cardRect.X + 12, cardRect.Y + (cardRect.Height - checkSize) / 2, checkSize, checkSize);

            if (item.IsCompleted)
            {
                using (var fillBrush = new SolidBrush(Color.FromArgb(34, 197, 94)))
                using (var path = GetRoundedRectangle(checkRect, 4))
                {
                    g.FillPath(fillBrush, path);
                }
                using (var checkPen = new Pen(Color.White, 2.2f))
                {
                    checkPen.StartCap = LineCap.Round;
                    checkPen.EndCap = LineCap.Round;
                    g.DrawLines(checkPen, new[]
                    {
                        new Point(checkRect.X + 4, checkRect.Y + 9),
                        new Point(checkRect.X + 8, checkRect.Y + 13),
                        new Point(checkRect.X + 14, checkRect.Y + 5)
                    });
                }
            }
            else
            {
                using (var borderPen = new Pen(Color.FromArgb(148, 163, 184), 1.5f))
                using (var path = GetRoundedRectangle(checkRect, 4))
                {
                    g.DrawPath(borderPen, path);
                }
            }

            // Delete button ("×") on the right
            int delSize = 20;
            var delRect = new Rectangle(cardRect.Right - delSize - 12, cardRect.Y + (cardRect.Height - delSize) / 2, delSize, delSize);
            using (var delFont = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            using (var delBrush = new SolidBrush(Color.FromArgb(156, 163, 175)))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("×", delFont, delBrush, delRect, sf);
            }

            // Left side starting position for badges & text
            int curX = checkRect.Right + 10;

            // Draw Priority pill (if High or Medium)
            if (item.Priority == (int)TodoPriority.High)
            {
                var pillRect = new Rectangle(curX, cardRect.Y + (cardRect.Height - 18) / 2, 46, 18);
                using (var path = GetRoundedRectangle(pillRect, 4))
                using (var brush = new SolidBrush(Color.FromArgb(254, 226, 226)))
                using (var textBrush = new SolidBrush(Color.FromArgb(185, 28, 28)))
                using (var pillFont = new Font("Segoe UI", 7.5f, FontStyle.Bold))
                {
                    g.FillPath(brush, path);
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("🔥 High", pillFont, textBrush, pillRect, sf);
                }
                curX += pillRect.Width + 6;
            }
            else if (item.Priority == (int)TodoPriority.Medium)
            {
                var pillRect = new Rectangle(curX, cardRect.Y + (cardRect.Height - 18) / 2, 44, 18);
                using (var path = GetRoundedRectangle(pillRect, 4))
                using (var brush = new SolidBrush(Color.FromArgb(254, 243, 199)))
                using (var textBrush = new SolidBrush(Color.FromArgb(180, 83, 9)))
                using (var pillFont = new Font("Segoe UI", 7.5f, FontStyle.Bold))
                {
                    g.FillPath(brush, path);
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("⚡ Med", pillFont, textBrush, pillRect, sf);
                }
                curX += pillRect.Width + 6;
            }

            // Right side boundary for task text (reserve space for reminder badge if present)
            int textRight = delRect.Left - 6;

            // Draw Reminder badge on the right side if active
            string reminderText = item.GetReminderBadgeText();
            if (!string.IsNullOrEmpty(reminderText))
            {
                int badgeW = 74;
                var remRect = new Rectangle(delRect.Left - badgeW - 6, cardRect.Y + (cardRect.Height - 18) / 2, badgeW, 18);
                bool isOverdue = reminderText.Contains("Overdue");

                Color remBg = isOverdue ? Color.FromArgb(254, 226, 226) : Color.FromArgb(241, 245, 249);
                Color remText = isOverdue ? Color.FromArgb(185, 28, 28) : Color.FromArgb(71, 85, 105);

                using (var path = GetRoundedRectangle(remRect, 4))
                using (var brush = new SolidBrush(remBg))
                using (var textBrush = new SolidBrush(remText))
                using (var badgeFont = new Font("Segoe UI", 7.5f, isOverdue ? FontStyle.Bold : FontStyle.Regular))
                {
                    g.FillPath(brush, path);
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(reminderText, badgeFont, textBrush, remRect, sf);
                }
                textRight = remRect.Left - 6;
            }

            // Task text
            int textWidth = textRight - curX;
            if (textWidth > 10)
            {
                var textRect = new Rectangle(curX, cardRect.Y, textWidth, cardRect.Height);
                FontStyle fontStyle = item.IsCompleted ? FontStyle.Strikeout : FontStyle.Regular;
                Color textColor = item.IsCompleted ? Color.FromArgb(148, 163, 184) : Color.FromArgb(30, 41, 59);

                using (var textFont = new Font(this.Font.FontFamily, 9.75f, fontStyle))
                using (var textBrush = new SolidBrush(textColor))
                {
                    var sf = new StringFormat
                    {
                        LineAlignment = StringAlignment.Center,
                        Trimming = StringTrimming.EllipsisCharacter,
                        FormatFlags = StringFormatFlags.NoWrap
                    };
                    g.DrawString(item.Title, textFont, textBrush, textRect, sf);
                }
            }
        }

        private static GraphicsPath GetRoundedRectangle(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int diameter = radius * 2;
            var size = new Size(diameter, diameter);
            var arc = new Rectangle(rect.Location, size);

            path.AddArc(arc, 180, 90);
            arc.X = rect.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = rect.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = rect.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void LstTodos_MouseDown(object sender, MouseEventArgs e)
        {
            int index = _lstTodos.IndexFromPoint(e.Location);
            if (index < 0 || index >= _lstTodos.Items.Count)
            {
                return;
            }

            _lstTodos.SelectedIndex = index;
            var item = (TodoItem)_lstTodos.Items[index];

            var itemRect = _lstTodos.GetItemRectangle(index);
            var cardRect = new Rectangle(itemRect.X + 12, itemRect.Y + 3, itemRect.Width - 24, itemRect.Height - 6);

            // Check if clicked in checkbox area (left ~42px of the card)
            if (e.X >= cardRect.X && e.X <= cardRect.X + 38 && e.Button == MouseButtons.Left)
            {
                ToggleSelectedCompletion();
                return;
            }

            // Check if clicked in delete area (right ~36px of the card)
            if (e.X >= cardRect.Right - 36 && e.X <= cardRect.Right && e.Button == MouseButtons.Left)
            {
                DeleteSelectedTask();
                return;
            }

            // Check if clicked in reminder area (around right 40 to 115px)
            if (item.HasReminder && e.X >= cardRect.Right - 115 && e.X <= cardRect.Right - 40 && e.Button == MouseButtons.Left)
            {
                SetCustomReminderOnSelected();
                return;
            }
        }

        private void LstTodos_DoubleClick(object sender, EventArgs e)
        {
            ToggleSelectedCompletion();
        }

        private void LstTodos_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                DeleteSelectedTask();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Space)
            {
                ToggleSelectedCompletion();
                e.Handled = true;
            }
        }
    }
}
