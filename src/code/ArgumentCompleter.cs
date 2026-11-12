// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PSResourceGet.UtilClasses;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Language;

internal class RuntimeIdentifierCompleter : IArgumentCompleter
{
    private static readonly string[] s_knownRids =
    {
        "win-x64",
        "win-x86",
        "win-arm64",
        "linux-x64",
        "linux-arm64",
        "linux-arm",
        "linux-musl-x64",
        "linux-musl-arm64",
        "osx-x64",
        "osx-arm64",
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

        foreach (string rid in s_knownRids)
        {
            if (wordToCompletePattern.IsMatch(rid))
            {
                yield return new CompletionResult(rid);
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
