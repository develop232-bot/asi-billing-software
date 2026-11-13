using System;
using System.Windows.Controls;
using System.Windows.Input;
using AsiErp.ViewModels;

namespace AsiErp.Views
{
    public partial class PosView : UserControl
    {
        public PosView()
        {
            InitializeComponent();

            Loaded += (s, e) =>
            {
                TxtBarcodeScan?.Focus();
            };

            KeyDown += (s, e) =>
            {
                if (e.Key == Key.F2)
                {
                    TxtBarcodeScan?.Focus();
                    TxtBarcodeScan?.SelectAll();
                    e.Handled = true;
                }
            };
        }

        private void CbQuickAdd_DropDownOpened(object sender, EventArgs e)
        {
            if (DataContext is PosViewModel vm)
            {
                vm.ResetQuickAddFilter();
            }
        }
    }
}