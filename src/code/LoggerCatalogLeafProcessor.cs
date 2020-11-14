/***
using System;
using System.Threading.Tasks;
//using Microsoft.Extensions.Logging;
using NuGet.Protocol.Catalog;

public class LoggerCatalogLeafProcessor : ICatalogLeafProcessor
{
    private const int FailAfterCommitCount = 10;
    //private readonly ILogger<LoggerCatalogLeafProcessor> _logger;
    private DateTimeOffset? _lastCommitTimestamp;
    private int _commitCount;

    /*
    public LoggerCatalogLeafProcessor(ILogger<LoggerCatalogLeafProcessor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _lastCommitTimestamp = null;
        _commitCount = 0;
    }
    */
/*** 
    public Task<bool> ProcessPackageDeleteAsync(PackageDeleteCatalogLeaf leaf)
    {
        Console.WriteLine(
            $"{leaf.CommitTimestamp:O}: Found package delete leaf for {leaf.PackageId} {leaf.PackageVersion}.");

        return GetResultAsync(leaf);
    }

    public Task<bool> ProcessPackageDetailsAsync(PackageDetailsCatalogLeaf leaf)
    {
        Console.WriteLine(
            $"{leaf.CommitTimestamp:O}: Found package details leaf for {leaf.PackageId} {leaf.PackageVersion}.");

        return GetResultAsync(leaf);
    }

    private Task<bool> GetResultAsync(ICatalogLeafItem leaf)
    {
        if (_lastCommitTimestamp.HasValue
            && _lastCommitTimestamp.Value != leaf.CommitTimestamp)
        {
            _commitCount++;

            /*
            // Simulate a failure every N commits to demonstrate how cursors and failures interact.
            if (_commitCount % FailAfterCommitCount == 0)
            {
                _logger.LogError(
                    "{commitCount} catalog commits have been processed. We will now simulate a failure.",
                    FailAfterCommitCount);
                return Task.FromResult(false);
            }
            */
/***
        }

        _lastCommitTimestamp = leaf.CommitTimestamp;
        return Task.FromResult(true);
    }
}
*/