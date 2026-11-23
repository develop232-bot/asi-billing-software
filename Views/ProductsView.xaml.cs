using System;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Data;
using AsiErp.ViewModels;

namespace AsiErp.Views
{
    public partial class ProductsView : UserControl
    {
        public ProductsView()
        {
            InitializeComponent();
            Loaded += (s, e) => ClearGridSort();
            DataContextChanged += (s, e) =>
            {
                if (DataContext is ProductsViewModel vm)
                {
                    vm.SortResetRequested -= ClearGridSort;
                    vm.SortResetRequested += ClearGridSort;
                }
            };
        }

        private void ClearGridSort()
        {
            if (ProductsGrid?.ItemsSource != null)
            {
                ICollectionView view = CollectionViewSource.GetDefaultView(ProductsGrid.ItemsSource);
                if (view != null && view.SortDescriptions.Count > 0)
                {
                    view.SortDescriptions.Clear();
                    foreach (var col in ProductsGrid.Columns)
                    {
                        col.SortDirection = null;
                    }
                }
            }
        }
    }
}
