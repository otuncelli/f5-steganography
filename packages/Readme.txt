This versions of packages specified in ..\F5Lib\F5Lib.csproj

*.nupkg files - this is a ZIP-archives and can be renamed to *.zip and unzipped in the folders, then.

In the folders - only included dll's.

After unzip packages, can be runned the compilation:
	\F5Console\compile.bat
disk:\WINDOWS\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe /property:Configuration=Release F5Console.csproj

	\F5Lib\compile.bat
disk:\WINDOWS\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe /property:Configuration=Release F5Lib.csproj

After compilation can be runned F5 console application:
	\F5Console\bin\Release\f5.bat
F5 e -e test.txt -p mypasswd -q 70 15017749353771.jpg out.jpg
F5 x -p mypasswd -e extracted_test.txt out.jpg

