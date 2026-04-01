using System;
using System.Globalization;
using System.Text;
using Microsoft.UI.Xaml;

namespace BitLockerUFCV.Client
{
    public partial class App : Application
    {
        private Window? _window;

        public App()
        {
            InitializeComponent();
            ApplyFrenchCulture();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();
            _window.Activate();
        }

        private static void ApplyFrenchCulture()
        {
            CultureInfo culture = CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        }
    }
}
