rd /S /Q .\Publishing\lib

call build.bat
if %errorlevel% neq 0 exit /b %errorlevel%

md .\Publishing\lib
md .\Publishing\lib\net45

copy .\bin\Release\Metrics.NET.ElasticSearch.dll .\Publishing\lib\net45\
copy .\bin\Release\Metrics.NET.ElasticSearch.xml .\Publishing\lib\net45\
copy .\bin\Release\Metrics.NET.ElasticSearch.pdb .\Publishing\lib\net45\

.\.nuget\NuGet.exe pack .\Publishing\Metrics.NET.ElasticSearch.nuspec -OutputDirectory .\Publishing
if %errorlevel% neq 0 exit /b %errorlevel%