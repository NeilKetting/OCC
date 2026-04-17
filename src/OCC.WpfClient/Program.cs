using System;
using Velopack;

namespace OCC.WpfClient
{
    public class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                // VelopackApp should be the first thing to run to handle update events
                VelopackApp.Build().Run();

                // Start the WPF application
                var app = new App();
                app.InitializeComponent();
                app.Run();
            }
            catch (Exception ex)
            {
                // Standard error handling for startup failures
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
