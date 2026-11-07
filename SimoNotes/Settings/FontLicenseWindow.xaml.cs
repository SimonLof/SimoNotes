using System.IO;
using System.Windows;

namespace SimoNotes.Settings;

public partial class FontLicenseWindow : Window
{
    internal FontLicenseWindow()
    {
        InitializeComponent();
        try
        {
            using var licenseStream = typeof(FontLicenseWindow).Assembly.GetManifestResourceStream(
                "SimoNotes.Assets.PressStart2P-OFL.txt");
            if (licenseStream is null)
            {
                LicenseText.Text = "The bundled font license file could not be read.";
                return;
            }

            using var reader = new StreamReader(licenseStream);
            LicenseText.Text = reader.ReadToEnd();
        }
        catch (IOException)
        {
            LicenseText.Text = "The bundled font license file could not be read.";
        }
        catch (UnauthorizedAccessException)
        {
            LicenseText.Text = "The bundled font license file could not be read.";
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}