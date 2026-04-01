using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BitLockerUFCV.Client.Models;
using BitLockerUFCV.Client.Services;
using BitLockerUFCV.Client.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace BitLockerUFCV.Client
{
    public sealed partial class MainWindow : Window
    {
        private readonly CancellationTokenSource _lifetimeCancellation = new();
        private bool _hasInitialized;
        private bool _isSanitizingPasswordInput;

        public MainWindow()
        {
            PowerShellRunner powerShellRunner = new();
            PostponeCounterService postponeCounterService = new();
            BitLockerService bitLockerService = new(powerShellRunner);
            StartupValidationService startupValidationService = new(
                new ExecutionContextService(),
                new RegistryPolicyService(),
                new DomainConnectivityService(powerShellRunner),
                postponeCounterService,
                bitLockerService);

            ViewModel = new MainViewModel(startupValidationService, bitLockerService, postponeCounterService);

            InitializeComponent();
            RootGrid.DataContext = ViewModel;
            ConfigureWindow();
            AppWindow.Closing += OnAppWindowClosing;
            Closed += OnWindowClosed;
        }

        public MainViewModel ViewModel { get; }

        private async void OnRootLoaded(object sender, RoutedEventArgs e)
        {
            if (_hasInitialized)
            {
                return;
            }

            _hasInitialized = true;

            AppDialog? startupDialog = await ViewModel.InitializeAsync(_lifetimeCancellation.Token);
            if (startupDialog is null)
            {
                return;
            }

            await ShowDialogAsync(startupDialog);
            ViewModel.MarkExitWithoutPostpone();
            Close();
        }

        private void OnPinPasswordChanged(object sender, RoutedEventArgs e)
        {
            string sanitized = SanitizePasswordBox((PasswordBox)sender);
            ViewModel.UpdatePin(sanitized);
        }

        private void OnPinConfirmationPasswordChanged(object sender, RoutedEventArgs e)
        {
            string sanitized = SanitizePasswordBox((PasswordBox)sender);
            ViewModel.UpdatePinConfirmation(sanitized);
        }

        private async void OnValidateClicked(object sender, RoutedEventArgs e)
        {
            PinSubmissionValidationResult validationResult = ViewModel.ValidatePinSubmission();
            if (!validationResult.CanContinue)
            {
                if (validationResult.ClearConfirmation)
                {
                    ClearPasswordBox(PinConfirmationBox);
                    ViewModel.UpdatePinConfirmation(string.Empty);
                }

                if (validationResult.Dialog is not null)
                {
                    await ShowDialogAsync(validationResult.Dialog);
                }

                return;
            }

            Task provisioningTask = ViewModel.StartProvisioningAsync(_lifetimeCancellation.Token);
            ClearPasswordBox(PinInputBox);
            ClearPasswordBox(PinConfirmationBox);
            await provisioningTask;
        }

        private void OnPostponeClicked(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.CanPostpone)
            {
                return;
            }

            ViewModel.RequestPostpone();
            Close();
        }

        private void OnFinishClicked(object sender, RoutedEventArgs e)
        {
            ViewModel.RequestFinish();
            Close();
        }

        private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
        {
            WindowCloseDecision decision = ViewModel.EvaluateWindowClose();
            if (!decision.Cancel)
            {
                return;
            }

            args.Cancel = true;

            if (decision.Dialog is not null)
            {
                DispatcherQueue.TryEnqueue(async () => await ShowDialogAsync(decision.Dialog));
            }
        }

        private void OnWindowClosed(object sender, WindowEventArgs args)
        {
            _lifetimeCancellation.Cancel();
            _lifetimeCancellation.Dispose();
        }

        private void ConfigureWindow()
        {
            Title = "Activation BitLocker - UFCV";

            if (AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
            }

            AppWindow.Resize(new SizeInt32(900, 760));
            CenterWindow();
        }

        private void CenterWindow()
        {
            DisplayArea displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
            RectInt32 workArea = displayArea.WorkArea;
            SizeInt32 currentSize = AppWindow.Size;
            int x = workArea.X + Math.Max(0, (workArea.Width - currentSize.Width) / 2);
            int y = workArea.Y + Math.Max(0, (workArea.Height - currentSize.Height) / 2);
            AppWindow.Move(new PointInt32(x, y));
        }

        private async Task ShowDialogAsync(AppDialog dialog)
        {
            if (RootGrid.XamlRoot is null)
            {
                return;
            }

            ContentDialog contentDialog = new()
            {
                XamlRoot = RootGrid.XamlRoot,
                Title = dialog.Title,
                CloseButtonText = dialog.CloseButtonText,
                DefaultButton = ContentDialogButton.Close,
                Content = new TextBlock
                {
                    Text = dialog.Message,
                    TextWrapping = TextWrapping.WrapWholeWords,
                    MaxWidth = 480
                }
            };

            await contentDialog.ShowAsync();
        }

        private string SanitizePasswordBox(PasswordBox passwordBox)
        {
            if (_isSanitizingPasswordInput)
            {
                return passwordBox.Password;
            }

            string sanitized = new(passwordBox.Password.Where(char.IsDigit).Take(20).ToArray());
            if (sanitized == passwordBox.Password)
            {
                return sanitized;
            }

            _isSanitizingPasswordInput = true;
            passwordBox.Password = sanitized;
            _isSanitizingPasswordInput = false;
            return sanitized;
        }

        private void ClearPasswordBox(PasswordBox passwordBox)
        {
            if (string.IsNullOrEmpty(passwordBox.Password))
            {
                return;
            }

            _isSanitizingPasswordInput = true;
            passwordBox.Password = string.Empty;
            _isSanitizingPasswordInput = false;
        }
    }
}
