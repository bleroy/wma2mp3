using System;
using System.Diagnostics;
using System.IO;

namespace wma2mp3
{
    class Program
    {
        const int deviation = 40; //%
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine($@"wma2mp3 usage:

wma2mp3 sourcepath targetpath

Wma2mp3 will scan sourcepath recursively, will copy all files but wma files, which will be transcoded to mp3.

wma2mp3 -c sourcepath targetpath

Specifying the 'c' flag will do a comparison in size of source wma files with their mp3 converted counterparts,
and will print out the path of any file that is smaller than its source by more than {deviation}% in size.
This is useful to do a sanity check on the results of a bulk conversion, as corrupted conversions will often
result in incomplete files.

wma2mp3 -d targetpath

Specifying the 'd' flag will scan the target path recursively for duplicate files, which are recognized as
""filename (1).mp3"" where ""filename.mp3"" exists.
");
                return;
            }
            var check = args[0].Equals("-c", StringComparison.InvariantCultureIgnoreCase);
            var removeDuplicates = args[0].Equals("-d", StringComparison.InvariantCultureIgnoreCase);

            var target = removeDuplicates ? args[1] : (check ? args[2] : args[1]);

            if (removeDuplicates)
            {
                Console.WriteLine($"Removing duplicates from {target}.");
                FindAndRemoveDuplicates(target);
            }
            else
            {
                var source = check ? args[1] : args[0];

                if (check)
                {
                    Console.WriteLine($"Checking relative sizes of all wma and mp3 from {source} to {target}.");
                    Check(source, target);
                }
                else
                {
                    Console.WriteLine($"Converting all wma from {source} to {target}.");
                    var ffmpeg = Path.GetFullPath(".\\ffmpeg.exe");
                    if (!File.Exists(ffmpeg))
                    {
                        Console.WriteLine("Couldn't find ffmpeg.exe on the current path.");
                        return;
                    }
                    FindAndConvert(source, target, ffmpeg);
                }
            }
            Console.ReadKey();
        }

        static void FindAndRemoveDuplicates(string target)
        {
            // Recurse into subdirectories.
            foreach (var subdirectoryFullPath in Directory.EnumerateDirectories(target))
            {
                var subdirectory = Path.GetFileName(subdirectoryFullPath);
                FindAndRemoveDuplicates(Path.Combine(target, subdirectory));
            }
            // Find mp3s and copy them over if they don't already exist.
            foreach (var mp3 in Directory.EnumerateFiles(target, "* (1).mp3", SearchOption.TopDirectoryOnly))
            {
                var original = Path.Combine(target, mp3.Substring(0, mp3.Length - 8) + ".mp3");
                if (File.Exists(original))
                {
                    Console.WriteLine($"- {mp3}");
                    File.Delete(mp3);
                }
            }
        }

        static void FindAndConvert(string source, string target, string ffmpeg)
        {
            Console.WriteLine();
            Console.WriteLine($"{source} -> {target}");
            Directory.CreateDirectory(target);
            // Recurse into subdirectories.
            foreach(var subdirectoryFullPath in Directory.EnumerateDirectories(source))
            {
                var subdirectory = Path.GetFileName(subdirectoryFullPath);
                FindAndConvert(Path.Combine(source, subdirectory), Path.Combine(target, subdirectory), ffmpeg);
            }
            // Find mp3s and copy them over if they don't already exist.
            foreach (var mp3 in Directory.EnumerateFiles(source, "*.mp3", SearchOption.TopDirectoryOnly))
            {
                var mp3Target = Path.Combine(target, Path.GetFileName(mp3));
                if (File.Exists(mp3Target))
                {
                    Console.Write(".");
                }
                else
                {
                    File.Copy(mp3, mp3Target);
                    Console.Write("#");
                }
            }
            // Find wmas, copy them locally, convert them, then copy to the destination.
            foreach (var wma in Directory.EnumerateFiles(source, "*.wma", SearchOption.TopDirectoryOnly))
            {
                var fileName = Path.GetFileNameWithoutExtension(wma);
                var mp3 = fileName + ".mp3";
                var mp3FullPath = Path.Combine(target, mp3);
                // mp3 already exists at destination. Skip.
                if (File.Exists(mp3FullPath))
                {
                    Console.Write(".");
                }
                else
                {
                    var tempPath = Path.Combine(Path.GetTempPath(), "wma2mp3");
                    var tempPathWma = tempPath + ".wma";
                    // Copy the wma locally.
                    File.Copy(wma, tempPathWma, true);
                    var tempPathMp3 = tempPath + ".mp3";
                    // Put ffmpeg on the case.
                    var ffmpegProcessStartInfo = new ProcessStartInfo
                    {
                        FileName = ffmpeg,
                        Arguments = $" -i \"{tempPathWma}\" -acodec libmp3lame -ab 160k -ac 2 -ar 44100 \"{tempPathMp3}\"",
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    var ffmpegProcess = Process.Start(ffmpegProcessStartInfo);
                    ffmpegProcess.WaitForExit();
                    // We should now have a local mp3. Move it to dest.
                    if (File.Exists(tempPathMp3))
                    {
                        File.Move(tempPathMp3, mp3FullPath);
                    }
                    // We're done. Clean-up the temporary local copy of the wma.
                    File.Delete(tempPathWma);
                    if (ffmpegProcess.ExitCode == 0)
                    {
                        Console.Write("*");
                    }
                    else
                    {
                        Console.WriteLine();
                        Console.WriteLine($"Error {ffmpegProcess.ExitCode} while converting {wma}.");
                    }
                }
            }
        }

        static void Check(string source, string target)
        {
            Directory.CreateDirectory(target);
            // Recurse into subdirectories.
            foreach (var subdirectoryFullPath in Directory.EnumerateDirectories(source))
            {
                var subdirectory = Path.GetFileName(subdirectoryFullPath);
                Check(Path.Combine(source, subdirectory), Path.Combine(target, subdirectory));
            }
            // Find wmas, compare them to the destination.
            foreach (var wma in Directory.EnumerateFiles(source, "*.wma", SearchOption.TopDirectoryOnly))
            {
                var fileName = Path.GetFileNameWithoutExtension(wma);
                var mp3 = fileName + ".mp3";
                var mp3FullPath = Path.Combine(target, mp3);
                // mp3 already exists at destination.
                if (File.Exists(mp3FullPath))
                {
                    var wmaSize = new FileInfo(wma).Length;
                    var mp3Size = new FileInfo(mp3FullPath).Length;
                    if (wmaSize == 0)
                    {
                        Console.WriteLine($"0 {wma} empty");
                    }
                    // mp3 is smaller than wma by more than {deviation}%. Flag it.
                    else if (((double)mp3Size / wmaSize) < (1 - (double)deviation/100))
                    {
                        Console.WriteLine($"! {mp3FullPath} {mp3Size} << {wmaSize}");
                    }
                }
                else
                {
                    Console.WriteLine($"x {mp3FullPath} missing");
                }
            }
        }
    }
}
