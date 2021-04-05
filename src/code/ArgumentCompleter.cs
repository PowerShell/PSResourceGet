using System.ComponentModel;
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Language;

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
