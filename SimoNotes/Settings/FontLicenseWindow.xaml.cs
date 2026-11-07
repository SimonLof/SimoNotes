using System.IO;
using System.Windows;

namespace SimoNotes.Settings;

public partial class FontLicenseWindow : Window
{
    internal FontLicenseWindow()
    {
        InitializeComponent();
        var licensePath = Path.Combine(AppContext.BaseDirectory, "Assets", "PressStart2P-OFL.txt");

        try
        {
            LicenseText.Text = File.ReadAllText(licensePath);
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