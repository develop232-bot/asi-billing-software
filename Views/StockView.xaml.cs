using System;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Data;
using AsiErp.ViewModels;

namespace AsiErp.Views
{
    public partial class StockView : UserControl
    {
        public StockView()
        {
            InitializeComponent();
            Loaded += (s, e) => ClearGridSort();
        }

        private void ClearGridSort()
        {
            if (StockGrid?.ItemsSource != null)
            {
                ICollectionView view = CollectionViewSource.GetDefaultView(StockGrid.ItemsSource);
                if (view != null && view.SortDescriptions.Count > 0)
                {
                    view.SortDescriptions.Clear();
                    foreach (var col in StockGrid.Columns)
                    {
                        col.SortDirection = null;
                    }
                }
            }
        }
    }
}
