// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PSResourceGet.UtilClasses;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace Microsoft.PowerShell.PSResourceGet.Cmdlets
{
    /// <summary>
    /// Find helper class provides the core functionality for FindPSResource.
    /// </summary>
    internal class FindHelper
    {
        #region Members

        private CancellationToken _cancellationToken;
        private readonly PSCmdlet _cmdletPassedIn;
        private HashSet<string> _pkgsLeftToFind;
        private List<string> _tagsLeftToFind;
        private ResourceType _type;
        private string _version;
        private VersionRange _versionRange;
        private NuGetVersion _nugetVersion;
        private VersionType _versionType;
        private bool _prerelease = false;
        private string[] _tag;
        private bool _includeDependencies = false;
        private bool _repositoryNameContainsWildcard = true;
        private NetworkCredential _networkCredential;

        #endregion

        #region Constructor

        private FindHelper() { }

        public FindHelper(CancellationToken cancellationToken, PSCmdlet cmdletPassedIn, NetworkCredential networkCredential)
        {
            _cancellationToken = cancellationToken;
            _cmdletPassedIn = cmdletPassedIn;
            _networkCredential = networkCredential;
        }

        #endregion

        #region Public Methods

        public IEnumerable<PSResourceInfo> FindByResourceName(
            string[] name,
            ResourceType type,
            VersionRange versionRange,
            NuGetVersion nugetVersion,
            VersionType versionType,
            string version,
            bool prerelease,
            string[] tag,
            string[] repository,
            bool includeDependencies,
            bool suppressErrors)
        {
            _type = type;
            _version = version;
            _prerelease = prerelease;
            _tag = tag ?? Utils.EmptyStrArray;
            _includeDependencies = includeDependencies;
            _versionRange = versionRange;
            _nugetVersion = nugetVersion;
            _versionType = versionType;

            _cmdletPassedIn.WriteDebug("In FindHelper::FindByResourceName()");
            _cmdletPassedIn.WriteDebug(string.Format("Parameters passed in >>> Name: '{0}'; ResourceType: '{1}'; VersionRange: '{2}'; NuGetVersion: '{3}'; VersionType: '{4}'; Version: '{5}'; Prerelease: '{6}'; " +
                "Tag: '{7}'; Repository: '{8}'; IncludeDependencies '{9}'",
                string.Join(",", name),
                type.ToString() ?? string.Empty,
                versionRange != null ? (versionRange.OriginalString != null ? versionRange.OriginalString : string.Empty) : string.Empty,
                nugetVersion != null ? nugetVersion.ToString() : string.Empty,
                versionType.ToString(),
                version != null ? version : String.Empty,
                prerelease.ToString(),
                tag != null ? string.Join(",", tag) : string.Empty,
                repository != null ? string.Join(",", repository) : string.Empty,
                includeDependencies.ToString()));

            if (name.Length == 0)
            {
                _cmdletPassedIn.WriteDebug("Names were not provided or could not be resolved");
                yield break;
            }

            _pkgsLeftToFind = new HashSet<string>(name, StringComparer.InvariantCultureIgnoreCase);
            HashSet<string> pkgsDiscovered = GetPackageNamesPopulated(_pkgsLeftToFind.ToArray());

            _tagsLeftToFind = tag == null ? new List<string>() : new List<string>(tag);

            if (repository != null)
            {
                // Write error and disregard repository entries containing wildcards.
                repository = Utils.ProcessNameWildcards(repository, removeWildcardEntries:false, out string[] errorMsgs, out _repositoryNameContainsWildcard);
                foreach (string error in errorMsgs)
                {
                    _cmdletPassedIn.WriteError(new ErrorRecord(
                        new PSInvalidOperationException(error),
                        "ErrorFilteringNamesForUnsupportedWildcards",
                        ErrorCategory.InvalidArgument,
                        this));
                }

                // If repository entries includes wildcards and non-wildcard names, write terminating error
                // Ex: -Repository *Gallery, localRepo
                bool containsWildcard = false;
                bool containsNonWildcard = false;
                foreach (string repoName in repository)
                {
                    if (repoName.Contains("*"))
                    {
                        containsWildcard = true;
                    }
                    else
                    {
                        containsNonWildcard = true;
                    }
                }

                if (containsNonWildcard && containsWildcard)
                {
                    _cmdletPassedIn.ThrowTerminatingError(new ErrorRecord(
                        new PSInvalidOperationException("Repository name with wildcard is not allowed when another repository without wildcard is specified."),
                        "RepositoryNamesWithWildcardsAndNonWildcardUnsupported",
                        ErrorCategory.InvalidArgument,
                        this));
                }
            }


            // At this point we can only have scenarios such as:
            // -Repository PSGallery
            // -Repository PSGallery, NuGetGallery -> search and write error for each
            // -Repository *Gallery -> write error if in none
            // No -Repository -> search all, write error if in none

            // Get repositories to search.
            List<PSRepositoryInfo> repositoriesToSearch;
            try
            {
                repositoriesToSearch = RepositorySettings.Read(repository, out string[] errorList);
                if (repositoriesToSearch != null && repositoriesToSearch.Count == 0)
                {
                    _cmdletPassedIn.ThrowTerminatingError(new ErrorRecord(
                        new PSArgumentException ("Cannot resolve -Repository name. Run 'Get-PSResourceRepository' to view all registered repositories."),
                        "RepositoryNameIsNotResolved",
                        ErrorCategory.InvalidArgument,
                        this));
                }

                foreach (string error in errorList)
                {
                    _cmdletPassedIn.WriteError(new ErrorRecord(
                        new PSInvalidOperationException(error),
                        "ErrorRetrievingSpecifiedRepository",
                        ErrorCategory.InvalidOperation,
                        this));
                }
            }
            catch (Exception e)
            {
                _cmdletPassedIn.ThrowTerminatingError(new ErrorRecord(
                    new PSInvalidOperationException(e.Message),
                    "ErrorLoadingRepositoryStoreFile",
                    ErrorCategory.InvalidArgument,
                    this));

                yield break;
            }

            List<string> repositoryNamesToSearch = new List<string>();
            for (int i = 0; i < repositoriesToSearch.Count; i++)
            {
                PSRepositoryInfo currentRepository = repositoriesToSearch[i];
                repositoryNamesToSearch.Add(currentRepository.Name);
                _networkCredential = Utils.SetNetworkCredential(currentRepository, _networkCredential, _cmdletPassedIn);
                ServerApiCall currentServer = ServerFactory.GetServer(currentRepository, _cmdletPassedIn, _networkCredential);
                if (currentServer == null)
                {
                    // this indicates that PSRepositoryInfo.APIVersion = PSRepositoryInfo.APIVersion.unknown
                    _cmdletPassedIn.WriteError(new ErrorRecord(
                        new PSInvalidOperationException($"Repository '{currentRepository.Name}' is not a known repository type that is supported. Please file an issue for support at https://github.com/PowerShell/PSResourceGet/issues"),
                        "RepositoryApiVersionUnknown",
                        ErrorCategory.InvalidArgument,
                        this));

                    continue;
                }

                ResponseUtil currentResponseUtil = ResponseUtilFactory.GetResponseUtil(currentRepository);
                _cmdletPassedIn.WriteDebug($"Searching through repository '{currentRepository.Name}'");

                bool shouldReportErrorForEachRepo = !suppressErrors && !_repositoryNameContainsWildcard;
                foreach (PSResourceInfo currentPkg in SearchByNames(currentServer, currentResponseUtil, currentRepository, shouldReportErrorForEachRepo))
                {
                    if (currentPkg == null) {
                        _cmdletPassedIn.WriteDebug("No packages returned from server");
                        continue;
                    }

                    string currentPkgName = currentPkg.Name;
                    _cmdletPassedIn.WriteDebug($"Package '{currentPkgName}' returned from server");
                    // Check if pkgsDiscovered dictionary contains this package name exactly, otherwise this may have been a package found for wildcard name input.
                    if (pkgsDiscovered.Contains(currentPkgName))
                    {
                        _cmdletPassedIn.WriteDebug($"Package '{currentPkgName}' was previously discovered and returned");
                        pkgsDiscovered.Remove(currentPkgName);
                    }

                    yield return currentPkg;
                }
            }

            if (!suppressErrors && _repositoryNameContainsWildcard)
            {
                // Scenarios: Find-PSResource -Name "pkg" -> write error only if pkg wasn't found in any registered repositories
                // Scenarios: Find-PSResource -Name "pkg" -Repository *Gallery -> write error if only if pkg wasn't found in any matching repositories.
                foreach(string pkgName in pkgsDiscovered)
                {
                    var msg = repository == null ? $"Package '{pkgName}' could not be found in any registered repositories." : 
                        $"Package '{pkgName}' could not be found in registered repositories: '{string.Join(", ", repositoryNamesToSearch)}'.";

                    _cmdletPassedIn.WriteError(new ErrorRecord(
                        new ResourceNotFoundException(msg),
                        "PackageNotFound",
                        ErrorCategory.ObjectNotFound,
                        this));
                }
            }
}

        public IEnumerable<PSCommandResourceInfo> FindByCommandOrDscResource(
            bool isSearchingForCommands,
            bool prerelease,
            string[] tag,
            string[] repository)
        {
            _prerelease = prerelease;
            _tag = tag;

            _cmdletPassedIn.WriteDebug("In FindHelper::FindByCommandOrDscResource()");
            _cmdletPassedIn.WriteDebug(string.Format("Parameters passed in >>> IsSearchingForCommands: '{0}'; Prerelease: '{1}'; Tag: '{2}'; Repository: '{3}'",
                isSearchingForCommands.ToString(),
                prerelease.ToString(),
                string.Join(",", tag),
                repository != null ? string.Join(",", repository) : string.Empty));

            if (_tag.Length == 0)
            {
                _cmdletPassedIn.WriteDebug("Tags were not provided or could not be resolved");
                yield break;
            }

            // Error out if repository array of names to be searched contains wildcards.
            if (repository != null)
            {
                repository = Utils.ProcessNameWildcards(repository, removeWildcardEntries:false, out string[] errorMsgs, out _repositoryNameContainsWildcard);

                if (string.Equals(repository[0], "*"))
                {
                    _cmdletPassedIn.ThrowTerminatingError(new ErrorRecord(
                        new PSArgumentException ("-Repository parameter does not support entry '*' with -CommandName and -DSCResourceName parameters."),
                        "RepositoryDoesNotSupportWildcardEntryWithCmdOrDSCName",
                        ErrorCategory.InvalidArgument,
                        this));
                }

                foreach (string error in errorMsgs)
                {
                    _cmdletPassedIn.WriteError(new ErrorRecord(
                        new PSInvalidOperationException(error),
                        "ErrorFilteringNamesForUnsupportedWildcards",
                        ErrorCategory.InvalidArgument,
                        this));
                }

                // If repository entries includes wildcards and non-wildcard names, write terminating error
                // Ex: -Repository *Gallery, localRepo
                bool containsWildcard = false;
                bool containsNonWildcard = false;
                foreach (string repoName in repository)
                {
                    if (repoName.Contains("*"))
                    {
                        containsWildcard = true;
                    }
                    else
                    {
                        containsNonWildcard = true;
                    }
                }

                if (containsNonWildcard && containsWildcard)
                {
                    _cmdletPassedIn.ThrowTerminatingError(new ErrorRecord(
                        new PSInvalidOperationException("Repository name with wildcard is not allowed when another repository without wildcard is specified."),
                        "RepositoryNamesWithWildcardsAndNonWildcardUnsupported",
                        ErrorCategory.InvalidArgument,
                        this));
                }
            }

            // Get repositories to search.
            List<PSRepositoryInfo> repositoriesToSearch;
            try
            {
                repositoriesToSearch = RepositorySettings.Read(repository, out string[] errorList);
                if (repositoriesToSearch != null && repositoriesToSearch.Count == 0)
                {
                    _cmdletPassedIn.ThrowTerminatingError(new ErrorRecord(
                        new PSArgumentException ("Cannot resolve -Repository name. Run 'Get-PSResourceRepository' to view all registered repositories."),
                        "RepositoryNameIsNotResolved",
                        ErrorCategory.InvalidArgument,
                        this));
                }

                foreach (string error in errorList)
                {
                    _cmdletPassedIn.WriteError(new ErrorRecord(
                        new PSInvalidOperationException(error),
                        "ErrorRetrievingSpecifiedRepository",
                        ErrorCategory.InvalidOperation,
                        this));
                }
            }
            catch (Exception e)
            {
                _cmdletPassedIn.ThrowTerminatingError(new ErrorRecord(
                    new PSInvalidOperationException(e.Message),
                    "ErrorLoadingRepositoryStoreFile",
                    ErrorCategory.InvalidArgument,
                    this));

                yield break;
            }

            List<string> repositoryNamesToSearch = new List<string>();
            // For tags that are representative of Commands or DSCResource names, all those tags must be present in the returned package.
            // The class inheriting from ServerApiCalls must ensure packages returned satisfied all Command/DSCResource tags.
            bool isCmdOrDSCTagFound = false;
            bool shouldReportErrorForEachRepo = !_repositoryNameContainsWildcard;
            for (int i = 0; i < repositoriesToSearch.Count; i++)
            {
                PSRepositoryInfo currentRepository = repositoriesToSearch[i];
                repositoryNamesToSearch.Add(currentRepository.Name);
                _networkCredential = Utils.SetNetworkCredential(currentRepository, _networkCredential, _cmdletPassedIn);
                ServerApiCall currentServer = ServerFactory.GetServer(currentRepository, _cmdletPassedIn, _networkCredential);
                if (currentServer == null)
                {
                    // this indicates that PSRepositoryInfo.APIVersion = PSRepositoryInfo.APIVersion.unknown
                    _cmdletPassedIn.WriteError(new ErrorRecord(
                        new PSInvalidOperationException($"Repository '{currentRepository.Name}' is not a known repository type that is supported. Please file an issue for support at https://github.com/PowerShell/PSResourceGet/issues"),
                        "RepositoryApiVersionUnknown",
                        ErrorCategory.InvalidArgument,
                        this));

                    continue;
                }

                ResponseUtil currentResponseUtil = ResponseUtilFactory.GetResponseUtil(currentRepository);

                _cmdletPassedIn.WriteDebug($"Searching in repository '{currentRepository.Name}' for tags: '{String.Join(",", _tag)}'");
                FindResults responses = currentServer.FindCommandOrDscResource(_tag, _prerelease, isSearchingForCommands, out ErrorRecord errRecord);
                if (errRecord != null)
                {
                    if (shouldReportErrorForEachRepo)
                    {
                        _cmdletPassedIn.WriteError(errRecord);
                    }
                    else
                    {
                        _cmdletPassedIn.WriteVerbose(errRecord.Exception.Message);
                    }

                    continue;
                }

                foreach (PSResourceResult currentResult in currentResponseUtil.ConvertToPSResourceResult(responses))
                {
                    if (currentResult.exception != null && !currentResult.exception.Message.Equals(string.Empty))
                    {
                        errRecord = new ErrorRecord(
                            new ResourceNotFoundException($"'{String.Join(", ", _tag)}' could not be found", currentResult.exception), 
                            "FindCmdOrDscToPSResourceObjFailure", 
                            ErrorCategory.NotSpecified, 
                            this);

                        if (shouldReportErrorForEachRepo)
                        {
                            _cmdletPassedIn.WriteError(errRecord);
                        }
                        else
                        {
                            _cmdletPassedIn.WriteVerbose(errRecord.Exception.Message);
                        }
                        
                        continue;
                    }

                    PSCommandResourceInfo currentCmdPkg = new PSCommandResourceInfo(_tag, currentResult.returnedObject);
                    isCmdOrDSCTagFound = true;
                    _cmdletPassedIn.WriteDebug($"Found Command or DSCResource with parent package '{currentCmdPkg.ParentResource.Name}'");

                    yield return currentCmdPkg;
                }
            }

            if (!isCmdOrDSCTagFound && !shouldReportErrorForEachRepo)
            {
                string parameterName = isSearchingForCommands ? "CommandName" : "DSCResourceName";
                var msg = repository == null ? $"Package with {parameterName} '{String.Join(", ", _tag)}' could not be found in any registered repositories." : 
                    $"Package with {parameterName} '{String.Join(", ", _tag)}' could not be found in registered repositories: '{string.Join(", ", repositoryNamesToSearch)}'.";

                _cmdletPassedIn.WriteError(new ErrorRecord(
                    new ResourceNotFoundException(msg),
                    "PackageWithCmdOrDscNotFound",
                    ErrorCategory.ObjectNotFound,
                    this));
            }
        }

        public IEnumerable<PSResourceInfo> FindByTag(
            ResourceType type,
            bool prerelease,
            string[] tag,
            string[] repository)
        {
            _type = type;
            _prerelease = prerelease;
            _tag = tag;

            _cmdletPassedIn.WriteDebug("In FindHelper::FindByTag()");
            _cmdletPassedIn.WriteDebug(string.Format("Parameters passed in >>> ResourceType: '{0}'; Prerelease: '{1}'; Tag: '{2}'; Repository: '{3}'",
                type.ToString() ?? string.Empty,
                prerelease.ToString(),
                string.Join(",", tag),
                repository != null ? string.Join(",", repository) : string.Empty));

            if (_tag.Length == 0)
            {
                _cmdletPassedIn.WriteDebug("Tags were not provided or could not be resolved");
                yield break;
            }

            if (repository != null)
            {
                repository = Utils.ProcessNameWildcards(repository, removeWildcardEntries:false, out string[] errorMsgs, out _repositoryNameContainsWildcard);

                if (string.Equals(repository[0], "*"))
                {
                    _cmdletPassedIn.ThrowTerminatingError(new ErrorRecord(
                        new PSArgumentException ("-Repository parameter does not support entry '*' with -Tag parameter."),
                        "RepositoryDoesNotSupportWildcardEntryWithTag",
                        ErrorCategory.InvalidArgument,
                        this));
                }

                foreach (string error in errorMsgs)
                {
                    _cmdletPassedIn.WriteError(new ErrorRecord(
                        new PSInvalidOperationException(error),
                        "ErrorFilteringNamesForUnsupportedWildcards",
                        ErrorCategory.InvalidArgument,
                        this));
                }

                // If repository entries includes wildcards and non-wildcard names, write terminating error
                // Ex: -Repository *Gallery, localRepo
                bool containsWildcard = false;
                bool containsNonWildcard = false;
                foreach (string repoName in repository)
                {
                    if (repoName.Contains("*"))
                    {
                        containsWildcard = true;
                    }
                    else
                    {
                        containsNonWildcard = true;
                    }
                }

                if (containsNonWildcard && containsWildcard)
                {
                    _cmdletPassedIn.ThrowTerminatingError(new ErrorRecord(
                        new PSInvalidOperationException("Repository name with wildcard is not allowed when another repository without wildcard is specified."),
                        "RepositoryNamesWithWildcardsAndNonWildcardUnsupported",
                        ErrorCategory.InvalidArgument,
                        this));
                }
            }

            // Get repositories to search.
            List<PSRepositoryInfo> repositoriesToSearch;
            try
            {
                repositoriesToSearch = RepositorySettings.Read(repository, out string[] errorList);
                if (repositoriesToSearch != null && repositoriesToSearch.Count == 0)
                {
                    _cmdletPassedIn.ThrowTerminatingError(new ErrorRecord(
                        new PSArgumentException ("Cannot resolve -Repository name. Run 'Get-PSResourceRepository' to view all registered repositories."),
                        "RepositoryNameIsNotResolved",
                        ErrorCategory.InvalidArgument,
                        this));
                }

                foreach (string error in errorList)
                {
                    _cmdletPassedIn.WriteError(new ErrorRecord(
                        new PSInvalidOperationException(error),
                        "ErrorRetrievingSpecifiedRepository",
                        ErrorCategory.InvalidOperation,
                        this));
                }
            }
            catch (Exception e)
            {
                _cmdletPassedIn.ThrowTerminatingError(new ErrorRecord(
                    new PSInvalidOperationException(e.Message),
                    "ErrorLoadingRepositoryStoreFile",
                    ErrorCategory.InvalidArgument,
                    this));

                yield break;
            }

            // For Find-PSResource with -Tags, only packages that have *all* required tags are returned.
            // The class inheriting from ServerA1piCalls must ensure packages returned satisfied all tags.
            bool isTagFound = false;
            bool shouldReportErrorForEachRepo = !_repositoryNameContainsWildcard;
            List<string> repositoryNamesToSearch = new List<string>();
            for (int i = 0; i < repositoriesToSearch.Count; i++)
            {
                PSRepositoryInfo currentRepository = repositoriesToSearch[i];
                repositoryNamesToSearch.Add(currentRepository.Name);
                _networkCredential = Utils.SetNetworkCredential(currentRepository, _networkCredential, _cmdletPassedIn);
                ServerApiCall currentServer = ServerFactory.GetServer(currentRepository, _cmdletPassedIn, _networkCredential);
                if (currentServer == null)
                {
                    // this indicates that PSRepositoryInfo.APIVersion = PSRepositoryInfo.APIVersion.unknown
                    _cmdletPassedIn.WriteError(new ErrorRecord(
                        new PSInvalidOperationException($"Repository '{currentRepository.Name}' is not a known repository type that is supported. Please file an issue for support at https://github.com/PowerShell/PSResourceGet/issues"),
                        "RepositoryApiVersionUnknown",
                        ErrorCategory.InvalidArgument,
                        this));

                    continue;
                }

                ResponseUtil currentResponseUtil = ResponseUtilFactory.GetResponseUtil(currentRepository);
                _cmdletPassedIn.WriteDebug($"Searching through repository '{currentRepository.Name}'");
                if (_type != ResourceType.None && repositoriesToSearch[i].Name != "PSGallery")
                {
                    _cmdletPassedIn.ThrowTerminatingError(new ErrorRecord(
                        new PSInvalidOperationException("-Type parameter is only supported with the PowerShellGallery."),
                        "ErrorUsingTypeParameter",
                        ErrorCategory.InvalidOperation,
                        this));
                }

                FindResults responses = currentServer.FindTags(_tag, _prerelease, type, out ErrorRecord errRecord);
                if (errRecord != null)
                {
                    if (shouldReportErrorForEachRepo)
                    {
                        _cmdletPassedIn.WriteError(errRecord);
                    }
                    else
                    {
                        _cmdletPassedIn.WriteVerbose(errRecord.Exception.Message);
                    }

                    continue;
                }

                foreach (PSResourceResult currentResult in currentResponseUtil.ConvertToPSResourceResult(responses))
                {
                    if (currentResult.exception != null && !currentResult.exception.Message.Equals(string.Empty))
                    {
                        errRecord = new ErrorRecord(
                            new ResourceNotFoundException($"Tags '{String.Join(", ", _tag)}' could not be found" , currentResult.exception), 
                            "FindTagConvertToPSResourceFailure", 
                            ErrorCategory.InvalidResult, 
                            this);

                        if (shouldReportErrorForEachRepo)
                        {
                            _cmdletPassedIn.WriteError(errRecord);
                        }
                        else
                        {
                            _cmdletPassedIn.WriteVerbose(errRecord.Exception.Message);
                        }

                        continue;
                    }

                    isTagFound = true;
                    yield return currentResult.returnedObject;
                }
            }

            if (!isTagFound && !shouldReportErrorForEachRepo)
            {
                var msg = repository == null ? $"Package with Tags '{String.Join(", ", _tag)}' could not be found in any registered repositories." : 
                    $"Package with Tags '{String.Join(", ", _tag)}' could not be found in registered repositories: '{string.Join(", ", repositoryNamesToSearch)}'.";

                _cmdletPassedIn.WriteError(new ErrorRecord(
                    new ResourceNotFoundException(msg),
                    "PackageWithTagsNotFound",
                    ErrorCategory.ObjectNotFound,
                    this));
            }
        }

        #endregion

        #region Private Client Search Methods

        private IEnumerable<PSResourceInfo> SearchByNames(ServerApiCall currentServer, ResponseUtil currentResponseUtil, PSRepositoryInfo repository, bool shouldReportErrorForEachRepo)
        {
            ErrorRecord errRecord = null;
            List<PSResourceInfo> parentPkgs = new List<PSResourceInfo>();
            HashSet<string> pkgsFound = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string tagsAsString = String.Empty;

            _cmdletPassedIn.WriteDebug("In FindHelper::SearchByNames()");
            foreach (string pkgName in _pkgsLeftToFind.ToArray())
            {
                if (_versionType == VersionType.NoVersion)
                {
                    if (pkgName.Trim().Equals("*"))
                    {
                        _cmdletPassedIn.WriteDebug("No version specified, package name is '*'");
                        // Example: Find-PSResource -Name "*"
                        FindResults responses = currentServer.FindAll(_prerelease, _type, out errRecord);
                        if (errRecord != null)
                        {
                            if (shouldReportErrorForEachRepo)
                            {
                                _cmdletPassedIn.WriteError(errRecord);
                            }
                            else
                            {
                                _cmdletPassedIn.WriteVerbose(errRecord.Exception.Message);
                            }

                            continue;
                        }

                        foreach (PSResourceResult currentResult in currentResponseUtil.ConvertToPSResourceResult(responseResults: responses))
                        {
                            if (currentResult.exception != null && !currentResult.exception.Message.Equals(string.Empty))
                            {
                                _cmdletPassedIn.WriteError(new ErrorRecord(
                                    currentResult.exception, 
                                    "FindAllConvertToPSResourceFailure", 
                                    ErrorCategory.InvalidResult, 
                                    this));

                                continue;
                            }

                            PSResourceInfo foundPkg = currentResult.returnedObject;

                            if (foundPkg.Type == _type || _type == ResourceType.None)
                            {
                                parentPkgs.Add(foundPkg);
                                pkgsFound.Add(String.Format("{0}{1}", foundPkg.Name, foundPkg.Version.ToString()));
                                _cmdletPassedIn.WriteDebug($"Found package '{foundPkg.Name}' version '{foundPkg.Version}'");
                                yield return foundPkg;
                            }
                        }
                    }
                    else if(pkgName.Contains("*"))
                    {
                        // Example: Find-PSResource -Name "Az*"
                        // Example: Find-PSResource -Name "Az*" -Tag "Storage"
                        _cmdletPassedIn.WriteDebug("No version specified, package name contains a wildcard.");

                        FindResults responses = null;
                        if (_tag.Length == 0)
                        {
                            responses = currentServer.FindNameGlobbing(pkgName, _prerelease, _type, out errRecord);
                        }
                        else
                        {
                            responses = currentServer.FindNameGlobbingWithTag(pkgName, _tag, _prerelease, _type, out errRecord);
                            tagsAsString = String.Join(", ", _tag);
                        }

                        if (errRecord != null)
                        {
                            if (shouldReportErrorForEachRepo)
                            {
                                _cmdletPassedIn.WriteError(errRecord);
                            }
                            else
                            {
                                _cmdletPassedIn.WriteVerbose(errRecord.Exception.Message);
                            }

                            continue;
                        }

                        foreach (PSResourceResult currentResult in currentResponseUtil.ConvertToPSResourceResult(responses))
                        {
                            if (currentResult.exception != null && !currentResult.exception.Message.Equals(string.Empty))
                            {
                                _cmdletPassedIn.WriteError(new ErrorRecord(
                                    currentResult.exception, 
                                    "FindNameGlobbingConvertToPSResourceFailure", 
                                    ErrorCategory.InvalidResult, 
                                    this));

                                continue;
                            }

                            PSResourceInfo foundPkg = currentResult.returnedObject;
                            parentPkgs.Add(foundPkg);
                            pkgsFound.Add(String.Format("{0}{1}", foundPkg.Name, foundPkg.Version.ToString()));
                            yield return foundPkg;
                        }
                    }
                    else
                    {
                        // Example: Find-PSResource -Name "Az"
                        // Example: Find-PSResource -Name "Az" -Tag "Storage"
                        _cmdletPassedIn.WriteDebug("No version specified, package name is specified");

                        FindResults responses = null;
                        if (_tag.Length == 0)
                        {
                            responses = currentServer.FindName(pkgName, _prerelease, _type, out errRecord);
                        }
                        else
                        {
                            responses = currentServer.FindNameWithTag(pkgName, _tag, _prerelease, _type, out errRecord);
                            tagsAsString = String.Join(", ", _tag);
                        }

                        if (errRecord != null)
                        {
                            if (shouldReportErrorForEachRepo)
                            {
                                _cmdletPassedIn.WriteError(errRecord);
                            }
                            else
                            {
                                _cmdletPassedIn.WriteVerbose(errRecord.Exception.Message);
                            }

                            continue;
                        }

                        PSResourceResult currentResult = currentResponseUtil.ConvertToPSResourceResult(responses).First();
                        if (currentResult.exception != null && !currentResult.exception.Message.Equals(string.Empty))
                        {
                            _cmdletPassedIn.WriteError(new ErrorRecord(
                                currentResult.exception, 
                                "FindNameConvertToPSResourceFailure", 
                                ErrorCategory.ObjectNotFound, 
                                this));

                            continue;
                        }

                        PSResourceInfo foundPkg = currentResult.returnedObject;
                        parentPkgs.Add(foundPkg);
                        pkgsFound.Add(String.Format("{0}{1}", foundPkg.Name, foundPkg.Version.ToString()));
                        yield return foundPkg;
                    }
                }
                else if (_versionType == VersionType.SpecificVersion)
                {
                    if (pkgName.Contains("*"))
                    {
                        _cmdletPassedIn.WriteError(new ErrorRecord(
                            new ArgumentException("Name cannot contain or equal wildcard when using specific version."),
                            "InvalidWildCardUsage", 
                            ErrorCategory.InvalidOperation, 
                            this));

                        continue;
                    }
                    else
                    {
                        // Example: Find-PSResource -Name "Az" -Version "3.0.0.0"
                        // Example: Find-PSResource -Name "Az" -Version "3.0.0.0" -Tag "Windows"
                        _cmdletPassedIn.WriteDebug("Exact version and package name are specified");

                        FindResults responses = null;
                        if (_tag.Length == 0)
                        {
                            responses = currentServer.FindVersion(pkgName, _nugetVersion.ToNormalizedString(), _type, out errRecord);
                        }
                        else
                        {
                            responses = currentServer.FindVersionWithTag(pkgName, _nugetVersion.ToNormalizedString(), _tag, _type, out errRecord);
                            tagsAsString = String.Join(", ", _tag);
                        }

                        if (errRecord != null)
                        {
                            if (shouldReportErrorForEachRepo)
                            {
                                _cmdletPassedIn.WriteError(errRecord);
                            }
                            else
                            {
                                _cmdletPassedIn.WriteVerbose(errRecord.Exception.Message);
                            }

                            continue;
                        }

                        PSResourceResult currentResult = currentResponseUtil.ConvertToPSResourceResult(responses).First();
                        if (currentResult.exception != null && !currentResult.exception.Message.Equals(string.Empty))
                        {
                            _cmdletPassedIn.WriteError(new ErrorRecord(
                                currentResult.exception,
                                "FindVersionConvertToPSResourceFailure", 
                                ErrorCategory.ObjectNotFound, 
                                this));
                            
                            continue;
                        }

                        PSResourceInfo foundPkg = currentResult.returnedObject;
                        parentPkgs.Add(foundPkg);
                        pkgsFound.Add(String.Format("{0}{1}", foundPkg.Name, foundPkg.Version.ToString()));
                        yield return foundPkg;
                    }
                }
                else
                {
                    // version type is Version Range
                    if (pkgName.Contains("*"))
                    {
                        _cmdletPassedIn.WriteError(new ErrorRecord(
                            new ArgumentException("Name cannot contain or equal wildcard when using version range"),
                            "InvalidWildCardUsage", 
                            ErrorCategory.InvalidOperation, 
                            this));
                    }
                    else
                    {
                        // Example: Find-PSResource -Name "Az" -Version "[1.0.0.0, 3.0.0.0]"
                        _cmdletPassedIn.WriteDebug("Version range and package name are specified");

                        FindResults responses = null;
                        if (_tag.Length == 0)
                        {
                            responses = currentServer.FindVersionGlobbing(pkgName, _versionRange, _prerelease, _type, getOnlyLatest: false, out errRecord);
                        }
                        else
                        {
                            _cmdletPassedIn.WriteError(new ErrorRecord(
                                new ArgumentException("-Tag parameter cannot be specified when using version range."),
                                "InvalidWildCardUsage", 
                                ErrorCategory.InvalidOperation,
                                this));

                            continue;
                        }

                        if (errRecord != null)
                        {
                            if (shouldReportErrorForEachRepo)
                            {
                                _cmdletPassedIn.WriteError(errRecord);
                            }
                            else
                            {
                                _cmdletPassedIn.WriteVerbose(errRecord.Exception.Message);
                            }

                            continue;
                        }

                        foreach (PSResourceResult currentResult in currentResponseUtil.ConvertToPSResourceResult(responses))
                        {
                            // This currently catches for V2ServerApiCalls where the package was not found
                            if (currentResult.exception != null && !currentResult.exception.Message.Equals(string.Empty))
                            {
                                _cmdletPassedIn.WriteError(new ErrorRecord(
                                    currentResult.exception,
                                    "FindVersionGlobbingConvertToPSResourceFailure", 
                                    ErrorCategory.ObjectNotFound, 
                                    this));

                                continue;
                            }

                            // Check to see if version falls within version range 
                            PSResourceInfo foundPkg = currentResult.returnedObject;
                            string versionStr = $"{foundPkg.Version}";
                            if (foundPkg.IsPrerelease)
                            {
                                versionStr += $"-{foundPkg.Prerelease}";
                            }

                            if (NuGetVersion.TryParse(versionStr, out NuGetVersion version)
                                   && _versionRange.Satisfies(version))
                            {
                                parentPkgs.Add(foundPkg);
                                pkgsFound.Add(String.Format("{0}{1}", foundPkg.Name, foundPkg.Version.ToString()));

                                yield return foundPkg;
                            }
                        }
                    }
                }
            }

            // After retrieving all packages find their dependencies
            if (_includeDependencies)
            {
                if (currentServer.Repository.ApiVersion == PSRepositoryInfo.APIVersion.v3)
                {
                    _cmdletPassedIn.WriteWarning("Installing dependencies is not currently supported for V3 server protocol repositories. The package will be installed without installing dependencies.");
                    yield break;
                }

                foreach (PSResourceInfo currentPkg in parentPkgs)
                {
                    _cmdletPassedIn.WriteDebug($"Finding dependency packages for '{currentPkg.Name}'");
                    foreach (PSResourceInfo pkgDep in FindDependencyPackages(currentServer, currentResponseUtil, currentPkg, repository, pkgsFound))
                    {
                        yield return pkgDep;
                    }
                }
            }
        }

        /// <summary>
        /// Iterates over package names passed in by user, and populates non-wildcard names into a HashSet.
        /// Since we only write errors for non-wildcard names, this is used to track discovery and report errors.
        /// </summary>
        private HashSet<string> GetPackageNamesPopulated(string[] pkgNames)
        {
            HashSet<string> pkgsToDiscover = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string name in pkgNames)
            {
                if (!name.Contains("*"))
                {
                    pkgsToDiscover.Add(name);
                }
            }

            return pkgsToDiscover;
        }

        #endregion

        #region Internal Client Search Methods

        internal IEnumerable<PSResourceInfo> FindDependencyPackages(
            ServerApiCall currentServer,
            ResponseUtil currentResponseUtil,
            PSResourceInfo currentPkg,
            PSRepositoryInfo repository,
            HashSet<string> foundPkgs)
        {
            if (currentPkg.Dependencies.Length > 0)
            {
                foreach (var dep in currentPkg.Dependencies)
                {
                    PSResourceInfo depPkg = null;

                    if (dep.VersionRange.Equals(VersionRange.All))
                    {
                        FindResults responses = currentServer.FindName(dep.Name, includePrerelease: true, _type, out ErrorRecord errRecord);
                        if (errRecord != null)
                        {
                            if (errRecord.Exception is ResourceNotFoundException)
                            {
                                _cmdletPassedIn.WriteVerbose(errRecord.Exception.Message);
                            }
                            else {
                                _cmdletPassedIn.WriteError(errRecord);
                            }
                            yield return null;
                            continue;
                        }

                        PSResourceResult currentResult = currentResponseUtil.ConvertToPSResourceResult(responses).First();

                        if (currentResult.exception != null && !currentResult.exception.Message.Equals(string.Empty))
                        {
                            _cmdletPassedIn.WriteError(new ErrorRecord(
                                new ResourceNotFoundException($"Dependency package with name '{dep.Name}' could not be found in repository '{repository.Name}'", currentResult.exception), 
                                "DependencyPackageNotFound", 
                                ErrorCategory.ObjectNotFound, 
                                this));
                            yield return null;
                            continue;
                        }

                        depPkg = currentResult.returnedObject;
                        string pkgHashKey = String.Format("{0}{1}", depPkg.Name, depPkg.Version.ToString());

                        if (!foundPkgs.Contains(pkgHashKey))
                        {
                            foreach (PSResourceInfo depRes in FindDependencyPackages(currentServer, currentResponseUtil, depPkg, repository, foundPkgs))
                            {
                                yield return depRes;
                            }
                        }
                    }
                    else
                    {
                        FindResults responses = currentServer.FindVersionGlobbing(dep.Name, dep.VersionRange, includePrerelease: true, ResourceType.None, getOnlyLatest: true, out ErrorRecord errRecord);
                        if (errRecord != null)
                        {
                            if (errRecord.Exception is ResourceNotFoundException)
                            {
                                _cmdletPassedIn.WriteVerbose(errRecord.Exception.Message);
                            }
                            else {
                                _cmdletPassedIn.WriteError(errRecord);
                            }
                            yield return null;
                            continue;
                        }

                        if (responses.IsFindResultsEmpty())
                        {
                            _cmdletPassedIn.WriteError(new ErrorRecord(
                                new InvalidOrEmptyResponse($"Dependency package with name {dep.Name} and version range {dep.VersionRange} could not be found in repository '{repository.Name}"), 
                                "FindDepPackagesFindVersionGlobbingFailure", 
                                ErrorCategory.InvalidResult, 
                                this));
                            yield return null;
                            continue;
                        }

                        foreach (PSResourceResult currentResult in currentResponseUtil.ConvertToPSResourceResult(responses))
                        {
                            if (currentResult.exception != null && !currentResult.exception.Message.Equals(string.Empty))
                            {
                                _cmdletPassedIn.WriteError(new ErrorRecord(
                                    new ResourceNotFoundException($"Dependency package with name '{dep.Name}' and version range '{dep.VersionRange}' could not be found in repository '{repository.Name}'", currentResult.exception), 
                                    "DependencyPackageNotFound", 
                                    ErrorCategory.ObjectNotFound, 
                                    this));
                                
                                yield return null;
                                continue;
                            }

                            // Check to see if version falls within version range 
                            PSResourceInfo foundDep = currentResult.returnedObject;
                            string depVersionStr = $"{foundDep.Version}";
                            if (foundDep.IsPrerelease) {
                                depVersionStr += $"-{foundDep.Prerelease}";
                            }
                            
                            if (NuGetVersion.TryParse(depVersionStr, out NuGetVersion depVersion)
                                   && dep.VersionRange.Satisfies(depVersion))
                            {
                                depPkg = foundDep;
                            }
                        }

                        if (depPkg == null)
                        {
                            continue;
                        }
                        
                        string pkgHashKey = String.Format("{0}{1}", depPkg.Name, depPkg.Version.ToString());

                        if (!foundPkgs.Contains(pkgHashKey))
                        {
                            foreach (PSResourceInfo depRes in FindDependencyPackages(currentServer, currentResponseUtil, depPkg, repository, foundPkgs))
                            {
                                yield return depRes;
                            }
                        }
                    }
                }
            }

            string currentPkgHashKey = String.Format("{0}{1}", currentPkg.Name, currentPkg.Version.ToString());

            if (!foundPkgs.Contains(currentPkgHashKey))
            {
                yield return currentPkg;
            }
        }

        #endregion
    }
}
