using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using MsBox.Avalonia;
using SharpCompress.Archives;
using SharpCompress.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
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

			var check = checkinstall();
			
			switch (check)
			{
				case Installed.Installed: { playtext.Text = "Play"; break; }
				case Installed.Nonexistent: { playtext.Text = "Install"; break; }
				case Installed.Outdated: { playtext.Text = "Update"; break; }
				default: break;
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

		Installed checkinstall()
		{
			var appdata = GetAppDataPath();

			var data = Path.Combine(GetAppDataPath(), "DATA");
			var paths = Path.Combine(data, "Paths");

			if (!File.Exists(paths))
			{
				Directory.CreateDirectory(data);
				File.Create(paths).Close();
				File.WriteAllText(paths, "{}");

				installed = Installed.Nonexistent;
				return installed;
			}

			Dictionary<int, string> installtable;
			using (var file = File.OpenRead(paths))
				installtable = JsonSerializer.Deserialize<Dictionary<int, string>>(file) ?? throw new NullReferenceException("INSTALLTABLE NULL");

			if (installtable.ContainsKey((int)Games.pbb))
			{
				var version = Path.Combine(installtable[(int)Games.pbb], @"ProjectBadBot\version.txt".Replace('\\', Path.DirectorySeparatorChar));

				if (File.Exists(version))
				{
					installpath = installtable[(int)Games.pbb];

					if (CompareStringWithFileContent(File.ReadAllText(version)))
					{
						installed = Installed.Installed;
						return installed;
					}

					installed = Installed.Outdated;
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
			string pathsfile;

			if (path.StartsWith("file:///"))
			{
				apath = path.Substring(OperatingSystem.IsWindows() ? 8 : 7);  // Remove "file:///"
				apath = apath.Replace('/', Path.DirectorySeparatorChar); // Normalize slashes
			}
			else
			{
				apath = path;
			}

			pathsfile = Path.Combine(appdata, @"DATA\Paths".Replace('\\', Path.DirectorySeparatorChar));

			if (!File.Exists(pathsfile))
			{
				throw new Exception("NO PATHS FILE");
			}
			var fs = File.OpenWrite(pathsfile);
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
					url = "https://drive.usercontent.google.com/download?id=1JOZoTHNre3a8Yrpd_RfUOnfcQcyx-3TN&export=download&authuser=0&confirm=t&uuid=85d06c5f-ee34-4ae3-afe3-8f1a69a952ca&at=AN_67v2jcrlrPglyeSs6LFV_g6Yh:1728593956851";  // URL for Linux version
				}
				else
				{
					url = "https://drive.usercontent.google.com/download?id=1gynWaQX8wajyPtflNwLTHDXZmOaqtUpB&export=download&authuser=0&confirm=t&uuid=36fcf614-9789-476a-92c1-9a4849f7a40c&at=AN_67v19U7qgUDOl5ia7osf9SUx8:1728593998379";  // URL for Linux Arm64 version
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
					// Calculate total size of the files to be extracted
					long totalExtractSize = archive.TotalUncompressSize;

					long totalExtractedBytes = 0;

					if (targetPath.StartsWith("file:///"))
					{
						targetPath = targetPath.Substring(OperatingSystem.IsWindows() ? 8 : 7);  // Remove "file:///"
						targetPath = targetPath.Replace('/', Path.DirectorySeparatorChar); // Normalize slashes
					}

					foreach (var entry in archive.Entries)
					{
						if (cancel.IsCancellationRequested) { archive.Dispose(); File.Delete(zipPath); return; }

						// Keep the full path, preserving the original folder structure
						string relativePath = entry.Key.Replace("/", Path.DirectorySeparatorChar.ToString());

						// Build the destination path with the original folder structure
						string destinationPath = Path.Combine(targetPath, relativePath);

						if (string.IsNullOrWhiteSpace(relativePath) || entry.IsDirectory) continue; // Skip if it's a folder

						// Create the directory structure if it doesn't exist
						Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

						// Extract the file
						entry.WriteToFile(destinationPath, new ExtractionOptions { Overwrite = true });

						totalExtractedBytes += entry.Size;

						// Update progress based on extracted size
						double extractionProgressPercentage = (totalExtractedBytes * 100f) / totalExtractSize;
						Dispatcher.UIThread.Post(() => progressWindow.UpdateProgress(extractionProgressPercentage));
					}
				}
			}
			if (cancel.IsCancellationRequested) { File.Delete(zipPath); return; };

			// Step 3: Clean up by deleting the ZIP file
			File.Delete(zipPath);

			progressWindow.Closed -= ProgressBai;

			var check = checkinstall();

			string text = "";

			switch (check)
			{
				case Installed.Installed: { text = "Play"; break; }
				case Installed.Nonexistent: { text = "Install"; break; }
				case Installed.Outdated: { text = "Update"; break; }
				default: break;
			}

			Dispatcher.UIThread.Post(() =>
			{
				playtext.Text = text; // Update the UI after closing
				progressWindow.Close();
			});

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


		public bool CompareStringWithFileContent(string comparisonString)
		{
			using (HttpClient client = new HttpClient())
			{

				// Replace with your file URL
				string url = "https://drive.google.com/uc?id=1-jyftWNsLBpx_P45CKYK7uuUX5qZIkp_";
				var fileContentreq = client.GetStringAsync(url);
				fileContentreq.Wait();
				var fileContent = fileContentreq.Result;

				// Compare the file content with the provided string
				return string.Equals(fileContent.Trim(), comparisonString.Trim(), StringComparison.OrdinalIgnoreCase);

			}
		}

		public void LaunchGame()
		{
			Dispatcher.UIThread.Post(() => WindowState = WindowState.Minimized);

			string executableName = "ProjectBadBot"; // Default for Linux
			if (OperatingSystem.IsWindows())
			{
				executableName += ".exe"; // Add .exe for Windows
			}

			if (OperatingSystem.IsLinux())
			{
				if(IsLinuxArm64())
				{
					installpath = Path.Combine(installpath, "ProjectBadBot/Binaries/LinuxArm64/");
					executableName = "ProjectBadBot-LinuxArm64-Shipping";
				}
				else
				{
					installpath = Path.Combine(installpath, "ProjectBadBot/Binaries/Linux/");
					executableName = "ProjectBadBot-Linux-Shipping";
				}

				ProcessStartInfo chmod = new ProcessStartInfo("chmod")
				{
					Arguments = $"+x \"{Path.Combine(installpath, executableName)}\"",
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true
				};
				Process proca = Process.Start(chmod) ?? throw new Exception();
				proca.WaitForExit();
			}

			ProcessStartInfo psi = new ProcessStartInfo(Path.Combine(installpath, executableName))
			{
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true
			};

			Process process = Process.Start(psi) ?? throw new Exception();
			process.WaitForExit();


			Dispatcher.UIThread.Post(() =>
			{
				WindowState = WindowState.Normal;
				Activate();
			});
		}
	}
}