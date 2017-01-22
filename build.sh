mono .nuget/NuGet.exe restore Metrics.NET.ElasticSearch.sln 

xbuild Metrics.Sln /p:Configuration="Debug"
xbuild Metrics.Sln /p:Configuration="Release"