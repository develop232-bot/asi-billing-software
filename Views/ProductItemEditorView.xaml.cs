using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AsiErp.ViewModels;

namespace AsiErp.Views
{
    public partial class ProductItemEditorView : UserControl
    {
        public ProductItemEditorView()
        {
            InitializeComponent();
        }

        private void ItemName_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (DataContext is ProductItemEditorViewModel vm)
                {
                    vm.IsSuggestionsOpen = false;
                }
            }
        }

        private void ItemName_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb && DataContext is ProductItemEditorViewModel vm)
            {
                vm.TrySelectExactMatch(tb.Text);
            }
        }
    }
}
