// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// Find helper class provides the core functionality for FindPSResource.
    /// </summary>
    internal class FindHelper
    {
        #region Members

        private CancellationToken _cancellationToken;
        private readonly PSCmdlet _cmdletPassedIn;
        private List<string> _pkgsLeftToFind;
        private List<string> _tagsLeftToFind;
        private ResourceType _type;
        private string _version;
        private VersionRange _versionRange;
        private NuGetVersion _nugetVersion;
        private VersionType _versionType;
        private bool _prerelease = false;
        private string[] _tag;
        private bool _includeDependencies = false;
        private bool _repositoryNameContainsWildcard;
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
            bool includeDependencies)
        {
            _type = type;
            _version = version;
            _prerelease = prerelease;
            _tag = tag ?? Utils.EmptyStrArray;
            _includeDependencies = includeDependencies;
            _versionRange = versionRange;
            _nugetVersion = nugetVersion;
            _versionType = versionType;

            if (name.Length == 0)
            {
                yield break;
            }

            _pkgsLeftToFind = new List<string>(name);
            _tagsLeftToFind = tag == null ? new List<string>() : new List<string>(tag);

            // Error out if repository array of names to be searched contains wildcards.
            if (repository != null)
            {
                repository = Utils.ProcessNameWildcards(repository, removeWildcardEntries:false, out string[] errorMsgs, out _repositoryNameContainsWildcard);
                foreach (string error in errorMsgs)
                {
                    _cmdletPassedIn.WriteError(new ErrorRecord(
                        new PSInvalidOperationException(error),
                        "ErrorFilteringNamesForUnsupportedWildcards",
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
                        "ErrorGettingSpecifiedRepo",
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

            for (int i = 0; i < repositoriesToSearch.Count && _pkgsLeftToFind.Any(); i++)
            {
                PSRepositoryInfo currentRepository = repositoriesToSearch[i];
                SetNetworkCredential(currentRepository);
                ServerApiCall currentServer = ServerFactory.GetServer(currentRepository, _networkCredential);
                ResponseUtil currentResponseUtil = ResponseUtilFactory.GetResponseUtil(currentRepository);

                _cmdletPassedIn.WriteVerbose(string.Format("Searching in repository {0}", repositoriesToSearch[i].Name));

                foreach (PSResourceInfo currentPkg in SearchByNames(currentServer, currentResponseUtil, currentRepository))
                {
                    yield return currentPkg;
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

            List<string> cmdsLeftToFind = new List<string>(tag);

            if (tag.Length == 0)
            {
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
                        "ErrorGettingSpecifiedRepo",
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

            for (int i = 0; i < repositoriesToSearch.Count; i++)
            {
                PSRepositoryInfo currentRepository = repositoriesToSearch[i];
                
                SetNetworkCredential(currentRepository);
                ServerApiCall currentServer = ServerFactory.GetServer(currentRepository, _networkCredential);
                ResponseUtil currentResponseUtil = ResponseUtilFactory.GetResponseUtil(currentRepository);

                _cmdletPassedIn.WriteVerbose(string.Format("Searching in repository {0}", repositoriesToSearch[i].Name));

                FindResults responses = currentServer.FindCommandOrDscResource(tag, _prerelease, isSearchingForCommands, out ExceptionDispatchInfo edi);
                if (edi != null)
                {
                    _cmdletPassedIn.WriteError(new ErrorRecord(edi.SourceException, "FindCommandOrDSCResourceFail", ErrorCategory.InvalidOperation, this));
                    continue;
                }

                foreach (PSResourceResult currentResult in currentResponseUtil.ConvertToPSResourceResult(responses))
                {
                    if (!String.IsNullOrEmpty(currentResult.errorMsg))
                    {
                        _cmdletPassedIn.WriteError(new ErrorRecord(new PSInvalidOperationException(currentResult.errorMsg), "FindCmdOrDSCResponseConversionFail", ErrorCategory.NotSpecified, this));
                        continue;
                    }

                    PSCommandResourceInfo currentCmdPkg = new PSCommandResourceInfo(tag, currentResult.returnedObject);
                    yield return currentCmdPkg;
                }
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

            _tagsLeftToFind = new List<string>(tag);

            if (tag.Length == 0)
            {
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
                        "ErrorGettingSpecifiedRepo",
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

            for (int i = 0; i < repositoriesToSearch.Count && _tagsLeftToFind.Any(); i++)
            {
                PSRepositoryInfo currentRepository = repositoriesToSearch[i];
                SetNetworkCredential(currentRepository);
                ServerApiCall currentServer = ServerFactory.GetServer(currentRepository, _networkCredential);
                ResponseUtil currentResponseUtil = ResponseUtilFactory.GetResponseUtil(currentRepository);

                if (_type != ResourceType.None && repositoriesToSearch[i].Name != "PSGallery")
                {
                    _cmdletPassedIn.ThrowTerminatingError(new ErrorRecord(
                        new PSInvalidOperationException("-Type parameter is only supported with the PowerShellGallery."),
                        "ErrorUsingTypeParameter",
                        ErrorCategory.InvalidOperation,
                        this));
                }

                FindResults responses = currentServer.FindTags(_tag, _prerelease, type, out ExceptionDispatchInfo edi);

                if (edi != null)
                {
                    _cmdletPassedIn.WriteError(new ErrorRecord(edi.SourceException, "FindTagFail", ErrorCategory.InvalidOperation, this));
                    continue;
                }

                foreach (PSResourceResult currentResult in currentResponseUtil.ConvertToPSResourceResult(responses))
                {
                    if (!String.IsNullOrEmpty(currentResult.errorMsg))
                    {
                        string errMsg = $"Tags: {String.Join(", ", _tag)} could not be found due to: {currentResult.errorMsg}";
                        _cmdletPassedIn.WriteError(new ErrorRecord(new PSInvalidOperationException(errMsg), "FindTagResponseConversionFail", ErrorCategory.NotSpecified, this));
                        continue;
                    }

                    yield return currentResult.returnedObject;
                }
            }
        }

        #endregion

        #region Private Client Search Methods

        private IEnumerable<PSResourceInfo> SearchByNames(ServerApiCall currentServer, ResponseUtil currentResponseUtil, PSRepositoryInfo repository)
        {
            ExceptionDispatchInfo edi = null;
            List<PSResourceInfo> parentPkgs = new List<PSResourceInfo>();
            HashSet<string> pkgsFound = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string pkgName in _pkgsLeftToFind.ToArray())
            {
                if (_versionType == VersionType.NoVersion)
                {
                    if (pkgName.Trim().Equals("*"))
                    {
                        // Example: Find-PSResource -Name "*"
                        FindResults responses = currentServer.FindAll(_prerelease, _type, out edi);
                        if (edi != null)
                        {
                            _cmdletPassedIn.WriteError(new ErrorRecord(edi.SourceException, "FindAllFail", ErrorCategory.InvalidOperation, this));
                            continue;
                        }

                        foreach (PSResourceResult currentResult in currentResponseUtil.ConvertToPSResourceResult(responseResults: responses))
                        {
                            if (!String.IsNullOrEmpty(currentResult.errorMsg))
                            {
                                string errMsg = $"Package with search criteria: Name {pkgName} could not be found due to: {currentResult.errorMsg}.";
                                _cmdletPassedIn.WriteError(new ErrorRecord(new PSInvalidOperationException(errMsg), "FindAllResponseConversionFail", ErrorCategory.NotSpecified, this));
                                continue;
                            }

                            PSResourceInfo foundPkg = currentResult.returnedObject;
                            parentPkgs.Add(foundPkg);
                            pkgsFound.Add(String.Format("{0}{1}", foundPkg.Name, foundPkg.Version.ToString()));
                            yield return foundPkg;
                        }
                    }
                    else if(pkgName.Contains("*"))
                    {
                        // Example: Find-PSResource -Name "Az*"
                        // Example: Find-PSResource -Name "Az*" -Tag "Storage"
                        string tagMsg = String.Empty;
                        FindResults responses = null;
                        if (_tag.Length == 0)
                        {
                            responses = currentServer.FindNameGlobbing(pkgName, _prerelease, _type, out edi);
                        }
                        else
                        {
                            responses = currentServer.FindNameGlobbingWithTag(pkgName, _tag, _prerelease, _type, out edi);
                            string tagsAsString = String.Join(", ", _tag);
                            tagMsg = $" and Tags {tagsAsString}";
                        }

                        if (edi != null)
                        {
                            _cmdletPassedIn.WriteError(new ErrorRecord(edi.SourceException, "FindNameGlobbingFail", ErrorCategory.InvalidOperation, this));
                            continue;
                        }

                        foreach (PSResourceResult currentResult in currentResponseUtil.ConvertToPSResourceResult(responses))
                        {
                            if (!String.IsNullOrEmpty(currentResult.errorMsg))
                            {
                                string errMsg = $"Package with search criteria: Name {pkgName}{tagMsg} could not be found due to: {currentResult.errorMsg} originating at method: FindNameGlobbingResponseConversionFail().";
                                _cmdletPassedIn.WriteWarning(errMsg);
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
                        string tagMsg = String.Empty;
                        FindResults responses = null;
                        if (_tag.Length == 0)
                        {
                            responses = currentServer.FindName(pkgName, _prerelease, _type, out edi);
                        }
                        else
                        {
                            responses = currentServer.FindNameWithTag(pkgName, _tag, _prerelease, _type, out edi);
                            string tagsAsString = String.Join(", ", _tag);
                            tagMsg = $" and Tags {tagsAsString}";
                        }

                        if (edi != null)
                        {
                            _cmdletPassedIn.WriteError(new ErrorRecord(edi.SourceException, "FindNameFail", ErrorCategory.InvalidOperation, this));
                            continue;
                        }

                        PSResourceResult currentResult = currentResponseUtil.ConvertToPSResourceResult(responses).First();
                        
                        if (!String.IsNullOrEmpty(currentResult.errorMsg))
                        {
                            string errMsg = $"Package with search criteria: Name {pkgName}{tagMsg} could not be found due to: {currentResult.errorMsg}.";
                            _cmdletPassedIn.WriteError(new ErrorRecord(new PSInvalidOperationException(errMsg), "FindNameResponseConversionFail", ErrorCategory.NotSpecified, this));
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
                        var exMessage = "Name cannot contain or equal wildcard when using specific version.";
                        var ex = new ArgumentException(exMessage);
                        var WildcardError = new ErrorRecord(ex, "InvalidWildCardUsage", ErrorCategory.InvalidOperation, null);
                        _cmdletPassedIn.WriteError(WildcardError);

                        continue;
                    }
                    else
                    {
                        // Example: Find-PSResource -Name "Az" -Version "3.0.0.0"
                        // Example: Find-PSResource -Name "Az" -Version "3.0.0.0" -Tag "Windows"
                        FindResults responses = null;
                        string tagMsg = String.Empty;
                        if (_tag.Length == 0)
                        {
                            responses = currentServer.FindVersion(pkgName, _nugetVersion.ToNormalizedString(), _type, out edi);
                        }
                        else
                        {
                            responses = currentServer.FindVersionWithTag(pkgName, _nugetVersion.ToNormalizedString(), _tag, _type, out edi);
                            string tagsAsString = String.Join(", ", _tag);
                            tagMsg = $" and Tags {tagsAsString}";
                        }

                        if (edi != null)
                        {
                            _cmdletPassedIn.WriteError(new ErrorRecord(edi.SourceException, "FindVersionFail", ErrorCategory.InvalidOperation, this));
                            continue;
                        }

                        PSResourceResult currentResult = currentResponseUtil.ConvertToPSResourceResult(responses).First();
                        
                        if (!String.IsNullOrEmpty(currentResult.errorMsg))
                        {
                            string errMsg = $"Package with search criteria: Name {pkgName}, Version {_version} {tagMsg} could not be found due to: {currentResult.errorMsg}.";
                            _cmdletPassedIn.WriteError(new ErrorRecord(new PSInvalidOperationException(errMsg), "FindVersionResponseConversionFail", ErrorCategory.NotSpecified, this));
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
                        var exMessage = "Name cannot contain or equal wildcard when using version range";
                        var ex = new ArgumentException(exMessage);
                        var WildcardError = new ErrorRecord(ex, "InvalidWildCardUsage", ErrorCategory.InvalidOperation, null);
                        _cmdletPassedIn.WriteError(WildcardError);
                    }
                    else
                    {
                        // Example: Find-PSResource -Name "Az" -Version "[1.0.0.0, 3.0.0.0]"
                        FindResults responses = null;
                        if (_tag.Length == 0)
                        {
                            responses = currentServer.FindVersionGlobbing(pkgName, _versionRange, _prerelease, _type, getOnlyLatest: false, out edi);
                        }
                        else
                        {
                            var exMessage = "Name cannot contain or equal wildcard when using version range";
                            var ex = new ArgumentException(exMessage);
                            var WildcardError = new ErrorRecord(ex, "InvalidWildCardUsage", ErrorCategory.InvalidOperation, null);
                            _cmdletPassedIn.WriteError(WildcardError);
                            continue;
                        }

                        if (edi != null)
                        {
                            _cmdletPassedIn.WriteError(new ErrorRecord(edi.SourceException, "FindVersionGlobbingFail", ErrorCategory.InvalidOperation, this));
                            continue;
                        }

                        foreach (PSResourceResult currentResult in currentResponseUtil.ConvertToPSResourceResult(responses))
                        {
                            if (!String.IsNullOrEmpty(currentResult.errorMsg))
                            {
                                string errMsg = $"Package with search criteria: Name {pkgName} and Version {_version} could not be found due to: {currentResult.errorMsg} originating at method FindVersionGlobbingResponseConversionFail().";
                                _cmdletPassedIn.WriteWarning(errMsg);
                                continue;
                            }

                            PSResourceInfo foundPkg = currentResult.returnedObject;
                            parentPkgs.Add(foundPkg);
                            pkgsFound.Add(String.Format("{0}{1}", foundPkg.Name, foundPkg.Version.ToString()));
                            yield return foundPkg;
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
                    foreach (PSResourceInfo pkgDep in FindDependencyPackages(currentServer, currentResponseUtil, currentPkg, repository, pkgsFound))
                    {
                        yield return pkgDep;
                    }
                }
            }
        }

        private void SetNetworkCredential(PSRepositoryInfo repository)
        {
            // Explicitly passed in Credential takes precedence over repository CredentialInfo.
            if (_networkCredential == null && repository.CredentialInfo != null)
            {
                PSCredential repoCredential = Utils.GetRepositoryCredentialFromSecretManagement(
                    repository.Name,
                    repository.CredentialInfo,
                    _cmdletPassedIn);

                _networkCredential = new NetworkCredential(repoCredential.UserName, repoCredential.Password);

                _cmdletPassedIn.WriteVerbose("credential successfully read from vault and set for repository: " + repository.Name);
            }
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

                    if (dep.VersionRange == VersionRange.All)
                    {
                        FindResults responses = currentServer.FindName(dep.Name, _prerelease, _type, out ExceptionDispatchInfo edi);
                        if (edi != null)
                        {
                            _cmdletPassedIn.WriteError(new ErrorRecord(edi.SourceException, "HttpFindDepPackagesFindNameFail", ErrorCategory.InvalidOperation, this));
                            yield return null;
                            continue;
                        }

                        PSResourceResult currentResult = currentResponseUtil.ConvertToPSResourceResult(responses).First();

                        if (!String.IsNullOrEmpty(currentResult.errorMsg))
                        {
                            _cmdletPassedIn.WriteError(new ErrorRecord(new PSInvalidOperationException(currentResult.errorMsg), "FindNameForDepResponseConversionFail", ErrorCategory.NotSpecified, this));
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
                        FindResults responses = currentServer.FindVersionGlobbing(dep.Name, dep.VersionRange, _prerelease, ResourceType.None, getOnlyLatest: true, out ExceptionDispatchInfo edi);
                        if (edi != null)
                        {
                            _cmdletPassedIn.WriteError(new ErrorRecord(edi.SourceException, "HttpFindDepPackagesFindVersionGlobbingFail", ErrorCategory.InvalidOperation, this));
                            yield return null;
                            continue;
                        }

                        if (responses.IsFindResultsEmpty())
                        {
                            _cmdletPassedIn.WriteError(new ErrorRecord(new InvalidOrEmptyResponse($"Dependency package with Name {dep.Name} and VersionRange {dep.VersionRange} could not be found in this repository."), "HttpFindDepPackagesFindVersionGlobbingFail", ErrorCategory.InvalidOperation, this));
                            yield return null;
                            continue;
                        }

                        foreach (PSResourceResult currentResult in currentResponseUtil.ConvertToPSResourceResult(responses))
                        {
                            if (!String.IsNullOrEmpty(currentResult.errorMsg))
                            {
                                _cmdletPassedIn.WriteError(new ErrorRecord(new PSInvalidOperationException(currentResult.errorMsg), "FindVersionGlobbingForDepResponseConversionFail", ErrorCategory.NotSpecified, this));
                                yield return null;
                                continue;
                            }

                            depPkg = currentResult.returnedObject;
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
