using System;
using System.Collections.Generic;
using System.Windows.Controls;
using Windows.ApplicationModel.Activation;
using TelegramClient.ViewModels;

public sealed partial class App : CaliburnApplication
{
    private WinRTContainer container;

    public App()
    {
        InitializeComponent();
    }

    protected override void Configure()
    {
        container = new WinRTContainer();

        container.RegisterWinRTServices();

        container.PerRequest<ShellViewModel>();
    }

    protected override void PrepareViewFirst(Frame rootFrame)
    {
        container.RegisterNavigationService(rootFrame);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        DisplayRootViewFor<ShellViewModel>();
    }

    protected override object GetInstance(Type service, string key)
    {
        return container.GetInstance(service, key);
    }

    protected override IEnumerable<object> GetAllInstances(Type service)
    {
        return container.GetAllInstances(service);
    }

    protected override void BuildUp(object instance)
    {
        container.BuildUp(instance);
    }
}