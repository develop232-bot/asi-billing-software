using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using AsiErp.Dialogs;
using AsiErp.Models;
using AsiErp.Services;
using AsiErp.ViewModels;

namespace AsiErp
{
    public partial class MainWindow : Window
    {
        private readonly Dictionary<Type, FrameworkElement> _viewCache = new();

        private MainViewModel? _viewModel => DataContext as MainViewModel;
        public MainWindow()
        {
            InitializeComponent();

            // Pre-fill default admin password so user can sign in immediately
            LoginPasswordBox.Password = "admin";

            // Wire up silent auto-update status banner
            UpdateService.Instance.StatusChanged += status =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (UpdateService.Instance.IsDownloading || status.Contains("New version") || status.Contains("Restarting"))
                    {
                        AutoUpdateBanner.Visibility = Visibility.Visible;
                        TxtAutoUpdateStatus.Text = status;
                    }
                    else
                    {
                        AutoUpdateBanner.Visibility = Visibility.Collapsed;
                    }
                });
            };

            UpdateService.Instance.DownloadProgressChanged += percent =>
            {
                Dispatcher.Invoke(() =>
                {
                    AutoUpdateBanner.Visibility = Visibility.Visible;
                    PbAutoUpdate.Value = percent;
                });
            };

            // Check for updates automatically in background immediately on startup
            if (UpdateService.Instance.Config.AutoCheckOnStartup)
            {
                _ = System.Threading.Tasks.Task.Run(async () =>
                {
                    await System.Threading.Tasks.Task.Delay(1000);
                    await UpdateService.Instance.CheckForUpdatesAsync(isManualCheck: false);
                });
            }

            if (DataContext is MainViewModel vm)
            {
                vm.RequestOpenDialog += OnRequestOpenDialog;
                vm.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(MainViewModel.CurrentView) && vm.CurrentView != null)
                    {
                        SwitchView(vm.CurrentView);
                    }
                };
                if (vm.CurrentView != null)
                {
                    SwitchView(vm.CurrentView);
                }
            }
        }

        private void SwitchView(ViewModelBase currentVm)
        {
            var type = currentVm.GetType();
            if (!_viewCache.TryGetValue(type, out var view))
            {
                view = type.Name switch
                {
                    nameof(DashboardViewModel) => new Views.DashboardView(),
                    nameof(PosViewModel) => new Views.PosView(),
                    nameof(SalesViewModel) => new Views.SalesView(),
                    nameof(PurchaseViewModel) => new Views.PurchaseView(),
                    nameof(ProductionViewModel) => new Views.ProductionView(),
                    nameof(StockViewModel) => new Views.StockView(),
                    nameof(ProductsViewModel) => new Views.ProductsView(),
                    nameof(AccountsViewModel) => new Views.AccountsView(),
                    _ => null
                };
                if (view != null)
                {
                    _viewCache[type] = view;
                }
            }

            if (view != null)
            {
                view.DataContext = currentVm;
                MainContentArea.Content = view;
            }
        }

        private void OnRequestOpenDialog(string dialogType, object? parameter)
        {
            switch (dialogType)
            {
                case "domestic_invoice":
                    {
                        var dlg = new DomesticInvoiceWindow { Owner = this };
                        if (dlg.ShowDialog() == true && dlg.CreatedSale != null)
                        {
                            DocumentPrinter.PrintDirect(dlg.CreatedSale, silent: true);
                        }
                        break;
                    }

                case "export_invoice":
                    {
                        var dlg = new ExportInvoiceWindow { Owner = this };
                        if (dlg.ShowDialog() == true && dlg.CreatedSale != null)
                        {
                            DocumentPrinter.PrintDirect(dlg.CreatedSale, silent: true);
                        }
                        break;
                    }

                case "purchase_raw":
                    {
                        var dlg = new PurchaseBillWindow("raw") { Owner = this };
                        dlg.ShowDialog();
                        break;
                    }

                case "purchase_ready":
                    {
                        var dlg = new PurchaseBillWindow("ready") { Owner = this };
                        dlg.ShowDialog();
                        break;
                    }

                case "purchase_gen":
                    {
                        var dlg = new PurchaseBillWindow("gen") { Owner = this };
                        dlg.ShowDialog();
                        break;
                    }

                case "purchase_bill":
                    {
                        string kind = parameter?.ToString() ?? "raw";
                        var dlg = new PurchaseBillWindow(kind) { Owner = this };
                        dlg.ShowDialog();
                        break;
                    }

                case "work_order":
                    {
                        var wo = parameter as WorkOrderDoc;
                        var dlg = new WorkOrderWindow(wo) { Owner = this };
                        dlg.ShowDialog();
                        break;
                    }

                case "product_edit_mfg":
                    {
                        var dlg = new ProductEditWindow("mfg", parameter) { Owner = this };
                        dlg.ShowDialog();
                        break;
                    }

                case "product_edit_ready":
                    {
                        var dlg = new ProductEditWindow("ready", parameter) { Owner = this };
                        dlg.ShowDialog();
                        break;
                    }

                case "product_edit_raw":
                    {
                        var dlg = new ProductEditWindow("raw", parameter) { Owner = this };
                        dlg.ShowDialog();
                        break;
                    }

                case "product_edit_gen":
                    {
                        var dlg = new ProductEditWindow("gen", parameter) { Owner = this };
                        dlg.ShowDialog();
                        break;
                    }

                case "stock_adjust":
                    {
                        if (parameter is StockDisplayItem item)
                        {
                            var dlg = new StockAdjustWindow(item) { Owner = this };
                            dlg.ShowDialog();
                        }
                        break;
                    }

                case "payment_sale":
                    {
                        if (parameter is SaleDoc sale)
                        {
                            var dlg = new PaymentWindow("sales", sale) { Owner = this };
                            dlg.ShowDialog();
                        }
                        break;
                    }

                case "payment_purchase":
                    {
                        if (parameter is PurchaseDoc purchase)
                        {
                            var dlg = new PaymentWindow("purchases", purchase) { Owner = this };
                            dlg.ShowDialog();
                        }
                        break;
                    }

                case "payment_quick":
                    {
                        var unpaidSale = AppDataService.Instance.Sales.FirstOrDefault(s => s.Status != "Paid");
                        if (unpaidSale != null)
                        {
                            var dlg = new PaymentWindow("sales", unpaidSale) { Owner = this };
                            dlg.ShowDialog();
                        }
                        else
                        {
                            var unpaidPurchase = AppDataService.Instance.Purchases.FirstOrDefault(p => p.Status != "Paid");
                            if (unpaidPurchase != null)
                            {
                                var dlg = new PaymentWindow("purchases", unpaidPurchase) { Owner = this };
                                dlg.ShowDialog();
                            }
                            else
                            {
                                MessageBox.Show("No pending invoices or vendor bills found.", "All Settled", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                        }
                        break;
                    }

                case "expense_add":
                    {
                        var dlg = new ExpenseWindow { Owner = this };
                        dlg.ShowDialog();
                        break;
                    }

                case "investment_add":
                    {
                        var dlg = new InvestmentWindow { Owner = this };
                        dlg.ShowDialog();
                        break;
                    }

                case "print_barcode":
                    {
                        if (parameter is ProductItem prod)
                        {
                            var dlg = new BarcodePrintWindow(prod) { Owner = this };
                            dlg.ShowDialog();
                        }
                        break;
                    }

                case "view_doc":
                case "view_purchase_doc":
                    {
                        if (parameter != null)
                        {
                            var dlg = new DocumentViewerWindow(parameter) { Owner = this };
                            dlg.ShowDialog();
                        }
                        break;
                    }
                case "users":
                    {
                        var dlg = new Dialogs.UserManagementWindow { Owner = this };
                        dlg.ShowDialog();
                        break;
                    }

                case "bulk_csv_import":
                    {
                        var dlg = new Dialogs.BulkProductImportWindow { Owner = this };
                        dlg.ShowDialog();
                        break;
                    }
            }
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            PerformLoginAction();
        }

        private void LoginInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                PerformLoginAction();
            }
        }

        private void PerformLoginAction()
        {
            LoginErrorText.Visibility = Visibility.Collapsed;
            string username = LoginUsernameBox.Text?.Trim() ?? "";
            string password = LoginPasswordBox.Password ?? "";
            bool remember = LoginRememberCheckBox.IsChecked == true;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                LoginErrorText.Text = "Please enter both username and password.";
                LoginErrorText.Visibility = Visibility.Visible;
                return;
            }

            if (_viewModel != null)
            {
                var result = _viewModel.PerformLogin(username, password, remember);
                if (result.success)
                {
                    LoginPasswordBox.Password = "";
                    LoginErrorText.Visibility = Visibility.Collapsed;
                }
                else
                {
                    LoginErrorText.Text = string.IsNullOrEmpty(result.error) ? "Invalid username or password. Please try again." : result.error;
                    LoginErrorText.Visibility = Visibility.Visible;
                }
            }
        }

        private void AdminFill_Click(object sender, RoutedEventArgs e)
        {
            LoginUsernameBox.Text = "admin";
            LoginPasswordBox.Password = "admin";
            LoginErrorText.Visibility = Visibility.Collapsed;
        }

        private void StoreFill_Click(object sender, RoutedEventArgs e)
        {
            LoginUsernameBox.Text = "store";
            LoginPasswordBox.Password = "store";
            LoginErrorText.Visibility = Visibility.Collapsed;
        }

        private void FinanceFill_Click(object sender, RoutedEventArgs e)
        {
            LoginUsernameBox.Text = "finance";
            LoginPasswordBox.Password = "finance";
            LoginErrorText.Visibility = Visibility.Collapsed;
        }

    }
}
