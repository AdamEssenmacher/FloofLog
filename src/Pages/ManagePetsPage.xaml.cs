using System;

using FloofLog.ViewModels;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;

namespace FloofLog.Pages;

public partial class ManagePetsPage : ContentPage
{
    public ManagePetsPage()
        : this(GetRequiredService<ManagePetsViewModel>())
    {
    }

    public ManagePetsPage(ManagePetsViewModel viewModel)
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
