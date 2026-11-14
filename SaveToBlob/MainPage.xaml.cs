using SaveToBlob.ViewModels;

namespace SaveToBlob;

public partial class MainPage : ContentPage
{
    public MainPage(AzuriteSenderViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

}