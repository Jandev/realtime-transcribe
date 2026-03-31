using RealtimeTranscribe.ViewModels;

namespace RealtimeTranscribe;

public partial class DevicesPage : ContentPage
{
    public DevicesPage(DevicesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Refresh the device list each time the page becomes visible so any
        // newly connected devices are picked up automatically.
        if (BindingContext is DevicesViewModel vm)
            vm.RefreshDevicesCommand.Execute(null);
    }
}
