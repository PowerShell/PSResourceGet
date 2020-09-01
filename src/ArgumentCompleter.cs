using Microsoft.PowerShell.PowerShellGet.RepositorySettings;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Language;

internal class NameCompleter : IArgumentCompleter
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
        var res = new List<CompletionResult>();

        var r = new RespositorySettings();
        var listOfRepositories = r.Read(new string[]{ });

        foreach (var repo in listOfRepositories)
         {
            if ((repo.Properties["Name"].Value.ToString()).StartsWith(wordToComplete, StringComparison.OrdinalIgnoreCase))
            {
                res.Add(new CompletionResult(repo.Properties["Name"].Value.ToString()));
            }
        }

        return res;
    }
}