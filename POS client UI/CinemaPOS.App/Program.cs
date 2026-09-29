using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Extensions.Configuration;
using Serilog;
using CinemaPOS.App.Forms;
using CinemaPOS.Core.Database;

namespace CinemaPOS.App;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        var basePath = AppContext.BaseDirectory;
        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();

        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.File(Path.Combine(basePath, "logs", "cinemapos.txt"), rollingInterval: RollingInterval.Day)
            .CreateLogger();

        try
        {
            Log.Information("Starting Cinema POS");

            string connectionString = config.GetConnectionString("LocalDb");
            if (!string.IsNullOrEmpty(connectionString) && (args == null || (!args.Contains("--screenshot") && !args.Contains("--main-screenshot") && !args.Contains("--main-max-screenshot") && !args.Contains("--settings-screenshot") && !args.Contains("--fnb-screenshot") && !args.Contains("--seat-screenshot"))))
            {
                try
                {
                    var dbInitializer = new DatabaseInitializer(connectionString);
                    dbInitializer.Initialize();
                    Log.Information("Database initialized successfully.");
                }
                catch (Exception dbEx)
                {
                    Log.Warning(dbEx, "Database init skipped/failed.");
                }
            }

            if (args != null && args.Contains("--screenshot"))
            {
                var form = new LoginForm(config);
                form.Show();
                for (int i = 0; i < 20; i++)
                {
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(100);
                }
                using var bmp = new System.Drawing.Bitmap(form.Width, form.Height);
                form.DrawToBitmap(bmp, new System.Drawing.Rectangle(0, 0, form.Width, form.Height));
                bmp.Save(Path.Combine(basePath, "login_screenshot.png"), System.Drawing.Imaging.ImageFormat.Png);
                Console.WriteLine("SCREENSHOT_SAVED");
                return;
            }

            if (args != null && args.Contains("--main-screenshot"))
            {
                var form = new MainPosForm();
                form.WindowState = FormWindowState.Normal;
                form.Size = new System.Drawing.Size(1440, 900);
                form.StartPosition = FormStartPosition.CenterScreen;
                form.Show();
                for (int i = 0; i < 40; i++)
                {
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(100);
                }

                using var bmp = new System.Drawing.Bitmap(form.Width, form.Height);
                bool captured = false;
                try
                {
                    using var g = System.Drawing.Graphics.FromImage(bmp);
                    g.CopyFromScreen(form.Location, System.Drawing.Point.Empty, form.Size);
                    captured = true;
                }
                catch
                {
                    captured = false;
                }

                if (!captured)
                {
                    form.DrawToBitmap(bmp, new System.Drawing.Rectangle(0, 0, form.Width, form.Height));
                }

                string outPath = Path.Combine(basePath, "main_screenshot.png");
                bmp.Save(outPath, System.Drawing.Imaging.ImageFormat.Png);
                File.WriteAllText(Path.Combine(basePath, "screenshot_done.txt"), outPath);
                Console.WriteLine("MAIN_SCREENSHOT_SAVED");
                return;
            }

            if (args != null && args.Contains("--fnb-screenshot"))
            {
                var form = new MainPosForm();
                form.WindowState = FormWindowState.Normal;
                form.Size = new System.Drawing.Size(1440, 900);
                form.StartPosition = FormStartPosition.CenterScreen;
                form.Show();
                for (int i = 0; i < 35; i++)
                {
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(100);
                }
                form.SwitchViewForTesting("F&B");
                for (int i = 0; i < 15; i++)
                {
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(100);
                }

                using var bmp = new System.Drawing.Bitmap(form.Width, form.Height);
                bool captured = false;
                try
                {
                    using var g = System.Drawing.Graphics.FromImage(bmp);
                    g.CopyFromScreen(form.Location, System.Drawing.Point.Empty, form.Size);
                    captured = true;
                }
                catch { captured = false; }

                if (!captured)
                {
                    form.DrawToBitmap(bmp, new System.Drawing.Rectangle(0, 0, form.Width, form.Height));
                }

                string outPath = Path.Combine(basePath, "fnb_screenshot.png");
                bmp.Save(outPath, System.Drawing.Imaging.ImageFormat.Png);
                Console.WriteLine("FNB_SCREENSHOT_SAVED");
                return;
            }

            if (args != null && args.Contains("--seat-screenshot"))
            {
                var form = new MainPosForm();
                form.WindowState = FormWindowState.Normal;
                form.Size = new System.Drawing.Size(1440, 900);
                form.StartPosition = FormStartPosition.CenterScreen;
                form.Show();
                for (int i = 0; i < 35; i++)
                {
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(100);
                }
                form.OpenFirstShowtimeSeatMapForTestingAsync().GetAwaiter().GetResult();
                form.SelectFirstAvailableSeatForTesting();
                for (int i = 0; i < 15; i++)
                {
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(100);
                }

                using var bmp = new System.Drawing.Bitmap(form.Width, form.Height);
                bool captured = false;
                try
                {
                    using var g = System.Drawing.Graphics.FromImage(bmp);
                    g.CopyFromScreen(form.Location, System.Drawing.Point.Empty, form.Size);
                    captured = true;
                }
                catch { captured = false; }

                if (!captured)
                {
                    form.DrawToBitmap(bmp, new System.Drawing.Rectangle(0, 0, form.Width, form.Height));
                }

                string outPath = Path.Combine(basePath, "seatmap_screenshot.png");
                bmp.Save(outPath, System.Drawing.Imaging.ImageFormat.Png);
                Console.WriteLine("SEAT_SCREENSHOT_SAVED");
                return;
            }

            if (args != null && args.Contains("--main-max-screenshot"))
            {
                var form = new MainPosForm();
                form.WindowState = FormWindowState.Maximized;
                form.Show();
                for (int i = 0; i < 40; i++)
                {
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(100);
                }

                using var bmp = new System.Drawing.Bitmap(form.Width, form.Height);
                bool captured = false;
                try
                {
                    using var g = System.Drawing.Graphics.FromImage(bmp);
                    g.CopyFromScreen(form.Location, System.Drawing.Point.Empty, form.Size);
                    captured = true;
                }
                catch
                {
                    captured = false;
                }

                if (!captured)
                {
                    form.DrawToBitmap(bmp, new System.Drawing.Rectangle(0, 0, form.Width, form.Height));
                }

                string outPath = Path.Combine(basePath, "main_max_screenshot.png");
                bmp.Save(outPath, System.Drawing.Imaging.ImageFormat.Png);
                Console.WriteLine("MAIN_MAX_SCREENSHOT_SAVED");
                return;
            }

            if (args != null && args.Contains("--settings-screenshot"))
            {
                var form = new TerminalSettingsForm();
                form.StartPosition = FormStartPosition.CenterScreen;
                form.Show();
                for (int i = 0; i < 20; i++)
                {
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(100);
                }

                using var bmp = new System.Drawing.Bitmap(form.Width, form.Height);
                bool captured = false;
                try
                {
                    using var g = System.Drawing.Graphics.FromImage(bmp);
                    g.CopyFromScreen(form.Location, System.Drawing.Point.Empty, form.Size);
                    captured = true;
                }
                catch
                {
                    captured = false;
                }

                if (!captured)
                {
                    form.DrawToBitmap(bmp, new System.Drawing.Rectangle(0, 0, form.Width, form.Height));
                }

                string outPath = Path.Combine(basePath, "settings_screenshot.png");
                bmp.Save(outPath, System.Drawing.Imaging.ImageFormat.Png);
                Console.WriteLine("SETTINGS_SCREENSHOT_SAVED");
                return;
            }

            Application.Run(new LoginForm(config));
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }    
}