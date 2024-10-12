using Avalonia;
using Avalonia.Controls;
using Avalonia.DesignerSupport;
using Avalonia.Logging;
using Avalonia.Threading;
using MsBox.Avalonia;
using SharpCompress.Archives;
using SharpCompress.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection.Metadata.Ecma335;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ArqitekLauncher.Views
{
	public partial class MainWindow : Window
	{
		public enum Installed
		{
			Installed,
			Nonexistent,
			Outdated
		}
		public enum Games
		{
			pbb
		}

		CancellationTokenSource cancel;

		public MainWindow()
		{
			InitializeComponent();

			using (var check = checkinstall())
			{
				check.Wait();
				switch (check.Result)
				{
					case Installed.Installed: { playtext.Text = "Play"; break; }
					case Installed.Nonexistent: { playtext.Text = "Install"; break; }
					case Installed.Outdated: { playtext.Text = "Update"; break; }
					default: break;
				}
			}

			cancel = new();

			Play.Click += Play_Click;
			
		}

		private void Play_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
		{
			HandlePlay();
		}

		Installed installed;
		string installpath = "";

		public static string GetAppDataPath()
		{
			string appDataPath;

			if (OperatingSystem.IsWindows())
			{
				appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData); // %APPDATA%
			}
			else if (OperatingSystem.IsMacOS())
			{
				appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support");
			}
			else if (OperatingSystem.IsLinux())
			{
				appDataPath = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ??
							  Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
			}
			else
			{
				throw new PlatformNotSupportedException("Operating system not supported");
			}

			// Optionally append your application name or folder
			return Path.Combine(appDataPath, "aqtklnch");
		}

		async Task<Installed> checkinstall()
		{
			var appdata = GetAppDataPath();

			if (!File.Exists(appdata + @"\DATA\Paths"))
			{
				Directory.CreateDirectory(appdata + @"\DATA");
				File.Create(appdata + @"\DATA\Paths").Close();
				File.WriteAllText(appdata + @"\DATA\Paths", "{}");
				
				installed = Installed.Nonexistent;
				return installed;
			}

			Dictionary<int, string> installtable;
			using(var file = File.OpenRead(appdata + @"\DATA\Paths"))
			installtable = await JsonSerializer.DeserializeAsync<Dictionary<int, string>>(file).ConfigureAwait(false) ?? throw new NullReferenceException("INSTALLTABLE NULL");

			if (installtable.ContainsKey((int)Games.pbb))
			{
				if (File.Exists(installtable[(int)Games.pbb]+@"\ProjectBadBot\version.txt"))
				{
					installpath = installtable[(int)Games.pbb];

					if (!await CompareStringWithFileContent(File.ReadAllText(installtable[(int)Games.pbb] + @"\ProjectBadBot\version.txt")))
					{
						installed = Installed.Outdated;
						return installed;
					}

					installed = Installed.Installed;
					return installed;

				}
			}

			installed = Installed.Nonexistent;
			return installed;
		}

		void HandlePlay()
		{
			// Use async void carefully; it's fine here for UI event handlers
			Task.Run(async () =>
			{
				try
				{
					var appdata = GetAppDataPath();
					switch (installed)
					{
						case Installed.Nonexistent:
							await Download(true).ConfigureAwait(false);
							break;

						case Installed.Installed:
							LaunchGame();
							break;

						case Installed.Outdated:
							await Download(false).ConfigureAwait(false);
							break;

						default:
							break;
					}
				}
				catch (Exception ex)
				{
					// Handle exceptions from the Download method
					var msg = await Dispatcher.UIThread.InvokeAsync(() => MessageBoxManager.GetMessageBoxStandard("ERROR", $"An error occurred: {ex.Message}", MsBox.Avalonia.Enums.ButtonEnum.Ok));
					await Dispatcher.UIThread.InvokeAsync(msg.ShowAsync);
					Dispatcher.UIThread.Post(Close);
					cancel.Cancel();
				}
			}).ContinueWith((t) =>
			{
                if (cancel.IsCancellationRequested)
                {
                    Dispatcher.UIThread.Post(Close);
                    Dispatcher.UIThread.Post(() => ((App?)Application.Current)?.desktop?.Shutdown());
                }
            });
		}

		public async Task UpdateInstallPath(string path)
		{
			var appdata = GetAppDataPath();
			string apath;
            if (path.StartsWith("file:///"))
            {
                apath = path.Substring(8);  // Remove "file:///"
                apath = apath.Replace('/', Path.DirectorySeparatorChar); // Normalize slashes
            }
			else
			{
				apath = path;
			}
            if (!File.Exists(appdata + @"\DATA\Paths"))
			{
				throw new Exception("NO PATHS FILE");
			}
			var fs = File.OpenWrite(appdata + @"\DATA\Paths");
			await JsonSerializer.SerializeAsync(fs, new Dictionary<int, string>([new((int)Games.pbb, apath)]));
			fs.Close();

		}

		public async Task Download(bool pick)
		{
			string url = string.Empty;

			// Detect the operating system and set the URL accordingly
			if (OperatingSystem.IsWindows())
			{
				url = "https://drive.usercontent.google.com/download?id=1Uo92Y6dSTVkQQf1d92PL4r_iFEO6RQ_s&export=download&authuser=0&confirm=t&uuid=79095acd-64b4-4207-a6f1-c6cca608575d&at=AN_67v2iWXOB3qiP2yjDM554zEoQ:1728593901170";  // URL for Windows version
			}
			else if (OperatingSystem.IsLinux())
			{
				if (IsLinuxArm64()) // Custom method to detect Linux Arm64
				{
					url = "https://drive.usercontent.google.com/download?id=1gynWaQX8wajyPtflNwLTHDXZmOaqtUpB&export=download&authuser=0&confirm=t&uuid=36fcf614-9789-476a-92c1-9a4849f7a40c&at=AN_67v19U7qgUDOl5ia7osf9SUx8:1728593998379";  // URL for Linux Arm64 version
				}
				else
				{
					url = "https://drive.usercontent.google.com/download?id=1JOZoTHNre3a8Yrpd_RfUOnfcQcyx-3TN&export=download&authuser=0&confirm=t&uuid=85d06c5f-ee34-4ae3-afe3-8f1a69a952ca&at=AN_67v2jcrlrPglyeSs6LFV_g6Yh:1728593956851";  // URL for Linux version
				}
			}
			else
			{
				throw new PlatformNotSupportedException("Operating system not supported.");
			}

			string zipPath = Path.Combine(Path.GetTempPath(), "downloaded.7z");

			// Step 2: Extract files (without parent folder) to user-chosen directory
			string targetPath;

			if (pick)
			{
				var atargetPath = await StorageProvider.OpenFolderPickerAsync(new() { AllowMultiple = false, Title = "Install Location" }).ConfigureAwait(false);
				targetPath = atargetPath[0].Path.ToString();

				// Update the installation path for pbb in the JSON file
				await UpdateInstallPath(targetPath); // New method to update the path in JSON
			}
			else
			{
				targetPath = installpath;
			}

			var progressWindow = await Dispatcher.UIThread.InvokeAsync<ProgressWindow>(() => { return new(this); });

			progressWindow.Closed += ProgressBai;

			Dispatcher.UIThread.Post(() => progressWindow.ShowDialog(this));

			using (HttpClient client = new HttpClient())
			using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
			{
				response.EnsureSuccessStatusCode();

				var totalBytes = response.Content.Headers.ContentLength.GetValueOrDefault();

				using var stream = await response.Content.ReadAsStreamAsync();
				using var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None);
				{
					var buffer = new byte[8192];
					long totalRead = 0;
					int read;

					Dispatcher.UIThread.Post(() => progressWindow.Text = "Downloading, Please Wait...");

					while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
					{
						if (cancel.IsCancellationRequested) { fileStream.Close(); stream.Close(); response.Dispose(); client.Dispose(); File.Delete(zipPath); return; };
						await fileStream.WriteAsync(buffer.AsMemory(0, read));
						totalRead += read;

						// Calculate progress percentage
						var progressPercentage = (int)((totalRead * 100) / totalBytes);

						Dispatcher.UIThread.Post(() => progressWindow.UpdateProgress(progressPercentage));
						
					}
				}
			}

			if (cancel.IsCancellationRequested) { File.Delete(zipPath); return; };

			Dispatcher.UIThread.Post(() => progressWindow.Text = "Extracting, Please Wait...");
			Dispatcher.UIThread.Post(() => progressWindow.UpdateProgress(0));

			if (!string.IsNullOrWhiteSpace(targetPath))
			{
				using (var archive = ArchiveFactory.Open(zipPath))
				{
					// Find the correct folder prefix to extract (either Windows, Linux, or LinuxArm64)
					var folderPrefix = archive.Entries
						.Select(entry => entry.Key!.Split('/').FirstOrDefault())
						.FirstOrDefault(prefix => prefix == "Windows" || prefix == "Linux" || prefix == "LinuxArm64");

					if (!string.IsNullOrEmpty(folderPrefix))
					{
						// Calculate total size of the files to be extracted
						long totalExtractSize = archive.Entries
							.Where(entry => !entry.IsDirectory && entry.Key!.StartsWith($"{folderPrefix}/"))
							.Sum(entry => entry.Size);

						long totalExtractedBytes = 0;

						if (targetPath.StartsWith("file:///"))
						{
							targetPath = targetPath.Substring(8);  // Remove "file:///"
							targetPath = targetPath.Replace('/', Path.DirectorySeparatorChar); // Normalize slashes
						}

						foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory && entry.Key!.StartsWith($"{folderPrefix}/")))
						{
							if (cancel.IsCancellationRequested) { archive.Dispose(); File.Delete(zipPath); return; };

							// Ensure the correct relative path is extracted by removing the folder prefix
							string relativePath = entry.Key!.Substring(entry.Key.IndexOf('/') + 1);

							// Ensure the destinationPath is constructed correctly with proper slashes
							string destinationPath = Path.Combine(targetPath, relativePath.Replace("/", Path.DirectorySeparatorChar.ToString()));

							if (string.IsNullOrWhiteSpace(relativePath)) continue; // Skip if it's just the folder

							// Create directory if it doesn't exist
							Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

							// Extract the file
							entry.WriteToFile(destinationPath, new ExtractionOptions { Overwrite = true });

							totalExtractedBytes += entry.Size;

							// Update the progress bar with extraction progress
							var extractionProgressPercentage = (int)((totalExtractedBytes * 100) / totalExtractSize);
							Dispatcher.UIThread.Post(() => progressWindow.UpdateProgress(extractionProgressPercentage));
						}
					}
				}
			}
            if (cancel.IsCancellationRequested) { File.Delete(zipPath); return; };

            // Step 3: Clean up by deleting the ZIP file
            File.Delete(zipPath);

            progressWindow.Closed -= ProgressBai;

            Dispatcher.UIThread.Post(() =>
			{
				playtext.Text = "Play"; // Update the UI after closing
				progressWindow.Close();
			});

			installed = Installed.Installed;

		}

		// Custom method to detect Linux Arm64
		public bool IsLinuxArm64()
		{
			// You can use `RuntimeInformation` to check for the architecture
			return System.Runtime.InteropServices.RuntimeInformation.OSArchitecture == System.Runtime.InteropServices.Architecture.Arm64;
		}

		private void ProgressBai(object? sender, EventArgs e)
        {
            cancel.Cancel(); Close();
        }


        public async Task<bool> CompareStringWithFileContent(string comparisonString)
		{
			using (HttpClient client = new HttpClient())
			{

				// Replace with your file URL
				string url = "https://drive.google.com/uc?id=1-jyftWNsLBpx_P45CKYK7uuUX5qZIkp_";
				string fileContent = await client.GetStringAsync(url);

				// Compare the file content with the provided string
				return string.Equals(fileContent.Trim(), comparisonString.Trim(), StringComparison.OrdinalIgnoreCase);

			}
		}

		public void LaunchGame()
		{
			
			Dispatcher.UIThread.Post(() => WindowState = WindowState.Minimized);

			ProcessStartInfo startInfo = new ProcessStartInfo
			{
				FileName = installpath+@"\ProjectBadBot.exe",
				UseShellExecute = false,
				WorkingDirectory = installpath
			};

			Process gameProcess = Process.Start(startInfo) ?? throw new Exception("GAME NOT RUN");

			Task.Run(() =>
			{
				gameProcess.WaitForExit();

				Dispatcher.UIThread.InvokeAsync(() =>
				{
					WindowState = WindowState.Normal;
					Activate();
				});
			});
		}
	}
}