mono .nuget/NuGet.exe restore Metrics.NET.ElasticSearch.sln 

xbuild Metrics.NET.ElasticSearch.Sln /p:Configuration="Debug"
xbuild Metrics.NET.ElasticSearch.Sln /p:Configuration="Release"