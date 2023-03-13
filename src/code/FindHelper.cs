// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using MoreLinq.Extensions;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Net.Http;
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
        private SwitchParameter _prerelease = false;
        private string[] _tag;
        private SwitchParameter _includeDependencies = false;
        private readonly string _psGalleryRepoName = "PSGallery";
        private readonly string _psGalleryScriptsRepoName = "PSGalleryScripts";
        private readonly string _poshTestGalleryRepoName = "PoshTestGallery";
        private bool _repositoryNameContainsWildcard;
        private NetworkCredential _networkCredential;

        // NuGet's SearchAsync() API takes a top parameter of 6000, but testing shows for PSGallery
        // usually a max of around 5990 is returned while more are left to retrieve in a second SearchAsync() call
        private const int SearchAsyncMaxTake = 6000;
        private const int SearchAsyncMaxReturned = 5990;
        private const int GalleryMax = 12000;

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

        #region Public methods

        public IEnumerable<PSResourceInfo> FindByResourceName(
            string[] name,
            ResourceType type,
            VersionRange versionRange,
            NuGetVersion nugetVersion,
            VersionType versionType,
            string version,
            SwitchParameter prerelease,
            string[] tag,
            string[] repository,
            SwitchParameter includeDependencies)
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
                if (repository != null && repositoriesToSearch.Count == 0)
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
                if (repositoriesToSearch[i].ApiVersion == PSRepositoryInfo.APIVersion.local)
                {
                    foreach (PSResourceInfo currentPkg in SearchFromLocalRepository(repositoriesToSearch[i]))
                    {
                        yield return currentPkg;
                    }
                }
                else
                {
                    PSRepositoryInfo currentRepository = repositoriesToSearch[i];

                    // Explicitly passed in Credential takes precedence over repository CredentialInfo.
                    if (_networkCredential == null && currentRepository.CredentialInfo != null)
                    {
                        PSCredential repoCredential = Utils.GetRepositoryCredentialFromSecretManagement(
                            currentRepository.Name,
                            currentRepository.CredentialInfo,
                            _cmdletPassedIn);

                        var username = repoCredential.UserName;
                        var password = repoCredential.Password;

                        _networkCredential = new NetworkCredential(username, password);

                        _cmdletPassedIn.WriteVerbose("credential successfully read from vault and set for repository: " + currentRepository.Name);
                    }

                    ServerApiCall currentServer = ServerFactory.GetServer(currentRepository, _networkCredential);
                    ResponseUtil currentResponseUtil = ResponseUtilFactory.GetResponseUtil(currentRepository);

                    _cmdletPassedIn.WriteVerbose(string.Format("Searching in repository {0}", repositoriesToSearch[i].Name));


                    foreach (PSResourceInfo currentPkg in SearchByNames(currentServer, currentResponseUtil, currentRepository))
                    {
                        yield return currentPkg;
                    }
                }
            }
        }

        public IEnumerable<PSCommandResourceInfo> FindCommandOrDscResource(
            bool isSearchingForCommands,
            SwitchParameter prerelease,
            string[] tag,
            string[] repository,
            PSCredential credential)
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
                if (repository != null && repositoriesToSearch.Count == 0)
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

            for (int i = 0; i < repositoriesToSearch.Count && cmdsLeftToFind.Any(); i++)
            {
                PSRepositoryInfo currentRepository = repositoriesToSearch[i];
                
                if (repositoriesToSearch[i].ApiVersion == PSRepositoryInfo.APIVersion.local)
                {
                    _pkgsLeftToFind = new List<string>() { "*" };

                    var potentialMatches = SearchFromLocalRepository(repositoriesToSearch[i]);
                    foreach (string cmdOrDsc in tag)
                    {
                        foreach (var package in potentialMatches)
                        {
                            // this check ensures DSC names provided as a Command name won't get returned mistakenly
                            // -CommandName "command1", "dsc1" <- (will not return or add DSC name)
                            if ((isSearchingForCommands && package.Includes.Command.Contains(cmdOrDsc, StringComparer.OrdinalIgnoreCase)) ||
                                (!isSearchingForCommands && package.Includes.DscResource.Contains(cmdOrDsc, StringComparer.OrdinalIgnoreCase)))
                            {
                                yield return new PSCommandResourceInfo(new string[] { cmdOrDsc }, package);
                            }
                        }
                    }
                }
                else
                {
                    // Explicitly passed in Credential takes precedence over repository CredentialInfo.
                    if (_networkCredential == null && currentRepository.CredentialInfo != null)
                    {
                        PSCredential repoCredential = Utils.GetRepositoryCredentialFromSecretManagement(
                            currentRepository.Name,
                            currentRepository.CredentialInfo,
                            _cmdletPassedIn);

                        var username = repoCredential.UserName;
                        var password = repoCredential.Password;

                        _networkCredential = new NetworkCredential(username, password);

                        _cmdletPassedIn.WriteVerbose("credential successfully read from vault and set for repository: " + currentRepository.Name);
                    }

                    ServerApiCall currentServer = ServerFactory.GetServer(currentRepository, _networkCredential);
                    ResponseUtil currentResponseUtil = ResponseUtilFactory.GetResponseUtil(currentRepository);

                    _cmdletPassedIn.WriteVerbose(string.Format("Searching in repository {0}", repositoriesToSearch[i].Name));

                    foreach (string currentCmdOrDSC in tag)
                    {
                        string[] responses = currentServer.FindCommandOrDscResource(currentCmdOrDSC, _prerelease, isSearchingForCommands, out ExceptionDispatchInfo edi);
                        if (edi != null)
                        {
                            _cmdletPassedIn.WriteError(new ErrorRecord(edi.SourceException, "FindCommandOrDSCResourceFail", ErrorCategory.InvalidOperation, this));
                            continue;
                        }

                        foreach (PSResourceResult currentResult in currentResponseUtil.ConvertToPSResourceResult(responses: responses))
                        {
                            if (!String.IsNullOrEmpty(currentResult.errorMsg))
                            {
                                _cmdletPassedIn.WriteError(new ErrorRecord(new PSInvalidOperationException(currentResult.errorMsg), "FindCmdOrDSCResponseConversionFail", ErrorCategory.NotSpecified, this));
                                continue;
                            }

                            cmdsLeftToFind.Remove(currentCmdOrDSC);
                            yield return currentResult.returnedCmdObject;
                        }
                    }
                }
            }
        }

        public IEnumerable<PSResourceInfo> FindTag(
            ResourceType type,
            SwitchParameter prerelease,
            string[] tag,
            string[] repository,
            PSCredential credential)
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
                if (repository != null && repositoriesToSearch.Count == 0)
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

                if (repositoriesToSearch[i].ApiVersion == PSRepositoryInfo.APIVersion.local)
                {
                    _pkgsLeftToFind = new List<string>() { "*" };
                    _tag = tag;

                    foreach (var package in SearchFromLocalRepository(repositoriesToSearch[i]))
                    {
                        yield return package;
                    }
                }
                else
                {
                    // Explicitly passed in Credential takes precedence over repository CredentialInfo.
                    if (_networkCredential == null && currentRepository.CredentialInfo != null)
                    {
                        PSCredential repoCredential = Utils.GetRepositoryCredentialFromSecretManagement(
                            currentRepository.Name,
                            currentRepository.CredentialInfo,
                            _cmdletPassedIn);

                        var username = repoCredential.UserName;
                        var password = repoCredential.Password;

                        _networkCredential = new NetworkCredential(username, password);

                        _cmdletPassedIn.WriteVerbose("credential successfully read from vault and set for repository: " + currentRepository.Name);
                    }


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

                    foreach (string currentTag in _tag)
                    {
                        // currentServer.FindTag() -> return string[] responses
                        // loop thru responses
                        // convert each response to PSResourceResult

                        string[] responses = currentServer.FindTag(currentTag, _prerelease, type, out ExceptionDispatchInfo edi);

                        if (edi != null)
                        {
                            _cmdletPassedIn.WriteError(new ErrorRecord(edi.SourceException, "FindTagFail", ErrorCategory.InvalidOperation, this));
                            continue;
                        }

                        foreach (PSResourceResult currentResult in currentResponseUtil.ConvertToPSResourceResult(responses: responses))
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
            }
        }

        #endregion

        #region Private HTTP methods

        private IEnumerable<PSResourceInfo> SearchByNames(ServerApiCall currentServer, ResponseUtil currentResponseUtil, PSRepositoryInfo repository)
        {
            ExceptionDispatchInfo edi = null;
            List<PSResourceInfo> parentPkgs = new List<PSResourceInfo>();

            foreach (string pkgName in _pkgsLeftToFind.ToArray())
            {
                if (_versionType == VersionType.NoVersion)
                {

                    if (pkgName.Trim().Equals("*"))
                    {
                        // Example: Find-PSResource -Name "*"
                        string[] responses = currentServer.FindAll(_prerelease, _type, out edi);
                        if (edi != null)
                        {
                            _cmdletPassedIn.WriteError(new ErrorRecord(edi.SourceException, "FindAllFail", ErrorCategory.InvalidOperation, this));
                            continue;
                        }

                        foreach (PSResourceResult currentResult in currentResponseUtil.ConvertToPSResourceResult(responses: responses))
                        {
                            if (!String.IsNullOrEmpty(currentResult.errorMsg))
                            {
                                string errMsg = $"Package with search criteria: Name {pkgName} could not be found due to: {currentResult.errorMsg}.";
                                _cmdletPassedIn.WriteError(new ErrorRecord(new PSInvalidOperationException(errMsg), "FindAllResponseConversionFail", ErrorCategory.NotSpecified, this));
                                continue;
                            }

                            PSResourceInfo foundPkg = currentResult.returnedObject;
                            parentPkgs.Add(foundPkg);
                            yield return foundPkg;  
                        }
                    }
                    else if(pkgName.Contains("*"))
                    {
                        // Example: Find-PSResource -Name "Az*"
                        // Example: Find-PSResource -Name "Az*" -Tag "Storage"
                        string tagMsg = String.Empty;
                        string[] responses = Utils.EmptyStrArray;
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

                        foreach (PSResourceResult currentResult in currentResponseUtil.ConvertToPSResourceResult(responses: responses))
                        {
                            if (!String.IsNullOrEmpty(currentResult.errorMsg))
                            {
                                string errMsg = $"Package with search criteria: Name {pkgName}{tagMsg} could not be found due to: {currentResult.errorMsg} originating at method: FindNameGlobbingResponseConversionFail().";
                                _cmdletPassedIn.WriteWarning(errMsg);
                                continue;
                            }

                            PSResourceInfo foundPkg = currentResult.returnedObject;
                            parentPkgs.Add(foundPkg);
                            yield return foundPkg;                    
                        }
                    }
                    else
                    {
                        // Example: Find-PSResource -Name "Az"
                        // Example: Find-PSResource -Name "Az" -Tag "Storage"
                        string tagMsg = String.Empty;
                        string response = String.Empty;
                        if (_tag.Length == 0)
                        {
                            response = currentServer.FindName(pkgName, _prerelease, _type, out edi);
                        }
                        else
                        {
                            response = currentServer.FindNameWithTag(pkgName, _tag, _prerelease, _type, out edi);
                            string tagsAsString = String.Join(", ", _tag);
                            tagMsg = $" and Tags {tagsAsString}";
                        }

                        string[] responses = new string[]{ response };
                        if (edi != null)
                        {
                            _cmdletPassedIn.WriteError(new ErrorRecord(edi.SourceException, "FindNameFail", ErrorCategory.InvalidOperation, this));
                            continue;
                        }

                        PSResourceResult currentResult = currentResponseUtil.ConvertToPSResourceResult(responses: responses).First();
                        
                        if (!String.IsNullOrEmpty(currentResult.errorMsg))
                        {
                            string errMsg = $"Package with search criteria: Name {pkgName}{tagMsg} could not be found due to: {currentResult.errorMsg}.";
                            _cmdletPassedIn.WriteError(new ErrorRecord(new PSInvalidOperationException(errMsg), "FindNameResponseConversionFail", ErrorCategory.NotSpecified, this));
                            continue;
                        }

                        PSResourceInfo foundPkg = currentResult.returnedObject;
                        parentPkgs.Add(foundPkg);
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
                        string response = String.Empty;
                        string tagMsg = String.Empty;
                        if (_tag.Length == 0)
                        {
                            response = currentServer.FindVersion(pkgName, _nugetVersion.ToNormalizedString(), _type, out edi);
                        }
                        else
                        {
                            response = currentServer.FindVersionWithTag(pkgName, _nugetVersion.ToNormalizedString(), _tag, _type, out edi);
                            string tagsAsString = String.Join(", ", _tag);
                            tagMsg = $" and Tags {tagsAsString}";
                        }

                        string[] responses = new string[]{ response };
                        if (edi != null)
                        {
                            _cmdletPassedIn.WriteError(new ErrorRecord(edi.SourceException, "FindVersionFail", ErrorCategory.InvalidOperation, this));
                            continue;
                        }

                        PSResourceResult currentResult = currentResponseUtil.ConvertToPSResourceResult(responses: responses).First();
                        
                        if (!String.IsNullOrEmpty(currentResult.errorMsg))
                        {
                            string errMsg = $"Package with search criteria: Name {pkgName}, Version {_version} {tagMsg} could not be found due to: {currentResult.errorMsg}.";
                            _cmdletPassedIn.WriteError(new ErrorRecord(new PSInvalidOperationException(errMsg), "FindVersionResponseConversionFail", ErrorCategory.NotSpecified, this));
                            continue;
                        }

                        PSResourceInfo foundPkg = currentResult.returnedObject;
                        parentPkgs.Add(foundPkg);
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

                        string[] responses = Utils.EmptyStrArray;
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

                        foreach (PSResourceResult currentResult in currentResponseUtil.ConvertToPSResourceResult(responses: responses))
                        {
                            if (!String.IsNullOrEmpty(currentResult.errorMsg))
                            {
                                string errMsg = $"Package with search criteria: Name {pkgName} and Version {_version} could not be found due to: {currentResult.errorMsg} originating at method FindVersionGlobbingResponseConversionFail().";
                                _cmdletPassedIn.WriteWarning(errMsg);
                                continue;
                            }

                            PSResourceInfo foundPkg = currentResult.returnedObject;
                            parentPkgs.Add(foundPkg);
                            yield return foundPkg;                    
                        }
                    }
                }
            }

            // After retrieving all packages
            if (_includeDependencies)
            {
                if (currentServer.repository.ApiVersion == PSRepositoryInfo.APIVersion.v3)
                {
                    _cmdletPassedIn.WriteWarning("Installing dependencies is not currently supported for V3 server protocol repositories. The package will be installed without installing dependencies.");
                    yield break;
                }

                foreach (PSResourceInfo currentPkg in parentPkgs)
                {
                    // Actually find and return the dependency packages
                    foreach (PSResourceInfo pkgDep in HttpFindDependencyPackages(currentServer, currentResponseUtil, currentPkg, repository))
                    {
                        yield return pkgDep;
                    }
                }
            }
        }

        private bool IsTagMatch(PSResourceInfo pkg)
        {
            List<string> matchedTags = _tag.Intersect(pkg.Tags, StringComparer.InvariantCultureIgnoreCase).ToList();

            foreach (string tag in matchedTags)
            {
                _tagsLeftToFind.Remove(tag);
            }

            return matchedTags.Count > 0;
        }

        internal IEnumerable<PSResourceInfo> HttpFindDependencyPackages(
            ServerApiCall currentServer,
            ResponseUtil currentResponseUtil,
            PSResourceInfo currentPkg,
            PSRepositoryInfo repository)
        {
            if (currentPkg != null)
            {
                foreach (var dep in currentPkg.Dependencies)
                { 
                    if (dep.VersionRange == VersionRange.All)
                    {
                        string response = currentServer.FindName(dep.Name, _prerelease, _type, out ExceptionDispatchInfo edi);
                        string[] responses = new string[]{ response };
                        if (edi != null)
                        {
                            _cmdletPassedIn.WriteError(new ErrorRecord(edi.SourceException, "HttpFindDepPackagesFindNameFail", ErrorCategory.InvalidOperation, this));
                            continue;
                        }

                        PSResourceResult currentResult = currentResponseUtil.ConvertToPSResourceResult(responses: responses).First();
                        
                        if (!String.IsNullOrEmpty(currentResult.errorMsg))
                        {
                            _cmdletPassedIn.WriteError(new ErrorRecord(new PSInvalidOperationException(currentResult.errorMsg), "FindNameForDepResponseConversionFail", ErrorCategory.NotSpecified, this));
                            continue;
                        }

                        PSResourceInfo depPkg = currentResult.returnedObject;
                        yield return depPkg;

                        HttpFindDependencyPackages(currentServer, currentResponseUtil, depPkg, repository);
                    }
                    else
                    {
                        string[] responses = currentServer.FindVersionGlobbing(dep.Name, dep.VersionRange, _prerelease, ResourceType.None, getOnlyLatest: true, out ExceptionDispatchInfo edi);
                        if (edi != null)
                        {
                            _cmdletPassedIn.WriteError(new ErrorRecord(edi.SourceException, "HttpFindDepPackagesFindVersionGlobbingFail", ErrorCategory.InvalidOperation, this));
                            continue;
                        }

                        foreach (PSResourceResult currentResult in currentResponseUtil.ConvertToPSResourceResult(responses: responses))
                        {
                            if (!String.IsNullOrEmpty(currentResult.errorMsg))
                            {
                                _cmdletPassedIn.WriteError(new ErrorRecord(new PSInvalidOperationException(currentResult.errorMsg), "FindVersionGlobbingForDepResponseConversionFail", ErrorCategory.NotSpecified, this));
                                continue;
                            }

                            PSResourceInfo depPkg = currentResult.returnedObject;
                            yield return depPkg;

                            HttpFindDependencyPackages(currentServer, currentResponseUtil, depPkg, repository);         
                        }
                    }
                }
            }
        }

        #endregion

        #region private NuGet APIs for Local Repo

        private IEnumerable<PSResourceInfo> SearchFromLocalRepository(PSRepositoryInfo repositoryInfo)
        {
            PackageSearchResource resourceSearch;
            PackageMetadataResource resourceMetadata;
            SearchFilter filter;
            SourceCacheContext context;

            // File based Uri scheme.
            if (repositoryInfo.Uri.Scheme == Uri.UriSchemeFile)
            {
                FindLocalPackagesResourceV2 localResource = new FindLocalPackagesResourceV2(repositoryInfo.Uri.ToString());
                resourceSearch = new LocalPackageSearchResource(localResource);
                resourceMetadata = new LocalPackageMetadataResource(localResource);
                filter = new SearchFilter(_prerelease);
                context = new SourceCacheContext();

                foreach (PSResourceInfo pkg in SearchAcrossNamesInRepository(
                    repositoryName: repositoryInfo.Name,
                    pkgSearchResource: resourceSearch,
                    pkgMetadataResource: resourceMetadata,
                    searchFilter: filter,
                    sourceContext: context))
                {
                    yield return pkg;
                }
                yield break;
            }
        }

        private IEnumerable<PSResourceInfo> SearchAcrossNamesInRepository(
           string repositoryName,
           PackageSearchResource pkgSearchResource,
           PackageMetadataResource pkgMetadataResource,
           SearchFilter searchFilter,
           SourceCacheContext sourceContext)
        {
            foreach (string pkgName in _pkgsLeftToFind.ToArray())
            {
                if (String.IsNullOrWhiteSpace(pkgName))
                {
                    _cmdletPassedIn.WriteVerbose(String.Format("Package name: {0} provided was null or whitespace, so name was skipped in search.",
                        pkgName == null ? "null string" : pkgName));
                    continue;
                }

                // call NuGet client API
                foreach (PSResourceInfo pkg in FindFromPackageSourceSearchAPI(
                    repositoryName: repositoryName,
                    pkgName: pkgName,
                    pkgSearchResource: pkgSearchResource,
                    pkgMetadataResource: pkgMetadataResource,
                    searchFilter: searchFilter,
                    sourceContext: sourceContext))
                {
                    yield return pkg;
                }
            }
        }


        private IEnumerable<PSResourceInfo> FindFromPackageSourceSearchAPI(
            string repositoryName,
            string pkgName,
            PackageSearchResource pkgSearchResource,
            PackageMetadataResource pkgMetadataResource,
            SearchFilter searchFilter,
            SourceCacheContext sourceContext)
        {
            List<IPackageSearchMetadata> foundPackagesMetadata = new List<IPackageSearchMetadata>();
            VersionRange versionRange = null;

            if (_version != null)
            {
                if (!Utils.TryParseVersionOrVersionRange(_version, out versionRange))
                {
                    _cmdletPassedIn.WriteError(new ErrorRecord(
                        new ArgumentException("Argument for -Version parameter is not in the proper format"),
                        "IncorrectVersionFormat",
                        ErrorCategory.InvalidArgument,
                        this));
                    yield break;
                }

                if (_version.Contains("-"))
                {
                    _prerelease = true;
                }
            }

            // filter by param: Name
            if (!pkgName.Contains("*"))
            {
                // case: searching for specific package name i.e "Carbon"
                IEnumerable<IPackageSearchMetadata> retrievedPkgs;
                try
                {
                    // GetMetadataAsync() API returns all versions for a specific non-wildcard package name
                    // For PSGallery GetMetadataAsync() API returns both Script and Module resources by checking only the Modules endpoint
                    retrievedPkgs = pkgMetadataResource.GetMetadataAsync(
                        packageId: pkgName,
                        includePrerelease: _prerelease,
                        includeUnlisted: false,
                        sourceCacheContext: sourceContext,
                        log: NullLogger.Instance,
                        token: _cancellationToken).GetAwaiter().GetResult();
                }
                catch (HttpRequestException ex)
                {
                    Utils.WriteVerboseOnCmdlet(_cmdletPassedIn, "FindHelper MetadataAsync: error receiving package: " + ex.Message);
                    if ((String.Equals(repositoryName, _psGalleryRepoName, StringComparison.InvariantCultureIgnoreCase) ||
                        String.Equals(repositoryName, _psGalleryScriptsRepoName, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        _cmdletPassedIn.WriteWarning(String.Format("Error receiving package from PSGallery. To check if this is due to a PSGallery outage check: https://aka.ms/psgallerystatus . Specific error: {0}", ex.Message));
                    }

                    yield break;
                }
                catch (Exception e)
                {
                    Utils.WriteVerboseOnCmdlet(_cmdletPassedIn, "FindHelper MetadataAsync: error receiving package: " + e.Message);
                    yield break;
                }

                // Iterate through any packages found in repository.
                bool packagesFound = false;
                foreach (var pkg in retrievedPkgs)
                {
                    foundPackagesMetadata.Add(pkg);
                    if (!packagesFound) { packagesFound = true; }
                }

                if (packagesFound && !_repositoryNameContainsWildcard)
                {
                    _pkgsLeftToFind.Remove(pkgName);
                }
            }
            else
            {
                // Case: searching for name containing wildcard i.e "Carbon.*".
                List<IPackageSearchMetadata> wildcardPkgs;
                try
                {
                    string wildcardPkgName = pkgName.Equals("*") ? string.Empty : pkgName;
                    // SearchAsync() API returns the latest version only for all packages that match the wild-card name
                    wildcardPkgs = pkgSearchResource.SearchAsync(
                        searchTerm: wildcardPkgName,
                        filters: searchFilter,
                        skip: 0,
                        take: SearchAsyncMaxTake,
                        log: NullLogger.Instance,
                        cancellationToken: _cancellationToken).GetAwaiter().GetResult().ToList();

                    if (wildcardPkgs.Count > SearchAsyncMaxReturned)
                    {
                        // Get the rest of the packages.
                        wildcardPkgs.AddRange(
                            pkgSearchResource.SearchAsync(
                            searchTerm: wildcardPkgName,
                            filters: searchFilter,
                            skip: SearchAsyncMaxTake,
                            take: GalleryMax,
                            log: NullLogger.Instance,
                            cancellationToken: _cancellationToken).GetAwaiter().GetResult());
                    }
                }
                catch (HttpRequestException ex)
                {
                    Utils.WriteVerboseOnCmdlet(_cmdletPassedIn, "FindHelper SearchAsync: error receiving package: " + ex.Message);
                    if ((String.Equals(repositoryName, _psGalleryRepoName, StringComparison.InvariantCultureIgnoreCase) ||
                        String.Equals(repositoryName, _psGalleryScriptsRepoName, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        _cmdletPassedIn.WriteWarning(String.Format("Error receiving package from PSGallery. To check if this is due to a PSGallery outage check: https://aka.ms/psgallerystatus . Specific error: {0}", ex.Message));
                    }
                    yield break;
                }
                catch (Exception e)
                {
                    Utils.WriteVerboseOnCmdlet(_cmdletPassedIn, "FindHelper SearchAsync: error receiving package: " + e.Message);
                    yield break;
                }

                // filter additionally because NuGet wildcard search API returns more than we need
                // perhaps validate in Find-PSResource, and use debugassert here?
                WildcardPattern nameWildcardPattern = new WildcardPattern(pkgName, WildcardOptions.IgnoreCase);
                foundPackagesMetadata.AddRange(
                    wildcardPkgs.Where(
                        p => nameWildcardPattern.IsMatch(p.Identity.Id)));

                if (!_repositoryNameContainsWildcard)
                {
                    // If the Script Uri endpoint still needs to be searched, don't remove the wildcard name from _pkgsLeftToFind
                    // PSGallery + Type == null -> M, S
                    // PSGallery + Type == M    -> M
                    // PSGallery + Type == S    -> S (but PSGallery would be skipped early on, only PSGalleryScripts would be checked)
                    // PSGallery + Type == C    -> M
                    // PSGallery + Type == D    -> M

                    if (String.Equals(repositoryName, _psGalleryRepoName, StringComparison.InvariantCultureIgnoreCase) ||
                        String.Equals(repositoryName, _poshTestGalleryRepoName, StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (foundPackagesMetadata.Count > 0 && _type != ResourceType.None)
                        {
                            _pkgsLeftToFind.Remove(pkgName);
                        }
                    }

                }

                // if repository names did contain wildcard, we want to do an exhaustive search across all the repositories
                // which matched the input repository name search term.
            }

            if (foundPackagesMetadata.Count == 0)
            {
                // no need to attempt to filter further
                _cmdletPassedIn.WriteVerbose($"No packages found in repository: {repositoryName}.");
                yield break;
            }

            // filter by param: Version
            if (_version == null)
            {
                // return latest version for each package
                foundPackagesMetadata = foundPackagesMetadata.GroupBy(
                    p => p.Identity.Id, StringComparer.InvariantCultureIgnoreCase).Select(
                        x => x.OrderByDescending(
                            p => p.Identity.Version, VersionComparer.VersionRelease).FirstOrDefault()).ToList();
            }
            else
            {
                // at this point, version should be parsed successfully, into allVersions (null or "*") or versionRange (specific or range)
                if (pkgName.Contains("*"))
                {
                    // -Name containing wc with Version "*", or specific range
                    // at this point foundPackagesMetadata contains latest version for each package, get list of distinct
                    // package names and get all versions for each name, this is due to the SearchAsync and GetMetadataAsync() API restrictions !
                    List<IPackageSearchMetadata> allPkgsAllVersions = new List<IPackageSearchMetadata>();
                    foreach (string n in foundPackagesMetadata.Select(p => p.Identity.Id).Distinct(StringComparer.InvariantCultureIgnoreCase))
                    {
                        // get all versions for this package
                        allPkgsAllVersions.AddRange(pkgMetadataResource.GetMetadataAsync(n, _prerelease, false, sourceContext, NullLogger.Instance, _cancellationToken).GetAwaiter().GetResult().ToList());
                    }

                    foundPackagesMetadata = allPkgsAllVersions;
                    if (versionRange == VersionRange.All) // Version = "*"
                    {
                        foundPackagesMetadata = foundPackagesMetadata.GroupBy(
                            p => p.Identity.Id, StringComparer.InvariantCultureIgnoreCase).SelectMany(
                                x => x.OrderByDescending(
                                    p => p.Identity.Version, VersionComparer.VersionRelease)).ToList();
                    }
                    else // Version range
                    {
                        foundPackagesMetadata = foundPackagesMetadata.Where(
                            p => versionRange.Satisfies(p.Identity.Version)).GroupBy(
                                p => p.Identity.Id, StringComparer.InvariantCultureIgnoreCase).SelectMany(
                                    x => x.OrderByDescending(
                                    p => p.Identity.Version, VersionComparer.VersionRelease)).ToList();
                    }
                }
                else // name doesn't contain wildcards
                {
                    // for non wildcard names, NuGet GetMetadataAsync() API is which returns all versions for that package ordered descendingly
                    if (versionRange != VersionRange.All) // Version range
                    {
                        foundPackagesMetadata = foundPackagesMetadata.Where(
                            p => versionRange.Satisfies(
                                p.Identity.Version, VersionComparer.VersionRelease)).OrderByDescending(
                                    p => p.Identity.Version).ToList();
                    }
                }
            }

            foreach (IPackageSearchMetadata pkg in foundPackagesMetadata)
            {
                if (!PSResourceInfo.TryConvert(
                    metadataToParse: pkg,
                    psGetInfo: out PSResourceInfo currentPkg,
                    repositoryName: repositoryName,
                    type: _type,
                    errorMsg: out string errorMsg))
                {
                    _cmdletPassedIn.WriteError(new ErrorRecord(
                        new PSInvalidOperationException("Error parsing IPackageSearchMetadata to PSResourceInfo with message: " + errorMsg),
                        "IPackageSearchMetadataToPSResourceInfoParsingError",
                        ErrorCategory.InvalidResult,
                        this));
                    yield break;
                }

                // Only going to go in here for the main package, resolve Type and Tag requirements if any, and then find dependencies
                if (_tag == null || _tag.Length == 0 || (_tag != null && _tag.Length > 0 && IsTagMatch(currentPkg)))
                {
                    yield return currentPkg;

                    if (_includeDependencies)
                    {
                        foreach (PSResourceInfo pkgDep in FindDependencyPackages(currentPkg, pkgMetadataResource, sourceContext))
                        {
                            yield return pkgDep;
                        }
                    }
                }
            }
        }

        private List<PSResourceInfo> FindDependencyPackages(
            PSResourceInfo currentPkg,
            PackageMetadataResource packageMetadataResource,
            SourceCacheContext sourceCacheContext
        )
        {
            List<PSResourceInfo> thoseToAdd = new List<PSResourceInfo>();
            FindDependencyPackagesHelper(currentPkg, thoseToAdd, packageMetadataResource, sourceCacheContext);
            return thoseToAdd;
        }

        // TODO: create HTTP version of FindDependencyPackagesHelper
        private void FindDependencyPackagesHelper(
            PSResourceInfo currentPkg,
            List<PSResourceInfo> thoseToAdd,
            PackageMetadataResource packageMetadataResource,
            SourceCacheContext sourceCacheContext
        )
        {
            foreach (var dep in currentPkg.Dependencies)
            {
                List<IPackageSearchMetadata> depPkgs = packageMetadataResource.GetMetadataAsync(
                    packageId: dep.Name,
                    includePrerelease: _prerelease,
                    includeUnlisted: false,
                    sourceCacheContext: sourceCacheContext,
                    log: NullLogger.Instance,
                    token: _cancellationToken).GetAwaiter().GetResult().ToList();

                if (depPkgs.Count is 0)
                {
                    continue;
                }

                if (dep.VersionRange == VersionRange.All)
                {
                    // Return latest version, which is first in the list.
                    IPackageSearchMetadata depPkgLatestVersion = depPkgs[0];

                    if (!PSResourceInfo.TryConvert(
                        metadataToParse: depPkgLatestVersion,
                        psGetInfo: out PSResourceInfo depPSResourceInfoPkg,
                        repositoryName: currentPkg.Repository,
                        type: currentPkg.Type,
                        errorMsg: out string errorMsg))
                    {
                        _cmdletPassedIn.WriteError(new ErrorRecord(
                            new PSInvalidOperationException("Error parsing dependency IPackageSearchMetadata to PSResourceInfo with message: " + errorMsg),
                            "DependencyIPackageSearchMetadataToPSResourceInfoParsingError",
                            ErrorCategory.InvalidResult,
                            this));
                    }

                    thoseToAdd.Add(depPSResourceInfoPkg);
                    FindDependencyPackagesHelper(depPSResourceInfoPkg, thoseToAdd, packageMetadataResource, sourceCacheContext);
                }
                else
                {
                    List<IPackageSearchMetadata> pkgVersionsInRange = depPkgs.Where(
                        p => dep.VersionRange.Satisfies(
                            p.Identity.Version, VersionComparer.VersionRelease)).OrderByDescending(
                                p => p.Identity.Version).ToList();

                    if (pkgVersionsInRange.Count > 0)
                    {
                        IPackageSearchMetadata depPkgLatestInRange = pkgVersionsInRange[0];
                        if (depPkgLatestInRange != null)
                        {
                            if (!PSResourceInfo.TryConvert(
                                metadataToParse: depPkgLatestInRange,
                                psGetInfo: out PSResourceInfo depPSResourceInfoPkg,
                                repositoryName: currentPkg.Repository,
                                type: currentPkg.Type,
                                errorMsg: out string errorMsg))
                            {
                                _cmdletPassedIn.WriteError(new ErrorRecord(
                                    new PSInvalidOperationException("Error parsing dependency range IPackageSearchMetadata to PSResourceInfo with message: " + errorMsg),
                                    "DependencyRangeIPackageSearchMetadataToPSResourceInfoParsingError",
                                    ErrorCategory.InvalidResult,
                                    this));
                            }

                            thoseToAdd.Add(depPSResourceInfoPkg);
                            FindDependencyPackagesHelper(depPSResourceInfoPkg, thoseToAdd, packageMetadataResource, sourceCacheContext);
                        }
                    }
                }
            }
        }

        #endregion
    }
}
