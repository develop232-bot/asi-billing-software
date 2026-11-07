using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using AsiErp.Commands;
using AsiErp.Models;
using AsiErp.Services;

namespace AsiErp.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly AppDataService _dataService;
        private ViewModelBase _currentView;
        private string _activeRoute = "dashboard";
        private string _currentBreadcrumb = "Dashboard";
        private bool _isSidebarExpanded = true;
        private bool _isDarkMode = false;
        private bool _isNotificationPopupOpen = false;
        private bool _isNewMenuOpen = false;

        private readonly DispatcherTimer _clockTimer;

        // Child ViewModels
        public DashboardViewModel DashboardVM { get; }
        public PosViewModel PosVM { get; }
        public SalesViewModel SalesVM { get; }
        public DomesticInvoiceViewModel DomesticInvoiceVM { get; }
        public ExportInvoiceViewModel ExportInvoiceVM { get; }
        public PurchaseViewModel PurchaseVM { get; }
        public ProductionViewModel ProductionVM { get; }
        public StockViewModel StockVM { get; }
        public ProductsViewModel ProductsVM { get; }
        public AccountsViewModel AccountsVM { get; }

        public ViewModelBase CurrentView
        {
            get => _currentView;
            set => SetField(ref _currentView, value);
        }

        public string ActiveRoute
        {
            get => _activeRoute;
            set => SetField(ref _activeRoute, value);
        }

        public string CurrentBreadcrumb
        {
            get => _currentBreadcrumb;
            set => SetField(ref _currentBreadcrumb, value);
        }

        public bool IsSidebarExpanded
        {
            get => _isSidebarExpanded;
            set
            {
                if (SetField(ref _isSidebarExpanded, value))
                {
                    OnPropertyChanged(nameof(IsSidebarCompact));
                    OnPropertyChanged(nameof(SidebarColumnWidth));
                    OnPropertyChanged(nameof(SidebarToggleIcon));
                    OnPropertyChanged(nameof(SidebarToggleTooltip));
                }
            }
        }

        public bool IsSidebarCompact => !_isSidebarExpanded;
        public GridLength SidebarColumnWidth => _isSidebarExpanded ? new GridLength(252) : new GridLength(68);
        public string SidebarToggleIcon => _isSidebarExpanded ? "◀" : "☰";
        public string SidebarToggleTooltip => _isSidebarExpanded ? "Collapse Sidebar to Compact Rail" : "Expand Navigation Sidebar";

        public bool IsDarkMode
        {
            get => _isDarkMode;
            set
            {
                if (SetField(ref _isDarkMode, value)) ApplyTheme();
            }
        }

        public bool IsNotificationPopupOpen
        {
            get => _isNotificationPopupOpen;
            set => SetField(ref _isNotificationPopupOpen, value);
        }

        public bool IsNewMenuOpen
        {
            get => _isNewMenuOpen;
            set => SetField(ref _isNewMenuOpen, value);
        }

        public string CurrentDateFormatted => DateTime.Now.ToString("dd MMM yyyy");
        public string CurrentDayOfWeek => DateTime.Now.ToString("dddd");

        public bool IsFirebaseConnected => _dataService.Firebase.IsConnected;
        public string FirebaseStatusText => _dataService.Firebase.StatusMessage;
        public int SyncPendingCount => _dataService.SyncQueue.PendingCount;

        public string FirebaseStatusBadge
        {
            get
            {
                if (!IsFirebaseConnected)
                {
                    return SyncPendingCount > 0
                        ? $"● Offline ({SyncPendingCount} queued)"
                        : "● Local Mode";
                }

                if (SyncPendingCount > 0)
                {
                    return $"⏳ Syncing ({SyncPendingCount} queued)";
                }

                return $"✔ Cloud ({_dataService.Firebase.ProjectId})";
            }
        }

        public string FirebaseStatusDotColor
        {
            get
            {
                if (!IsFirebaseConnected) return "#E11D48";
                if (SyncPendingCount > 0) return "#D97706";
                return "#10B981";
            }
        }

        public bool IsSampleDataLoaded => _dataService.IsSampleDataLoaded;
        public string DataModeLabel => IsSampleDataLoaded ? "Demo Mode (Sample Data)" : "Live Cloud DB (Active)";
        public string DataModeBgColor => IsSampleDataLoaded ? "#FEF3C7" : "#ECFDF5";
        public string DataModeFgColor => IsSampleDataLoaded ? "#92400E" : "#065F46";
        public string DataModeDotColor => IsSampleDataLoaded ? "#D97706" : "#10B981";
        public string DataModeBorderColor => IsSampleDataLoaded ? "#FCD34D" : "#A7F3D0";

        public ObservableCollection<AppNotification> Notifications { get; } = new();
        public int NotificationCount => Notifications.Count;
        public bool HasNotifications => NotificationCount > 0;

        private bool _isUserMenuOpen;
        public bool IsUserMenuOpen
        {
            get => _isUserMenuOpen;
            set => SetField(ref _isUserMenuOpen, value);
        }

        public bool IsLoggedIn => _dataService.Auth.IsLoggedIn;
        public bool IsAdmin => _dataService.Auth.IsAdmin;
        public bool IsStoreKeeper => _dataService.Auth.IsStoreKeeper;
        public bool IsFinance => _dataService.Auth.IsFinance;

        // Role-based Access Permissives (Store Keeper now also has full Billing and Product Masters)
        public bool CanAccessDashboard => IsAdmin || IsFinance || IsStoreKeeper;
        public bool CanAccessBilling => IsAdmin || IsFinance || IsStoreKeeper;
        public bool CanAccessCatalog => IsAdmin || IsStoreKeeper || IsFinance;
        public bool CanAccessStore => IsAdmin || IsStoreKeeper;
        public bool CanAccessManufacturing => IsAdmin || IsStoreKeeper;
        public bool CanAccessFinance => IsAdmin || IsFinance;

        public string LoggedInUserRole => _dataService.Auth.CurrentUser?.Role ?? "guest";
        public string LoggedInUserName => _dataService.Auth.CurrentUser?.FullName ?? "User";
        public string LoggedInUserHandle => _dataService.Auth.CurrentUser?.Username ?? "user";
        public string LoggedInUserInitials
        {
            get
            {
                string name = LoggedInUserName;
                if (string.IsNullOrEmpty(name)) return "U";
                var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2) return $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant();
                return name.Substring(0, Math.Min(2, name.Length)).ToUpperInvariant();
            }
        }
        public string LoggedInRoleBadge => _dataService.Auth.CurrentUser?.RoleTitle ?? "Staff";
        public string LoggedInRoleColor => _dataService.Auth.CurrentUser?.RoleBadgeColor ?? "#475569";

        // Navigation & Menu Commands
        public RelayCommand NavigateCommand { get; }
        public RelayCommand ToggleSidebarCommand { get; }
        public RelayCommand ToggleThemeCommand { get; }
        public RelayCommand ToggleNotificationsCommand { get; }
        public RelayCommand ToggleNewMenuCommand { get; }
        public RelayCommand ToggleUserMenuCommand { get; }
        public RelayCommand LogoutCommand { get; }
        public RelayCommand OpenUserManagementCommand { get; }
        public RelayCommand StartFreshCommand { get; }
        public RelayCommand ReloadSampleCommand { get; }
        public RelayCommand LoadDemoDataCommand { get; }
        public RelayCommand LoadCloudDbCommand { get; }
        public RelayCommand ToggleDataModeCommand { get; }
        public RelayCommand SyncToCloudCommand { get; }
        public RelayCommand TestFirebaseCommand { get; }
        public RelayCommand OpenDialogCommand { get; }

        public event Action<string, object?>? RequestOpenDialog;

        public MainViewModel()
        {
            _dataService = AppDataService.Instance;

            DashboardVM = new DashboardViewModel(_dataService, NavigateTo, OpenDialog);
            PosVM = new PosViewModel(_dataService, OpenDialog, NavigateTo);
            SalesVM = new SalesViewModel(_dataService, NavigateTo, OpenDialog);
            DomesticInvoiceVM = new DomesticInvoiceViewModel(_dataService, NavigateTo, OpenDialog);
            ExportInvoiceVM = new ExportInvoiceViewModel(_dataService, NavigateTo, OpenDialog);
            PurchaseVM = new PurchaseViewModel(_dataService, OpenDialog, "raw");
            ProductionVM = new ProductionViewModel(_dataService, OpenDialog);
            StockVM = new StockViewModel(_dataService, OpenDialog, "raw");
            ProductsVM = new ProductsViewModel(_dataService, OpenDialog);
            AccountsVM = new AccountsViewModel(_dataService, OpenDialog, "income");

            // Sync subtabs when clicked inside the views back to ActiveRoute and Breadcrumb
            PurchaseVM.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(PurchaseViewModel.ActiveKind))
                {
                    string r = "pur:" + PurchaseVM.ActiveKind;
                    if (_activeRoute != r)
                    {
                        _activeRoute = r;
                        OnPropertyChanged(nameof(ActiveRoute));
                    }
                    CurrentBreadcrumb = PurchaseVM.Title;
                }
            };

            StockVM.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(StockViewModel.ActiveTab))
                {
                    string r = "stock:" + StockVM.ActiveTab;
                    if (_activeRoute != r)
                    {
                        _activeRoute = r;
                        OnPropertyChanged(nameof(ActiveRoute));
                    }
                    CurrentBreadcrumb = StockVM.Title;
                }
            };

            AccountsVM.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(AccountsViewModel.ActiveTab))
                {
                    string r = "acc:" + AccountsVM.ActiveTab;
                    if (_activeRoute != r)
                    {
                        _activeRoute = r;
                        OnPropertyChanged(nameof(ActiveRoute));
                    }
                    CurrentBreadcrumb = AccountsVM.ActiveTab switch
                    {
                        "income" => "Accounts · Income",
                        "exp" => "Accounts · Expenses",
                        "inv" => "Accounts · Investment",
                        "pl" => "Accounts · Profit & Loss",
                        _ => "Accounts"
                    };
                }
            };

            _currentView = DashboardVM;

            NavigateCommand = new RelayCommand(p =>
            {
                if (p is string r) NavigateTo(r);
            });

            ToggleSidebarCommand = new RelayCommand(_ => IsSidebarExpanded = !IsSidebarExpanded);
            ToggleThemeCommand = new RelayCommand(_ => IsDarkMode = !IsDarkMode);
            ToggleNotificationsCommand = new RelayCommand(_ => IsNotificationPopupOpen = !IsNotificationPopupOpen);
            ToggleNewMenuCommand = new RelayCommand(_ => IsNewMenuOpen = !IsNewMenuOpen);

            LoadDemoDataCommand = new RelayCommand(_ =>
            {
                var res = MessageBox.Show(
                    "Load demo sample data for footwear and uniform operations?\n\nThis will show only demo records in all software modules.",
                    "Load Demo Data", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                {
                    _dataService.LoadDemoData();
                    RefreshDataModeProperties();
                    NavigateTo("dashboard");
                }
            });
            ReloadSampleCommand = LoadDemoDataCommand;

            LoadCloudDbCommand = new RelayCommand(async _ =>
            {
                var res = MessageBox.Show(
                    "Clear demo data and load live records from Firebase Cloud Firestore?\n\nReal database data will be loaded into all modules and all new changes will sync directly to your database.",
                    "Clear Demo & Load Cloud DB", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                {
                    var (ok, msg, count) = await _dataService.LoadFromCloudDatabaseAsync();
                    RefreshDataModeProperties();
                    NavigateTo("dashboard");

                    MessageBox.Show(msg, "Cloud Database Sync", MessageBoxButton.OK, ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
                }
            });
            StartFreshCommand = LoadCloudDbCommand;

            ToggleDataModeCommand = new RelayCommand(async _ =>
            {
                if (IsSampleDataLoaded)
                {
                    var res = MessageBox.Show(
                        "Currently in Demo Mode.\n\nSwitch to Live Cloud Database? This clears demo data and loads your real records from Firebase Firestore.",
                        "Switch to Live Cloud DB", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (res == MessageBoxResult.Yes)
                    {
                        var (ok, msg, count) = await _dataService.LoadFromCloudDatabaseAsync();
                        RefreshDataModeProperties();
                        NavigateTo("dashboard");
                        MessageBox.Show(msg, "Cloud Database", MessageBoxButton.OK, ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
                    }
                }
                else
                {
                    var res = MessageBox.Show(
                        "Currently in Live Cloud DB Mode.\n\nSwitch to Demo Mode? This loads demo sample data for testing across the software.",
                        "Switch to Demo Mode", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (res == MessageBoxResult.Yes)
                    {
                        _dataService.LoadDemoData();
                        RefreshDataModeProperties();
                        NavigateTo("dashboard");
                    }
                }
            });

            SyncToCloudCommand = new RelayCommand(async _ =>
            {
                var res = MessageBox.Show(
                    "Queue all current software records to send to Firebase Cloud Firestore?\n\nThey will be stored safely inside the application and transmitted one by one to the cloud database.",
                    "Sync All to Cloud", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                {
                    await _dataService.PushAllToCloudAsync();
                    RefreshDataModeProperties();
                    MessageBox.Show($"All records queued ({_dataService.SyncQueue.PendingCount} items). The background worker will send them one by one to Firestore.", "Sync Initiated", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            });

            TestFirebaseCommand = new RelayCommand(async _ =>
            {
                _dataService.Firebase.TryInitialize();
                bool ok = await _dataService.Firebase.TestConnectionAsync();
                if (ok && _dataService.SyncQueue.PendingCount > 0)
                {
                    _ = _dataService.SyncQueue.ProcessQueueAsync();
                }
                RefreshDataModeProperties();

                int pending = _dataService.SyncQueue.PendingCount;
                string queueInfo = pending == 0
                    ? "All local changes are fully synced to Firestore."
                    : $"{pending} operations queued — transmitting one by one to Firestore.";

                MessageBox.Show(
                    ok ? $"Connected to Firebase Cloud Firestore successfully! (Project: {_dataService.Firebase.ProjectId})\n\n{queueInfo}"
                       : $"Firebase Status: {_dataService.Firebase.StatusMessage}\n\nNote: All data is saved safely inside the application and will be sent automatically when internet reconnects.\n\nKey location:\n{_dataService.Firebase.KeyFilePath}",
                    "Cloud Sync Status", MessageBoxButton.OK, ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
            });

            OpenDialogCommand = new RelayCommand(p =>
            {
                IsNewMenuOpen = false;
                if (p is string dlg) OpenDialog(dlg, null);
            });

            ToggleUserMenuCommand = new RelayCommand(_ => IsUserMenuOpen = !IsUserMenuOpen);

            LogoutCommand = new RelayCommand(_ =>
            {
                IsUserMenuOpen = false;
                var res = MessageBox.Show("Sign out from your session?", "Sign Out", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                {
                    _dataService.Auth.Logout();
                }
            });

            OpenUserManagementCommand = new RelayCommand(_ =>
            {
                IsUserMenuOpen = false;
                var dlg = new Dialogs.UserManagementWindow { Owner = Application.Current?.MainWindow };
                dlg.ShowDialog();
            });

            _dataService.Auth.AuthStateChanged += () =>
            {
                App.Current?.Dispatcher?.Invoke(() =>
                {
                    RefreshAuthProperties();
                });
            };

            _dataService.SyncQueue.QueueChanged += (count, msg) =>
            {
                App.Current?.Dispatcher?.Invoke(() =>
                {
                    RefreshDataModeProperties();
                });
            };

            _dataService.Firebase.ConnectionChanged += () =>
            {
                RefreshDataModeProperties();
            };

            _dataService.DataChanged += () =>
            {
                RefreshNotifications();
                RefreshDataModeProperties();
            };

            RefreshNotifications();
            RefreshDataModeProperties();

            // Try auto-login on startup
            _dataService.Auth.TryAutoLogin();
            RefreshAuthProperties();

            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _clockTimer.Tick += (s, e) =>
            {
                OnPropertyChanged(nameof(CurrentDateFormatted));
                OnPropertyChanged(nameof(CurrentDayOfWeek));
            };
            _clockTimer.Start();
        }

        public void RefreshAuthProperties()
        {
            OnPropertyChanged(nameof(IsLoggedIn));
            OnPropertyChanged(nameof(IsAdmin));
            OnPropertyChanged(nameof(IsStoreKeeper));
            OnPropertyChanged(nameof(IsFinance));
            OnPropertyChanged(nameof(CanAccessDashboard));
            OnPropertyChanged(nameof(CanAccessBilling));
            OnPropertyChanged(nameof(CanAccessCatalog));
            OnPropertyChanged(nameof(CanAccessStore));
            OnPropertyChanged(nameof(CanAccessManufacturing));
            OnPropertyChanged(nameof(CanAccessFinance));
            OnPropertyChanged(nameof(LoggedInUserRole));
            OnPropertyChanged(nameof(LoggedInUserName));
            OnPropertyChanged(nameof(LoggedInUserHandle));
            OnPropertyChanged(nameof(LoggedInUserInitials));
            OnPropertyChanged(nameof(LoggedInRoleBadge));
            OnPropertyChanged(nameof(LoggedInRoleColor));

            if (IsLoggedIn)
            {
                NavigateToDefaultRoleView();
            }
        }

        public void NavigateToDefaultRoleView()
        {
            if (IsStoreKeeper && !IsAdmin)
            {
                NavigateTo("sales:retail");
            }
            else if (IsFinance && !IsAdmin)
            {
                NavigateTo("sales:retail");
            }
            else
            {
                NavigateTo("dashboard");
            }
        }

        public (bool success, string error) PerformLogin(string username, string password, bool rememberMe)
        {
            var res = _dataService.Auth.Login(username, password, rememberMe);
            if (res.success)
            {
                RefreshAuthProperties();
            }
            return res;
        }

        public void RefreshDataModeProperties()
        {
            OnPropertyChanged(nameof(IsSampleDataLoaded));
            OnPropertyChanged(nameof(DataModeLabel));
            OnPropertyChanged(nameof(DataModeBgColor));
            OnPropertyChanged(nameof(DataModeFgColor));
            OnPropertyChanged(nameof(DataModeDotColor));
            OnPropertyChanged(nameof(DataModeBorderColor));
            OnPropertyChanged(nameof(IsFirebaseConnected));
            OnPropertyChanged(nameof(FirebaseStatusText));
            OnPropertyChanged(nameof(FirebaseStatusBadge));
            OnPropertyChanged(nameof(FirebaseStatusDotColor));
            OnPropertyChanged(nameof(SyncPendingCount));
        }

        public void NavigateTo(string route)
        {
            IsNotificationPopupOpen = false;
            IsNewMenuOpen = false;

            if (route == "dashboard")
            {
                ActiveRoute = route;
                CurrentBreadcrumb = "Dashboard";
                CurrentView = DashboardVM;
            }
            else if (route == "sales")
            {
                ActiveRoute = route;
                CurrentBreadcrumb = "Sales & Billing";
                SalesVM.SelectedChannel = "all";
                CurrentView = SalesVM;
            }
            else if (route == "sales:retail" || route == "pos")
            {
                ActiveRoute = "sales:retail";
                CurrentBreadcrumb = "Sales & Billing · Retail Billing";
                CurrentView = PosVM;
            }
            else if (route == "sales:domestic")
            {
                ActiveRoute = route;
                CurrentBreadcrumb = "Sales & Billing · Domestic Invoice";
                DomesticInvoiceVM.ResetForm();
                CurrentView = DomesticInvoiceVM;
            }
            else if (route == "sales:export")
            {
                ActiveRoute = route;
                CurrentBreadcrumb = "Sales & Billing · Export Invoice";
                ExportInvoiceVM.ResetForm();
                CurrentView = ExportInvoiceVM;
            }
            else if (route == "purchase")
            {
                ActiveRoute = route;
                CurrentBreadcrumb = "Purchase & Sourcing";
                PurchaseVM.ActiveKind = "all";
                CurrentView = PurchaseVM;
            }
            else if (route.StartsWith("pur:"))
            {
                string kind = route.Split(':')[1];
                PurchaseVM.ActiveKind = kind;
                ActiveRoute = route;
                CurrentBreadcrumb = PurchaseVM.Title;
                CurrentView = PurchaseVM;
            }
            else if (route == "production")
            {
                ActiveRoute = route;
                CurrentBreadcrumb = "Production";
                CurrentView = ProductionVM;
            }
            else if (route.StartsWith("stock:"))
            {
                string tab = route.Split(':')[1];
                StockVM.ActiveTab = tab;
                ActiveRoute = route;
                CurrentBreadcrumb = StockVM.Title;
                CurrentView = StockVM;
            }
            else if (route == "products")
            {
                ActiveRoute = route;
                CurrentBreadcrumb = "Products";
                CurrentView = ProductsVM;
            }
            else if (route.StartsWith("acc:"))
            {
                string tab = route.Split(':')[1];
                AccountsVM.ActiveTab = tab;
                ActiveRoute = route;
                CurrentBreadcrumb = tab switch
                {
                    "income" => "Accounts · Income",
                    "exp" => "Accounts · Expenses",
                    "inv" => "Accounts · Investment",
                    "pl" => "Accounts · Profit & Loss",
                    _ => "Accounts"
                };
                CurrentView = AccountsVM;
            }
        }

        public void OpenDialog(string dialogType, object? parameter)
        {
            RequestOpenDialog?.Invoke(dialogType, parameter);
        }

        private void RefreshNotifications()
        {
            Notifications.Clear();
            foreach (var n in _dataService.GetNotifications())
            {
                Notifications.Add(n);
            }
            OnPropertyChanged(nameof(NotificationCount));
            OnPropertyChanged(nameof(HasNotifications));
        }

        private void ApplyTheme()
        {
            // Dynamic brush switching handled via ResourceDictionary or DynamicResources
            var app = Application.Current;
            if (app != null)
            {
                var res = app.Resources;
                if (_isDarkMode)
                {
                    res["BgBrush"] = System.Windows.Media.Brushes.DarkSlateGray; // or custom hex
                }
            }
        }
    }
}
