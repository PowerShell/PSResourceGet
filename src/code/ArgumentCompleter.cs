// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PSResourceGet.UtilClasses;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Language;

internal class TargetFrameworkCompleter : IArgumentCompleter
{
    private static readonly string[] s_knownTargetFrameworks = new[]
    {
        "net462", "net472", "net48",
        "netstandard2.0", "netstandard2.1",
        "net6.0", "net7.0", "net8.0", "net9.0", "net10.0",
    };

    public IEnumerable<CompletionResult> CompleteArgument(
        string commandName,
        string parameterName,
        string wordToComplete,
        CommandAst commandAst,
        IDictionary fakeBoundParameters)
    {
        wordToComplete = Utils.TrimQuotes(wordToComplete);
        var wordToCompletePattern = WildcardPattern.Get(
            pattern: string.IsNullOrWhiteSpace(wordToComplete) ? "*" : wordToComplete + "*",
            options: WildcardOptions.IgnoreCase);

        foreach (string targetFramework in s_knownTargetFrameworks)
        {
            if (wordToCompletePattern.IsMatch(targetFramework))
            {
                yield return new CompletionResult(targetFramework);
            }
        }
    }
}

internal class RepositoryNameCompleter : IArgumentCompleter
{
    public IEnumerable<CompletionResult> CompleteArgument(
        string commandName,
        string parameterName,
        string wordToComplete,
        CommandAst commandAst,
        IDictionary fakeBoundParameters)
    {
        List<PSRepositoryInfo> listOfRepositories = RepositorySettings.Read(null, out string[] _);

        wordToComplete = Utils.TrimQuotes(wordToComplete);
        var wordToCompletePattern = WildcardPattern.Get(
            pattern: string.IsNullOrWhiteSpace(wordToComplete) ? "*" : wordToComplete + "*",
            options: WildcardOptions.IgnoreCase);

        foreach (PSRepositoryInfo repo in listOfRepositories)
        {
            string repoName = repo.Name;
            if (wordToCompletePattern.IsMatch(repoName))
            {
                yield return new CompletionResult(Utils.QuoteName(repoName));
            }
        }
    }
}
