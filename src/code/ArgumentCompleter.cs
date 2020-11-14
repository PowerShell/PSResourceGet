using Microsoft.PowerShell.PowerShellGet.RepositorySettings;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Language;

internal class RepositoryNameCompleter : IArgumentCompleter
{
    public IEnumerable<CompletionResult> CompleteArgument(
        string commandName,  // For cmdlets Get-PSResource, Set-PSResource, and Unregister-PSResource
        string parameterName, // For -Name parameter
        string wordToComplete,
        CommandAst commandAst,
        IDictionary fakeBoundParameters)
    {
        return CompleteRepositoryName(wordToComplete);
    }


    private IEnumerable<CompletionResult> CompleteRepositoryName(string wordToComplete)
    {
        List<CompletionResult> res = new List<CompletionResult>();

        RespositorySettings repositorySettings = new RespositorySettings();
        IReadOnlyList<PSObject> listOfRepositories = repositorySettings.Read(null);

        foreach (PSObject repo in listOfRepositories)
         {
            string repoName = repo.Properties["Name"].Value.ToString();
            if (repoName.StartsWith(wordToComplete, StringComparison.OrdinalIgnoreCase))
            {
                res.Add(new CompletionResult(repoName));
            }
        }

        return res;
    }
}