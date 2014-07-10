using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;

namespace ipa2deb
{
	class MainClass
	{
		static string bundleDir;
		static string workingDir;
		static string package;
		static string description;
		static string version;
		static string author;
		static string mainteiner;
		static string name;

		static string ShellExecute (string command,string arguments)
		{
			Console.WriteLine (command + " " + arguments);

			using (Process process = new Process ()) {
				process.StartInfo.UseShellExecute = false;
				process.StartInfo.RedirectStandardOutput = true;
				process.StartInfo.Arguments = arguments;
				process.StartInfo.FileName = command;

				process.Start ();
				process.WaitForExit ();

				return process.StandardOutput.ReadToEnd ();
			}
		}

		static string GetTemporaryDirectory ()
		{
			string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
			Directory.CreateDirectory (tempDirectory);
			return tempDirectory;
		}

		static string ExtractIpaFile (string ipa, string path)
		{
			ShellExecute ("unzip", string.Format ("\"{0}\" -d \"{1}\"", ipa, path));

			List<string> dirs = new List<string>(Directory.EnumerateDirectories (Path.Combine (path, "Payload")));
			return dirs [0];
		}

		static void WriteFiles (string path)
		{
			WriteControlFile (path);
			WriteMakefile (path);
		}

		static void WriteControlFile (string path)
		{
			string makefile = Path.Combine (path, "control");

			var builder = new StringBuilder ();
			builder.AppendFormat ("Package: {0}\n", package);
			builder.AppendFormat ("Name: {0}\n", name);
			builder.AppendFormat ("Version: {0}\n", version);
			builder.Append ("Architecture: iphoneos-arm\n");
			builder.AppendFormat ("Description: {0}\n", description);
			builder.AppendFormat ("Maintainer: {0}\n", mainteiner);
			builder.AppendFormat ("Author: {0}\n", author);
			builder.Append ("Section: Utilities\n");

			File.WriteAllText (makefile, builder.ToString ());
		}

		static void  WriteMakefile(string path)
		{
			string makefile = Path.Combine (path, "Makefile");

			var builder = new StringBuilder ();
			builder.Append ("include theos/makefiles/common.mk\n");
			builder.Append ("\n");
			builder.AppendFormat ("APPLICATION_NAME = {0}\n", name);
			builder.AppendFormat ("{0}_FILES = \n", name);
			builder.AppendFormat ("{0}_FRAMEWORKS = UIKit CoreGraphics \n", name);
			builder.Append ("\n");
			builder.Append ("include $(THEOS_MAKE_PATH)/application.mk");
			builder.Append ("\n");
			File.WriteAllText (makefile, builder.ToString ());
		}

		static void CreateTheosLink (string path)
		{
			ShellExecute ("ln"," -s " + Environment.GetEnvironmentVariable ("THEOS") + " " + Path.Combine(path, "theos"));
		}

		static void CopyAppBundle (string path)
		{
			string obj = Path.Combine (path, "obj");
			Directory.CreateDirectory (obj);

			string bundlePath = Path.Combine (workingDir, "Payload", name + ".app");
			string destPath = obj;

			Directory.CreateDirectory (destPath);

			ShellExecute ("cp"," -R \"" + bundlePath + "\" \"" + destPath + "\"");  
		}

		static string CreateDeb ()
		{
			string path = Path.Combine (workingDir, name);
			Directory.CreateDirectory (path);

			ShellExecute ("cd", path);
			Directory.SetCurrentDirectory (path);

			WriteFiles (path);
			CreateTheosLink (path);
			CopyAppBundle (path);
			ShellExecute ("make","package");

			List<string> dirs = new List<string>(Directory.EnumerateFiles (path, "*.deb"));
			return dirs [0];
		}

		static string GetCurrentUserName ()
		{
			var output = ShellExecute ("finger", Environment.UserName );
			output = output.Split ('\n') [0];
			int pos = output.IndexOf ("Name: ");
			return output.Substring (pos + 6);
		}

		static string ReadName ()
		{
			string infoPath = Path.Combine (bundleDir, "Info.plist");
			return ShellExecute ("/usr/libexec/PlistBuddy", " -c \"Print :CFBundleDisplayName\" " + infoPath);
		}

		static string ReadPackage ()
		{
			string infoPath = Path.Combine (bundleDir, "Info.plist");
			return ShellExecute ("/usr/libexec/PlistBuddy", " -c \"Print :CFBundleIdentifier\" " + infoPath);
		}

		static string ReadVersion ()
		{
			string infoPath = Path.Combine (bundleDir, "Info.plist");
			return ShellExecute ("/usr/libexec/PlistBuddy", " -c \"Print :CFBundleVersion\" " + infoPath);
		}

		static void ReadBundleInfo ()
		{
			name = ReadName ().Trim ();
			package = ReadPackage ().Trim ();
			version = ReadVersion ().Trim ();
		}

		public static int Main (string[] args)
		{
			if (args.Length < 2) {
				Console.Error.WriteLine ("Usage: ipa2deb ipafile description [author] [maintainer]");
				return 1;
			}
			string currentDir = Environment.CurrentDirectory;
			workingDir = GetTemporaryDirectory ();

			try {

				Directory.SetCurrentDirectory (workingDir);

				string ipaFile = args [0];
				description = args [1];
				if (args.Length > 2)
					author = args [2];
				else
					author = GetCurrentUserName ();

				if (args.Length > 3)
					mainteiner = args [3];
				else
					mainteiner = author;

				bundleDir = ExtractIpaFile (ipaFile, workingDir);
				ReadBundleInfo ();
				string debName = CreateDeb ();

				string outputFile = Path.Combine (currentDir, Path.GetFileNameWithoutExtension (ipaFile) + ".deb");
				ShellExecute ("cp",debName + " " + outputFile);
			}
			finally {
				ShellExecute ("rm","-rf " + workingDir);
			}
			return 0;
		}
	}
}
