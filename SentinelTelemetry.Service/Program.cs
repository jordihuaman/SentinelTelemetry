using SentinelTelemetry.Core.Interfaces;
using SentinelTelemetry.Persistence;
using System.Windows.Forms;

namespace SentinelTelemetry.Service
{
    public class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);
            builder.Services.AddHostedService<Worker>();
            builder.Services.AddSingleton<PauseController>();

            builder.Services.AddSingleton<IEventRepository>(provider =>
            {
                var connectionString = builder.Configuration
                    .GetConnectionString("SentinelDb");

                return CreateRepositoryWithRetry(connectionString);
            });

            var host = builder.Build();
            host.Start();

            var pauseController = host.Services.GetRequiredService<PauseController>();

            ApplicationConfiguration.Initialize();
            using var trayIcon = new NotifyIcon
            {
                Icon = System.Drawing.SystemIcons.Application, // ícono temporal, después se puede reemplazar por uno propio (.ico)
                Text = "Sentinel Telemetry",
                Visible = true
            };

            var menu = new ContextMenuStrip();

            var pauseResumeItem = new ToolStripMenuItem("Pausar");
            pauseResumeItem.Click += (s, e) =>
            {
                if (pauseController.IsPaused)
                {
                    pauseController.Resume();
                    pauseResumeItem.Text = "Pausar";
                }
                else
                {
                    pauseController.Pause();
                    pauseResumeItem.Text = "Reanudar";
                }
            };

            var closeItem = new ToolStripMenuItem("Cerrar");
            closeItem.Click += async (s, e) =>
            {
                trayIcon.Visible = false;
                await host.StopAsync();
                Application.Exit();
            };

            menu.Items.Add(pauseResumeItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(closeItem);

            trayIcon.ContextMenuStrip = menu;

            Application.Run();

        }
        private static SqlServerEventRepository CreateRepositoryWithRetry(string connectionString)
        {
            const int maxAttempts = 10;
            const int delaySeconds = 3;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    return new SqlServerEventRepository(connectionString);
                }
                catch (Exception) when (attempt < maxAttempts)
                {
                    Thread.Sleep(TimeSpan.FromSeconds(delaySeconds));
                }
            }

            // Último intento sin atrapar: si sigue fallando después de ~30s, ahí sí queremos ver el error real
            return new SqlServerEventRepository(connectionString);
        }
    }
}
