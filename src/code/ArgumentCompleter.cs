// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Language;
using Microsoft.PowerShell.PowerShellGet.UtilClasses;

internal class RepositoryNameCompleter : IArgumentCompleter
{
    public IEnumerable<CompletionResult> CompleteArgument(
        string commandName,
        string parameterName,
        string wordToComplete,
        CommandAst commandAst,
        IDictionary fakeBoundParameters)
    {
        List<PSRepositoryItem> listOfRepositories = RepositorySettings.Read(null, out string[] _);
        Console.WriteLine(listOfRepositories.Count);
        foreach(PSRepositoryItem repo in listOfRepositories)
        {
            string repoName = repo.Name;
            if (repoName.StartsWith(wordToComplete, StringComparison.OrdinalIgnoreCase))
            {
                yield return new CompletionResult(repoName);
            }
        }
    }
}
