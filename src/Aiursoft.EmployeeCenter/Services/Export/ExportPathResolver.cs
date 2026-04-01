using Aiursoft.Scanner.Abstractions;
using Microsoft.Extensions.Configuration;

namespace Aiursoft.EmployeeCenter.Services.Export;

public class ExportPathResolver : ITransientDependency
{
    private readonly string _exportRoot;
    private readonly string _autoExportRoot;

    public ExportPathResolver(IConfiguration configuration)
    {
        var basePath = configuration["Storage:Path"] ?? 
                       throw new InvalidDataException("Missing config 'Storage:Path'!");
        _exportRoot = Path.Combine(basePath, "Exports");
        _autoExportRoot = Path.Combine(basePath, "AutoExports");

        if (!Directory.Exists(_exportRoot))
        {
            Directory.CreateDirectory(_exportRoot);
        }

        if (!Directory.Exists(_autoExportRoot))
        {
            Directory.CreateDirectory(_autoExportRoot);
        }
    }

    public string GetUserExportRoot(string userId)
    {
        var path = Path.Combine(_exportRoot, userId);
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
        return path;
    }

    public string GetAutoExportRoot()
    {
        return _autoExportRoot;
    }
}
