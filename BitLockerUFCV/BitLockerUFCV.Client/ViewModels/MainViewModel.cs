using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using BitLockerUFCV.Client.Models;
using BitLockerUFCV.Client.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace BitLockerUFCV.Client.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly StartupValidationService _startupValidationService;
    private readonly BitLockerService _bitLockerService;
    private readonly PostponeCounterService _postponeCounterService;

    private bool _isInitializing = true;
    private bool _isProgressVisible;
    private bool _isProvisioning;
    private bool _isFinishVisible;
    private bool _isPostponeEnabled = true;
    private bool _canValidate;
    private bool _hasLoadedCounterState;
    private bool _postponeCounterCommitted;
    private int _currentPostponeCount;
    private string _pinValue = string.Empty;
    private string _pinConfirmationValue = string.Empty;
    private string _systemContextWarning = string.Empty;
    private string _loadingStatusText = "Vérification de l'environnement...";
    private string _postponeCounterText = "Reports restants : 99/99";
    private string _postponeButtonText = "Plus tard";
    private string _progressStatusText = "Préparation...";
    private string _progressPercentText = "0%";
    private string _pinValidationMessage = string.Empty;
    private string _pinConfirmationValidationMessage = string.Empty;
    private double _progressValue;
    private WindowAction _windowAction = WindowAction.None;
    private SolidColorBrush _postponeCounterBrush;
    private SolidColorBrush _pinBorderBrush;
    private SolidColorBrush _pinConfirmationBorderBrush;

    public MainViewModel(
        StartupValidationService startupValidationService,
        BitLockerService bitLockerService,
        PostponeCounterService postponeCounterService)
    {
        _startupValidationService = startupValidationService;
        _bitLockerService = bitLockerService;
        _postponeCounterService = postponeCounterService;

        _postponeCounterBrush = CreateBrush(0x66, 0xBB, 0x6A);
        _pinBorderBrush = CreateBrush(0xB5, 0xC0, 0xD0, 0xC8);
        _pinConfirmationBorderBrush = CreateBrush(0xB5, 0xC0, 0xD0, 0xC8);
        ProgressSteps = new ObservableCollection<ProgressStepItem>();
    }

    public ObservableCollection<ProgressStepItem> ProgressSteps { get; }

    public string SystemContextWarning
    {
        get => _systemContextWarning;
        private set
        {
            if (SetProperty(ref _systemContextWarning, value))
            {
                OnPropertyChanged(nameof(SystemContextWarningVisibility));
            }
        }
    }

    public Visibility SystemContextWarningVisibility => string.IsNullOrWhiteSpace(SystemContextWarning) ? Visibility.Collapsed : Visibility.Visible;

    public bool IsInitializing
    {
        get => _isInitializing;
        private set
        {
            if (SetProperty(ref _isInitializing, value))
            {
                OnPropertyChanged(nameof(LoadingOverlayVisibility));
                OnPropertyChanged(nameof(IsPinFormEnabled));
                OnPropertyChanged(nameof(CanPostpone));
                RecalculateCanValidate();
            }
        }
    }

    public Visibility LoadingOverlayVisibility => IsInitializing ? Visibility.Visible : Visibility.Collapsed;

    public bool IsProgressVisible
    {
        get => _isProgressVisible;
        private set
        {
            if (SetProperty(ref _isProgressVisible, value))
            {
                OnPropertyChanged(nameof(PinViewVisibility));
                OnPropertyChanged(nameof(ProgressViewVisibility));
                OnPropertyChanged(nameof(IsPinFormEnabled));
                OnPropertyChanged(nameof(CanPostpone));
                RecalculateCanValidate();
            }
        }
    }

    public Visibility PinViewVisibility => IsProgressVisible ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ProgressViewVisibility => IsProgressVisible ? Visibility.Visible : Visibility.Collapsed;

    public bool IsProvisioning
    {
        get => _isProvisioning;
        private set
        {
            if (SetProperty(ref _isProvisioning, value))
            {
                OnPropertyChanged(nameof(IsPinFormEnabled));
                OnPropertyChanged(nameof(CanPostpone));
                RecalculateCanValidate();
            }
        }
    }

    public bool IsPinFormEnabled => !IsInitializing && !IsProvisioning && !IsProgressVisible;

    public string LoadingStatusText
    {
        get => _loadingStatusText;
        private set => SetProperty(ref _loadingStatusText, value);
    }

    public string PostponeCounterText
    {
        get => _postponeCounterText;
        private set => SetProperty(ref _postponeCounterText, value);
    }

    public SolidColorBrush PostponeCounterBrush
    {
        get => _postponeCounterBrush;
        private set => SetProperty(ref _postponeCounterBrush, value);
    }

    public string PostponeButtonText
    {
        get => _postponeButtonText;
        private set => SetProperty(ref _postponeButtonText, value);
    }

    public bool IsPostponeEnabled
    {
        get => _isPostponeEnabled;
        private set
        {
            if (SetProperty(ref _isPostponeEnabled, value))
            {
                OnPropertyChanged(nameof(CanPostpone));
            }
        }
    }

    public bool CanPostpone => IsPostponeEnabled && !IsInitializing && !IsProvisioning;

    public bool CanValidate
    {
        get => _canValidate;
        private set => SetProperty(ref _canValidate, value);
    }

    public string PinValidationMessage
    {
        get => _pinValidationMessage;
        private set
        {
            if (SetProperty(ref _pinValidationMessage, value))
            {
                OnPropertyChanged(nameof(PinValidationVisibility));
            }
        }
    }

    public Visibility PinValidationVisibility => string.IsNullOrWhiteSpace(PinValidationMessage) ? Visibility.Collapsed : Visibility.Visible;

    public string PinConfirmationValidationMessage
    {
        get => _pinConfirmationValidationMessage;
        private set
        {
            if (SetProperty(ref _pinConfirmationValidationMessage, value))
            {
                OnPropertyChanged(nameof(PinConfirmationValidationVisibility));
            }
        }
    }

    public Visibility PinConfirmationValidationVisibility => string.IsNullOrWhiteSpace(PinConfirmationValidationMessage) ? Visibility.Collapsed : Visibility.Visible;

    public SolidColorBrush PinBorderBrush
    {
        get => _pinBorderBrush;
        private set => SetProperty(ref _pinBorderBrush, value);
    }

    public SolidColorBrush PinConfirmationBorderBrush
    {
        get => _pinConfirmationBorderBrush;
        private set => SetProperty(ref _pinConfirmationBorderBrush, value);
    }

    public string ProgressStatusText
    {
        get => _progressStatusText;
        private set => SetProperty(ref _progressStatusText, value);
    }

    public double ProgressValue
    {
        get => _progressValue;
        private set => SetProperty(ref _progressValue, value);
    }

    public string ProgressPercentText
    {
        get => _progressPercentText;
        private set => SetProperty(ref _progressPercentText, value);
    }

    public bool IsFinishVisible
    {
        get => _isFinishVisible;
        private set
        {
            if (SetProperty(ref _isFinishVisible, value))
            {
                OnPropertyChanged(nameof(FinishButtonVisibility));
            }
        }
    }

    public Visibility FinishButtonVisibility => IsFinishVisible ? Visibility.Visible : Visibility.Collapsed;

    public async Task<AppDialog?> InitializeAsync(CancellationToken cancellationToken)
    {
        LoadingStatusText = "Vérification de l'environnement...";

        StartupAssessment assessment = await _startupValidationService.EvaluateAsync(cancellationToken);
        SystemContextWarning = assessment.SystemContextWarning ?? string.Empty;

        if (assessment.PostponeCounterState is not null)
        {
            ApplyPostponeState(assessment.PostponeCounterState);
        }

        IsInitializing = false;

        if (assessment.BlockingDialog is not null)
        {
            _windowAction = WindowAction.ExitWithoutPostpone;
            return assessment.BlockingDialog;
        }

        return null;
    }

    public void UpdatePin(string pinValue)
    {
        _pinValue = pinValue ?? string.Empty;
        RefreshPinValidationState();
    }

    public void UpdatePinConfirmation(string pinConfirmationValue)
    {
        _pinConfirmationValue = pinConfirmationValue ?? string.Empty;
        RefreshPinValidationState();
    }

    public PinSubmissionValidationResult ValidatePinSubmission()
    {
        (bool isValid, string message) = ValidatePinValue(_pinValue);
        if (!isValid)
        {
            return new PinSubmissionValidationResult(false, new AppDialog("Erreur", message));
        }

        if (_pinValue != _pinConfirmationValue)
        {
            return new PinSubmissionValidationResult(
                false,
                new AppDialog("Erreur", "Les deux codes PIN ne correspondent pas. Veuillez réessayer."),
                ClearConfirmation: true);
        }

        return new PinSubmissionValidationResult(true);
    }

    public async Task StartProvisioningAsync(CancellationToken cancellationToken)
    {
        string pinToProvision = _pinValue;

        _pinValue = string.Empty;
        _pinConfirmationValue = string.Empty;
        RefreshPinValidationState();

        _windowAction = WindowAction.Provisioning;
        IsProvisioning = true;
        IsProgressVisible = true;
        IsFinishVisible = false;
        ProgressValue = 0;
        ProgressPercentText = "0%";
        ProgressStatusText = "Démarrage de la configuration...";
        ProgressSteps.Clear();
        AddProgressStep(ProgressStepKind.InProgress, "Initialisation...");

        try
        {
            ProvisioningResult result = await _bitLockerService.ProvisionAsync(
                pinToProvision,
                new Progress<ProvisioningProgressEvent>(HandleProvisioningProgress),
                cancellationToken);

            CompleteProgress(result.Message, isError: false);
        }
        catch (Exception exception)
        {
            CompleteProgress($"Erreur : {exception.Message}", isError: true);
        }
    }

    public void RequestPostpone()
    {
        _windowAction = WindowAction.Postponed;
    }

    public void RequestFinish()
    {
        _windowAction = WindowAction.Validated;
    }

    public void MarkExitWithoutPostpone()
    {
        _windowAction = WindowAction.ExitWithoutPostpone;
    }

    public WindowCloseDecision EvaluateWindowClose()
    {
        if (IsInitializing && !_hasLoadedCounterState)
        {
            return new WindowCloseDecision(false);
        }

        if (IsProvisioning)
        {
            AddProgressStep(ProgressStepKind.Warning, "Merci de patienter : configuration en cours...");
            return new WindowCloseDecision(true);
        }

        if (HasReachedPostponeLimit() && _windowAction is not (WindowAction.Validated or WindowAction.Completed or WindowAction.ExitWithoutPostpone))
        {
            return new WindowCloseDecision(
                true,
                new AppDialog("BitLocker", "Limite de reports atteinte. L'activation BitLocker est obligatoire."));
        }

        if (_windowAction is WindowAction.Validated or WindowAction.Completed or WindowAction.ExitWithoutPostpone)
        {
            return new WindowCloseDecision(false);
        }

        if (_windowAction == WindowAction.None)
        {
            _windowAction = WindowAction.Postponed;
        }

        if (_windowAction == WindowAction.Postponed && !_postponeCounterCommitted)
        {
            ApplyPostponeState(_postponeCounterService.Increment());
            _postponeCounterCommitted = true;
        }

        return new WindowCloseDecision(false);
    }

    private void HandleProvisioningProgress(ProvisioningProgressEvent progressEvent)
    {
        int clampedPercent = Math.Clamp(progressEvent.Percent, 0, 100);
        ProgressValue = clampedPercent;
        ProgressPercentText = $"{clampedPercent}%";
        ProgressStatusText = progressEvent.Text;
        AddProgressStep(progressEvent.Kind, progressEvent.Text);
    }

    private void CompleteProgress(string finalStatus, bool isError)
    {
        ProgressStatusText = finalStatus;
        IsProvisioning = false;
        IsFinishVisible = true;

        if (_windowAction == WindowAction.Provisioning)
        {
            _windowAction = WindowAction.Completed;
        }

        AddProgressStep(
            isError ? ProgressStepKind.Error : ProgressStepKind.Success,
            isError ? $"Échec : {finalStatus}" : $"Terminé : {finalStatus}");
    }

    private void ApplyPostponeState(PostponeCounterState state)
    {
        _hasLoadedCounterState = true;
        _currentPostponeCount = state.CurrentCount;

        if (state.HasReachedLimit)
        {
            PostponeCounterText = $"Limite de reports atteinte (0/{state.MaxCount})";
            PostponeCounterBrush = CreateBrush(0xEF, 0x53, 0x50);
            PostponeButtonText = "Limite atteinte";
            IsPostponeEnabled = false;
            return;
        }

        int remaining = state.RemainingCount;
        PostponeCounterText = $"Reports restants : {remaining}/{state.MaxCount}";
        PostponeButtonText = "Plus tard";
        IsPostponeEnabled = true;

        PostponeCounterBrush = remaining switch
        {
            <= 1 => CreateBrush(0xEF, 0x53, 0x50),
            <= 2 => CreateBrush(0xFF, 0xB7, 0x4D),
            _ => CreateBrush(0x66, 0xBB, 0x6A)
        };
    }

    private bool HasReachedPostponeLimit()
    {
        return _hasLoadedCounterState && _currentPostponeCount >= PostponeCounterService.MaxPostpones;
    }

    private void RefreshPinValidationState()
    {
        PinValidationMessage = string.Empty;
        PinConfirmationValidationMessage = string.Empty;

        if (string.IsNullOrEmpty(_pinValue) && string.IsNullOrEmpty(_pinConfirmationValue))
        {
            PinBorderBrush = CreateBrush(0xB5, 0xC0, 0xD0, 0xC8);
            PinConfirmationBorderBrush = CreateBrush(0xB5, 0xC0, 0xD0, 0xC8);
            RecalculateCanValidate();
            return;
        }

        (bool isPinValid, string pinValidationMessage) = ValidatePinValue(_pinValue);
        (bool isConfirmationValid, string confirmationValidationMessage) = ValidatePinValue(_pinConfirmationValue);

        if (!string.IsNullOrEmpty(_pinValue) && !isPinValid)
        {
            PinValidationMessage = pinValidationMessage;
        }

        if (!string.IsNullOrEmpty(_pinConfirmationValue) && !isConfirmationValid)
        {
            PinConfirmationValidationMessage = confirmationValidationMessage;
        }

        PinBorderBrush = ResolveInputBrush(_pinValue, isPinValid, _pinConfirmationValue, isConfirmationValid);
        PinConfirmationBorderBrush = ResolveInputBrush(_pinConfirmationValue, isConfirmationValid, _pinValue, isPinValid);

        RecalculateCanValidate();
    }

    private void RecalculateCanValidate()
    {
        (bool isPinValid, _) = ValidatePinValue(_pinValue);
        (bool isConfirmationValid, _) = ValidatePinValue(_pinConfirmationValue);

        CanValidate =
            !IsInitializing &&
            !IsProvisioning &&
            !IsProgressVisible &&
            !string.IsNullOrEmpty(_pinValue) &&
            !string.IsNullOrEmpty(_pinConfirmationValue) &&
            isPinValid &&
            isConfirmationValid &&
            string.Equals(_pinValue, _pinConfirmationValue, StringComparison.Ordinal);
    }

    private static SolidColorBrush ResolveInputBrush(string currentValue, bool isCurrentValid, string otherValue, bool isOtherValid)
    {
        if (string.IsNullOrEmpty(currentValue))
        {
            return CreateBrush(0xB5, 0xC0, 0xD0, 0xC8);
        }

        if (!isCurrentValid)
        {
            return CreateBrush(0xFF, 0xB7, 0x4D);
        }

        if (!string.IsNullOrEmpty(otherValue) && isOtherValid && string.Equals(currentValue, otherValue, StringComparison.Ordinal))
        {
            return CreateBrush(0x66, 0xBB, 0x6A);
        }

        if (!string.IsNullOrEmpty(otherValue) && !string.Equals(currentValue, otherValue, StringComparison.Ordinal))
        {
            return CreateBrush(0xEF, 0x53, 0x50);
        }

        return CreateBrush(0xB5, 0xC0, 0xD0, 0xC8);
    }

    private static (bool IsValid, string Message) ValidatePinValue(string pinValue)
    {
        if (string.IsNullOrEmpty(pinValue) || pinValue.Length < 6 || pinValue.Length > 20 || !IsDigitsOnly(pinValue))
        {
            return (false, "PIN invalide : 6 à 20 chiffres requis.");
        }

        if (IsStrictAscendingSequence(pinValue))
        {
            return (false, "PIN invalide : les chiffres ne doivent pas être en ordre croissant (ex : 123456).");
        }

        if (IsStrictDescendingSequence(pinValue))
        {
            return (false, "PIN invalide : les chiffres ne doivent pas être en ordre décroissant (ex : 654321).");
        }

        return (true, "OK");
    }

    private static bool IsDigitsOnly(string value)
    {
        foreach (char character in value)
        {
            if (!char.IsDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsStrictAscendingSequence(string pinValue)
    {
        for (int index = 0; index < pinValue.Length - 1; index++)
        {
            int current = pinValue[index] - '0';
            int next = pinValue[index + 1] - '0';

            if (next != current + 1)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsStrictDescendingSequence(string pinValue)
    {
        for (int index = 0; index < pinValue.Length - 1; index++)
        {
            int current = pinValue[index] - '0';
            int next = pinValue[index + 1] - '0';

            if (next != current - 1)
            {
                return false;
            }
        }

        return true;
    }

    private void AddProgressStep(ProgressStepKind kind, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        ProgressSteps.Add(new ProgressStepItem(kind, message));
    }

    private static SolidColorBrush CreateBrush(byte red, byte green, byte blue, byte alpha = 0xFF)
    {
        return new SolidColorBrush(ColorHelper.FromArgb(alpha, red, green, blue));
    }
}
