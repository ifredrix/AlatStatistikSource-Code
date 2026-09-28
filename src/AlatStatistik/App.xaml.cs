using System;
using System.IO;
using System.Text;
using System.Windows;

namespace AlatStatistik;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        TulisJejak("aplikasi mulai");

        
        
        
        
        DispatcherUnhandledException += (_, arg) =>
        {
            TulisLog(arg.Exception);
            MessageBox.Show(
                "Terjadi galat yang tidak terduga:\n\n"
                + arg.Exception.Message
                + "\n\nRincian lengkapnya ditulis ke:\n" + BerkasLog()
                + "\n\nAplikasi akan dicoba dilanjutkan. Kalau perilakunya "
                + "mulai aneh, tutup dan buka kembali.",
                "Galat tak terduga", MessageBoxButton.OK, MessageBoxImage.Error);
            arg.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, arg) =>
        {
            if (arg.ExceptionObject is Exception ex) TulisLog(ex);
        };
    }

    internal static void TulisLog(Exception ex) => TulisLogInti(ex);

    internal static void TulisJejak(string langkah)
    {
        try
        {
            string berkas = Path.Combine(Path.GetDirectoryName(BerkasLog())!, "jejak.log");
            Directory.CreateDirectory(Path.GetDirectoryName(berkas)!);
            File.AppendAllText(berkas,
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + langkah + "\n");
        }
        catch
        {
            
        }
    }

    internal static string BerkasLog()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "AlatStatistik", "galat.log");

    private static void TulisLogInti(Exception ex)
    {
        try
        {
            string berkas = BerkasLog();
            Directory.CreateDirectory(Path.GetDirectoryName(berkas)!);

            var sb = new StringBuilder();
            sb.AppendLine("=== " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===");
            sb.AppendLine(ex.GetType().FullName);
            sb.AppendLine(ex.Message);
            sb.AppendLine(ex.StackTrace);
            if (ex.InnerException != null)
            {
                sb.AppendLine("--- penyebab dalam ---");
                sb.AppendLine(ex.InnerException.ToString());
            }
            sb.AppendLine();

            File.AppendAllText(berkas, sb.ToString());
        }
        catch
        {
            
        }
    }
}
