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

            _pkgsLeftToFind = new HashSet<string>(name, StringComparer.InvariantCultureIgnoreCase);
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

            bool pkgFound = false;
            for (int i = 0; i < repositoriesToSearch.Count && _pkgsLeftToFind.Count > 0 ; i++)
            {
                PSRepositoryInfo currentRepository = repositoriesToSearch[i];
                _networkCredential = Utils.SetNetworkCredential(currentRepository, _networkCredential, _cmdletPassedIn);
                ServerApiCall currentServer = ServerFactory.GetServer(currentRepository, _networkCredential);
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

                _cmdletPassedIn.WriteVerbose(string.Format("Searching in repository {0}", repositoriesToSearch[i].Name));

                foreach (PSResourceInfo currentPkg in SearchByNames(currentServer, currentResponseUtil, currentRepository))
                {
                    pkgFound = currentPkg != null;
                    yield return currentPkg;
                }
            }

            // Do not write out error message if -Name "*"
            if (!pkgFound && !_pkgsLeftToFind.Contains("*"))
            {
                _cmdletPassedIn.WriteError(new ErrorRecord(
                            new ResourceNotFoundException($"Package(s) '{string.Join(", ", _pkgsLeftToFind)}' could not be found in any registered repositories."),
                            "PackageNotFound",
                            ErrorCategory.ObjectNotFound,
                            this));
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

            for (int i = 0; i < repositoriesToSearch.Count; i++)
            {
                PSRepositoryInfo currentRepository = repositoriesToSearch[i];
                
                _networkCredential = Utils.SetNetworkCredential(currentRepository, _networkCredential, _cmdletPassedIn);
                ServerApiCall currentServer = ServerFactory.GetServer(currentRepository, _networkCredential);
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

                _cmdletPassedIn.WriteVerbose(string.Format("Searching in repository {0}", repositoriesToSearch[i].Name));

                FindResults responses = currentServer.FindCommandOrDscResource(tag, _prerelease, isSearchingForCommands, out ErrorRecord errRecord);
                if (errRecord != null)
                {
                    if (errRecord.Exception is ResourceNotFoundException)
                    {
                        _cmdletPassedIn.WriteVerbose(errRecord.Exception.Message);
                    }
                    else {
                        _cmdletPassedIn.WriteError(errRecord);
                    }
                    continue;
                }

                foreach (PSResourceResult currentResult in currentResponseUtil.ConvertToPSResourceResult(responses))
                {
                    if (currentResult.exception != null && !currentResult.exception.Message.Equals(string.Empty))
                    {
                        errRecord = new ErrorRecord(
                                    new ResourceNotFoundException($"'{tag}' could not be found", currentResult.exception), 
                                    "FindCmdOrDscToPSResourceObjFailure", 
                                    ErrorCategory.NotSpecified, 
                                    this);

                        _cmdletPassedIn.WriteVerbose(errRecord.Exception.Message);
                        
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

            for (int i = 0; i < repositoriesToSearch.Count && _tagsLeftToFind.Any(); i++)
            {
                PSRepositoryInfo currentRepository = repositoriesToSearch[i];
                _networkCredential = Utils.SetNetworkCredential(currentRepository, _networkCredential, _cmdletPassedIn);
                ServerApiCall currentServer = ServerFactory.GetServer(currentRepository, _networkCredential);
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
                    if (errRecord.Exception is ResourceNotFoundException)
                    {
                        _cmdletPassedIn.WriteVerbose(errRecord.Exception.Message);
                    }
                    else {
                        _cmdletPassedIn.WriteError(errRecord);
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

                        _cmdletPassedIn.WriteVerbose(errRecord.Exception.Message);

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
            ErrorRecord errRecord = null;
            List<PSResourceInfo> parentPkgs = new List<PSResourceInfo>();
            HashSet<string> pkgsFound = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string pkgName in _pkgsLeftToFind.ToArray())
            {
                if (_versionType == VersionType.NoVersion)
                {
                    if (pkgName.Trim().Equals("*"))
                    {
                        // Example: Find-PSResource -Name "*"
                        FindResults responses = currentServer.FindAll(_prerelease, _type, out errRecord);
                        if (errRecord != null)
                        {
                            if (errRecord.Exception is ResourceNotFoundException)
                            {
                                _cmdletPassedIn.WriteVerbose(errRecord.Exception.Message);
                            }
                            else {
                                _cmdletPassedIn.WriteError(errRecord);
                            }
                            continue;
                        }

                        foreach (PSResourceResult currentResult in currentResponseUtil.ConvertToPSResourceResult(responseResults: responses))
                        {
                            if (currentResult.exception != null && !currentResult.exception.Message.Equals(string.Empty))
                            {
                               _cmdletPassedIn.WriteVerbose($"Package '{pkgName}' could not be found");
                                continue;
                            }

                            PSResourceInfo foundPkg = currentResult.returnedObject;

                            if (foundPkg.Type == _type || _type == ResourceType.None)
                            {
                                parentPkgs.Add(foundPkg);
                                _pkgsLeftToFind.Remove(foundPkg.Name);
                                pkgsFound.Add(String.Format("{0}{1}", foundPkg.Name, foundPkg.Version.ToString()));
                                yield return foundPkg;
                            }
                        }
                    }
                    else if(pkgName.Contains("*"))
                    {
                        // Example: Find-PSResource -Name "Az*"
                        // Example: Find-PSResource -Name "Az*" -Tag "Storage"
                        string tagMsg = String.Empty;
                        FindResults responses = null;
                        string tagsAsString = string.Empty;
                        if (_tag.Length == 0)
                        {
                            responses = currentServer.FindNameGlobbing(pkgName, _prerelease, _type, out errRecord);
                        }
                        else
                        {
                            responses = currentServer.FindNameGlobbingWithTag(pkgName, _tag, _prerelease, _type, out errRecord);
                            tagsAsString = String.Join(", ", _tag);
                            tagMsg = $" and Tags {tagsAsString}";
                        }

                        if (errRecord != null)
                        {
                            if (errRecord.Exception is ResourceNotFoundException)
                            {
                                _cmdletPassedIn.WriteVerbose(errRecord.Exception.Message);
                            }
                            else {
                                _cmdletPassedIn.WriteError(errRecord);
                            }
                            continue;
                        }

                        foreach (PSResourceResult currentResult in currentResponseUtil.ConvertToPSResourceResult(responses))
                        {
                            if (currentResult.exception != null && !currentResult.exception.Message.Equals(string.Empty))
                            {
                                string message = _tag.Length == 0 ? $"Package '{pkgName}' could not be found." : $"Package '{pkgName}' with tags '{tagsAsString}' could not be found.";
                                errRecord = new ErrorRecord(
                                            new ResourceNotFoundException(message, currentResult.exception), 
                                            "FindNameGlobbingConvertToPSResourceFailure", 
                                            ErrorCategory.InvalidResult, 
                                            this);
                                
                                _cmdletPassedIn.WriteVerbose(message);

                                continue;
                            }

                            PSResourceInfo foundPkg = currentResult.returnedObject;
                            parentPkgs.Add(foundPkg);
                            _pkgsLeftToFind.Remove(foundPkg.Name);
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
                        string tagsAsString = string.Empty;
                        if (_tag.Length == 0)
                        {
                            responses = currentServer.FindName(pkgName, _prerelease, _type, out errRecord);
                        }
                        else
                        {
                            responses = currentServer.FindNameWithTag(pkgName, _tag, _prerelease, _type, out errRecord);
                            tagsAsString = String.Join(", ", _tag);
                            tagMsg = $" and Tags {tagsAsString}";
                        }

                        if (errRecord != null)
                        {
                            if (errRecord.Exception is ResourceNotFoundException)
                            {
                                _cmdletPassedIn.WriteVerbose(errRecord.Exception.Message);
                            }
                            else {
                                _cmdletPassedIn.WriteError(errRecord);
                            }
                            continue;
                        }

                        PSResourceResult currentResult = currentResponseUtil.ConvertToPSResourceResult(responses).First();
                        
                        if (currentResult.exception != null && !currentResult.exception.Message.Equals(string.Empty))
                        {
                            string message = _tag.Length == 0 ? $"Package '{pkgName}' could not be found." : $"Package '{pkgName}' with tags '{tagsAsString}' could not be found.";

                            _cmdletPassedIn.WriteVerbose(message);

                            continue;
                        }

                        PSResourceInfo foundPkg = currentResult.returnedObject;
                        parentPkgs.Add(foundPkg);
                        _pkgsLeftToFind.Remove(foundPkg.Name);
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
                            responses = currentServer.FindVersion(pkgName, _nugetVersion.ToNormalizedString(), _type, out errRecord);
                        }
                        else
                        {
                            responses = currentServer.FindVersionWithTag(pkgName, _nugetVersion.ToNormalizedString(), _tag, _type, out errRecord);
                            string tagsAsString = String.Join(", ", _tag);
                            tagMsg = $" and Tags {tagsAsString}";
                        }

                        if (errRecord != null)
                        {
                            if (errRecord.Exception is ResourceNotFoundException)
                            {
                                _cmdletPassedIn.WriteVerbose(errRecord.Exception.Message);
                            }
                            else {
                                _cmdletPassedIn.WriteError(errRecord);
                            }
                            continue;
                        }

                        PSResourceResult currentResult = currentResponseUtil.ConvertToPSResourceResult(responses).First();
                        
                        if (currentResult.exception != null && !currentResult.exception.Message.Equals(string.Empty))
                        {
                            errRecord = new ErrorRecord(
                                        new ResourceNotFoundException($"Package '{pkgName}' with version '{_version}', and tags '{tagMsg}' could not be found", currentResult.exception), 
                                        "FindNameConvertToPSResourceFailure", 
                                        ErrorCategory.InvalidResult, 
                                        this);

                            _cmdletPassedIn.WriteVerbose(errRecord.Exception.Message);
                            
                            continue;
                        }

                        PSResourceInfo foundPkg = currentResult.returnedObject;
                        parentPkgs.Add(foundPkg);
                        _pkgsLeftToFind.Remove(foundPkg.Name);
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
                            responses = currentServer.FindVersionGlobbing(pkgName, _versionRange, _prerelease, _type, getOnlyLatest: false, out errRecord);
                        }
                        else
                        {
                            var exMessage = "Name cannot contain or equal wildcard when using version range";
                            var ex = new ArgumentException(exMessage);
                            var WildcardError = new ErrorRecord(ex, "InvalidWildCardUsage", ErrorCategory.InvalidOperation, null);
                            _cmdletPassedIn.WriteError(WildcardError);
                            continue;
                        }

                        if (errRecord != null)
                        {
                            if (errRecord.Exception is ResourceNotFoundException)
                            {
                                _cmdletPassedIn.WriteVerbose(errRecord.Exception.Message);
                            }
                            else {
                                _cmdletPassedIn.WriteError(errRecord);
                            }
                            continue;
                        }

                        foreach (PSResourceResult currentResult in currentResponseUtil.ConvertToPSResourceResult(responses))
                        {
                            if (currentResult.exception != null && !currentResult.exception.Message.Equals(string.Empty))
                            {
                                errRecord = new ErrorRecord(
                                            new ResourceNotFoundException($"Package '{pkgName}' with version '{_version}' could not be found", currentResult.exception), 
                                            "FindNameConvertToPSResourceFailure", 
                                            ErrorCategory.InvalidResult, 
                                            this);
                                
                                _cmdletPassedIn.WriteVerbose(errRecord.Exception.Message);

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
                                _pkgsLeftToFind.Remove(foundPkg.Name);
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
                    foreach (PSResourceInfo pkgDep in FindDependencyPackages(currentServer, currentResponseUtil, currentPkg, repository, pkgsFound))
                    {
                        yield return pkgDep;
                    }
                }
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

                    if (dep.VersionRange.Equals(VersionRange.All))
                    {
                        FindResults responses = currentServer.FindName(dep.Name, _prerelease, _type, out ErrorRecord errRecord);
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
                                        new ResourceNotFoundException($"Dependency package '{dep.Name}' could not be found in repository '{repository.Name}'", currentResult.exception), 
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
                        FindResults responses = currentServer.FindVersionGlobbing(dep.Name, dep.VersionRange, _prerelease, ResourceType.None, getOnlyLatest: true, out ErrorRecord errRecord);
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
                                        new InvalidOrEmptyResponse($"Dependency package with Name {dep.Name} and VersionRange {dep.VersionRange} could not be found in repository '{repository.Name}"), 
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
                                            new ResourceNotFoundException($"Dependency package '{dep.Name}' with version range '{dep.VersionRange}' could not be found in repository '{repository.Name}'", currentResult.exception), 
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
