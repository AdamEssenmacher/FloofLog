using FloofLog.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FloofLog;

public partial class MainPage : ContentPage
{
    public MainPage()
        : this(GetRequiredService<MainPageViewModel>())
    {
    }

    public MainPage(MainPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private static TService GetRequiredService<TService>()
        where TService : notnull
    {
        if (Application.Current?.Handler?.MauiContext?.Services is IServiceProvider services)
        {
            return services.GetRequiredService<TService>();
        }

        throw new InvalidOperationException("Unable to resolve service.");
    }
}
