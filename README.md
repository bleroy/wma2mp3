wma2mp3
=======

A simple way to bulk-convert a wma library to mp3 by driving ffmpeg.

This program requires ffmpeg. You can download the latest from https://ffmpeg.org/.
Just copy the ffmpeg.exe executable to the root of the project before you build.

Building the project will also make a copy of ffmpeg.exe next to wma2mp3.exe.
At runtime, wma2mp3 looks for ffmpeg.exe next to itself.

Usage
-----

```bat
wma2mp3 sourcepath targetpath
```

Wma2mp3 will scan sourcepath recursively, will copy all files but wma files, which will be transcoded to mp3.

```bat
wma2mp3 -c sourcepath targetpath
```

Specifying the 'c' flag will do a comparison in size of source wma files with their mp3 converted counterparts,
and will print out the path of any file that is smaller than its source by more than {deviation}% in size.
This is useful to do a sanity check on the results of a bulk conversion, as corrupted conversions will often
result in incomplete files.
