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
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Xml.Serialization;

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
        private SwitchParameter _prerelease = false;
        private PSCredential _credential;
        private string[] _tag;
        private SwitchParameter _includeDependencies = false;
        private readonly string _psGalleryRepoName = "PSGallery";
        private readonly string _psGalleryScriptsRepoName = "PSGalleryScripts";
        private readonly string _psGalleryUri = "https://www.powershellgallery.com/api/v2";
        private readonly string _poshTestGalleryRepoName = "PoshTestGallery";
        private readonly string _poshTestGalleryUri = "https://www.poshtestgallery.com/api/v2";
        private bool _isADOFeedRepository;
        private bool _repositoryNameContainsWildcard;
        private HttpFindPSResource _httpFindPSResource = new HttpFindPSResource();

        // NuGet's SearchAsync() API takes a top parameter of 6000, but testing shows for PSGallery
        // usually a max of around 5990 is returned while more are left to retrieve in a second SearchAsync() call
        private const int SearchAsyncMaxTake = 6000;
        private const int SearchAsyncMaxReturned = 5990;
        private const int GalleryMax = 12000;

        #endregion

        #region Constructor

        private FindHelper() { }

        public FindHelper(CancellationToken cancellationToken, PSCmdlet cmdletPassedIn)
        {
            _cancellationToken = cancellationToken;
            _cmdletPassedIn = cmdletPassedIn;
        }

        #endregion

        #region Public methods

        public List<PSResourceInfo> FindByResourceName(
            string[] name,
            ResourceType type,
            string version,
            SwitchParameter prerelease,
            string[] tag,
            string[] repository,
            PSCredential credential,
            SwitchParameter includeDependencies)
        {
            _type = type;
            _version = version;
            _prerelease = prerelease;
            _tag = tag;
            _credential = credential;
            _includeDependencies = includeDependencies;

            List<PSResourceInfo> foundPackages = new List<PSResourceInfo>();

            if (name.Length == 0)
            {
                return foundPackages;
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
                
                return foundPackages;
            }

            for (int i = 0; i < repositoriesToSearch.Count && _pkgsLeftToFind.Any(); i++)
            {
                _cmdletPassedIn.WriteVerbose(string.Format("Searching in repository {0}", repositoriesToSearch[i].Name));
                if (repositoriesToSearch[i].ApiVersion == PSRepositoryInfo.APIVersion.v2 ||
                    repositoriesToSearch[i].ApiVersion == PSRepositoryInfo.APIVersion.v3)
                {
                    foreach (var pkg in HttpSearchFromRepository(repositoriesToSearch[i]))
                    {
                        foundPackages.Add(pkg);
                    }
                }
                else
                {
                    foreach (var pkg in SearchFromRepository(repositoriesToSearch[i]))
                    {
                        foundPackages.Add(pkg);
                    } 
                }

            }

            return foundPackages;
        }

        public IEnumerable<PSCommandResourceInfo> FindCommandOrDscResource(
            bool isSearchingForCommands,
            SwitchParameter prerelease,
            string[] tag,
            string[] repository,
            PSCredential credential)
        {
            _prerelease = prerelease;
            _tag = tag;
            _credential = credential;

            List<PSCommandResourceInfo> foundPackages = new List<PSCommandResourceInfo>();
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
                _cmdletPassedIn.WriteVerbose(string.Format("Searching in repository {0}", repositoriesToSearch[i].Name));
                if (repositoriesToSearch[i].ApiVersion == PSRepositoryInfo.APIVersion.v2 && repositoriesToSearch[i].Name.Equals("PSGallery"))
                {
                    foreach (PSCommandResourceInfo cmdInfo in HttpFindCmdOrDsc(repositoriesToSearch[i], isSearchingForCommands))
                    {
                        yield return cmdInfo;

                        foundPackages.Add(cmdInfo);
                        
                        foreach (string cmd in cmdInfo.Names)
                        {
                            cmdsLeftToFind.Remove(cmd);
                        }
                    }                        
                }
                else
                {
                    _cmdletPassedIn.WriteVerbose($"Searching by -CommandName or -DSCResource is not supported for {repositoriesToSearch[i].Name}. It is only supported for PSGallery.");
                }
            }
        }

        public List<PSResourceInfo> FindTag(
            ResourceType type,
            SwitchParameter prerelease,
            string[] tag,
            string[] repository,
            PSCredential credential)
        {
            _type = type;
            _prerelease = prerelease;
            _tag = tag;
            _credential = credential;

            List<PSResourceInfo> foundPackages = new List<PSResourceInfo>();
            _tagsLeftToFind = new List<string>(tag);

            if (tag.Length == 0)
            {
                return foundPackages;
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
                
                return foundPackages;
            }

            for (int i = 0; i < repositoriesToSearch.Count && _tagsLeftToFind.Any(); i++)
            {
                if (_type != ResourceType.None && repositoriesToSearch[i].Name != "PSGallery")
                {
                    _cmdletPassedIn.ThrowTerminatingError(new ErrorRecord(
                        new PSInvalidOperationException("-Type parameter is only supported with the PowerShellGallery."),
                        "ErrorUsingTypeParameter",
                        ErrorCategory.InvalidOperation,
                        this));
                }
                
                _cmdletPassedIn.WriteVerbose(string.Format("Searching in repository {0}", repositoriesToSearch[i].Name));
                if (repositoriesToSearch[i].ApiVersion == PSRepositoryInfo.APIVersion.v2)
                {                    
                    // TODO:  didn't really finsh come back here
                    foreach (PSResourceInfo cmdInfo in HttpSearchFromRepository(repositoriesToSearch[i]))
                    {
                        foundPackages.Add(cmdInfo);
                    }
                }
                if (repositoriesToSearch[i].ApiVersion == PSRepositoryInfo.APIVersion.v3)
                {
                    foreach (PSResourceInfo cmdInfo in HttpSearchFromRepository(repositoriesToSearch[i]))
                    {
                        foundPackages.Add(cmdInfo);
                    }
                }
            }

            return foundPackages;
        }
        #endregion

        #region Private methods

        private IEnumerable<PSResourceInfo> HttpSearchFromRepository(PSRepositoryInfo repositoryInfo)
        {
            // File based Uri scheme.
            if (repositoryInfo.Uri.Scheme != Uri.UriSchemeFile)
            {
                // -Name parameter and -Tag parameter are exclusive
                if (_tag != null)
                {
                    foreach (PSResourceInfo pkgs in HttpFindTags(repositoryInfo, _type))
                    {
                        yield return pkgs;
                    }
                }
                else if (_pkgsLeftToFind.Count > 0)
                {
                    foreach (PSResourceInfo pkg in HttpSearchAcrossNamesInRepository(repositoryInfo))
                    {
                        yield return pkg;
                    }
                }

                yield break;
            }
            // TODO:  ADD command search, dscresource search, etc. 
        }

        private IEnumerable<PSResourceInfo> SearchFromRepository(PSRepositoryInfo repositoryInfo)
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

                foreach(PSResourceInfo pkg in SearchAcrossNamesInRepository(
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

            // Check if ADOFeed- for which searching for Name with wildcard has a different logic flow.
            if (repositoryInfo.Uri.ToString().Contains("pkgs."))
            {
                _isADOFeedRepository = true;
            }

            // HTTP, HTTPS, FTP Uri schemes (only other Uri schemes allowed by RepositorySettings.Read() API).
            PackageSource source = new PackageSource(repositoryInfo.Uri.ToString());

            // Explicitly passed in Credential takes precedence over repository CredentialInfo.
            if (_credential != null)
            {
                string password = new NetworkCredential(string.Empty, _credential.Password).Password;
                source.Credentials = PackageSourceCredential.FromUserInput(repositoryInfo.Uri.ToString(), _credential.UserName, password, true, null);
                _cmdletPassedIn.WriteVerbose("credential successfully set for repository: " + repositoryInfo.Name);
            }
            else if (repositoryInfo.CredentialInfo != null)
            {
                PSCredential repoCredential = Utils.GetRepositoryCredentialFromSecretManagement(
                    repositoryInfo.Name,
                    repositoryInfo.CredentialInfo,
                    _cmdletPassedIn);

                string password = new NetworkCredential(string.Empty, repoCredential.Password).Password;
                source.Credentials = PackageSourceCredential.FromUserInput(repositoryInfo.Uri.ToString(), repoCredential.UserName, password, true, null);
                _cmdletPassedIn.WriteVerbose("credential successfully read from vault and set for repository: " + repositoryInfo.Name);
            }

            // GetCoreV3() API is able to handle V2 and V3 repository endpoints.
            var provider = FactoryExtensionsV3.GetCoreV3(NuGet.Protocol.Core.Types.Repository.Provider);
            SourceRepository repository = new SourceRepository(source, provider);
            resourceSearch = null;
            resourceMetadata = null;

            try
            {
                resourceSearch = repository.GetResourceAsync<PackageSearchResource>().GetAwaiter().GetResult();
                resourceMetadata = repository.GetResourceAsync<PackageMetadataResource>().GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                Utils.WriteVerboseOnCmdlet(_cmdletPassedIn, "Error retrieving resource from repository: " + e.Message);
            }

            if (resourceSearch == null || resourceMetadata == null)
            {
                yield break;
            }

            filter = new SearchFilter(_prerelease);
            context = new SourceCacheContext();

            foreach(PSResourceInfo pkg in SearchAcrossNamesInRepository(
                repositoryName: String.Equals(repositoryInfo.Uri.AbsoluteUri, _psGalleryUri, StringComparison.InvariantCultureIgnoreCase) ? _psGalleryRepoName :
                                (String.Equals(repositoryInfo.Uri.AbsoluteUri, _poshTestGalleryUri, StringComparison.InvariantCultureIgnoreCase) ? _poshTestGalleryRepoName : repositoryInfo.Name),
                pkgSearchResource: resourceSearch,
                pkgMetadataResource: resourceMetadata,
                searchFilter: filter,
                sourceContext: context))
            {
                yield return pkg;
            }
        }

        private IEnumerable<PSResourceInfo> HttpSearchAcrossNamesInRepository(PSRepositoryInfo repository)
        {
            VersionRange versionRange = null;
            NuGetVersion nugetVersion = null;
            List<PSResourceInfo> parentPkgs = new List<PSResourceInfo>(); 

            if (_version != null)
            {
                if (!NuGetVersion.TryParse(_version, out nugetVersion))
                {
                    if (_version.Trim().Equals("*"))
                    {
                        versionRange = VersionRange.All;
                    }
                    else if (!VersionRange.TryParse(_version, out versionRange))
                    {
                        _cmdletPassedIn.WriteError(new ErrorRecord(
                            new ArgumentException("Argument for -Version parameter is not in the proper format"),
                            "IncorrectVersionFormat",
                            ErrorCategory.InvalidArgument,
                            this));
                        yield break;
                    }
                }

                // Checking 
                // eg:  -Version "3.0.0" -IncludePrerelease (there may be multiple prerelease versions of 3.0.0) 
                        //  3.0.0-beta1, 3.0.0-beta2, 3.0.0-beta3
                        //  query+IsAbsoluteLatest  =>  3.0.0-beta3

                // -Version "3.0.0.0" -IncludePrerelease  -> 
                //if (_version.Contains("-"))
                //{
                //    doVersionPrereleaseGlobbing = true;
                //}
            }

            // Note: For a single version, we have 1 or more name, with or without globbing
            // TODO: do we mean 2nd condition to be !?
            // scenario: specific version (case 1) or no version (case 2, for which we get latest version)
            if (nugetVersion != null || string.IsNullOrEmpty(_version))
            {
                foreach (string pkgName in _pkgsLeftToFind.ToArray())
                {
                    if (String.IsNullOrWhiteSpace(pkgName))
                    {
                        _cmdletPassedIn.WriteVerbose(String.Format("Package name: {0} provided was null or whitespace, so name was skipped in search.",
                            pkgName?? "null string"));
                        continue;
                    }

                    if (pkgName.Equals("*"))
                    {
                        if (nugetVersion != null)
                        { 
                            // TODO: write error about name "*" -Version X not being supported. Version must be empty
                            continue;
                        }

                        foreach (PSResourceResult searchResult in _httpFindPSResource.FindAll(repository, _prerelease, _type))
                        {
                            if (!String.IsNullOrEmpty(searchResult.errorMsg))
                            {
                                PSResourceInfo foundPkg = searchResult.returnedObject;
                                parentPkgs.Add(foundPkg);
                                yield return foundPkg;
                            }
                            else
                            {
                                _cmdletPassedIn.WriteError(new ErrorRecord(new PSInvalidOperationException(searchResult.errorMsg), "findAllFail", ErrorCategory.NotSpecified, null));
                            }
                        }
                    }
                    else if (pkgName.Contains('*'))
                    {
                        // call 'FindNameGlobbing' or 'FindNameGlobbingAndVersion'
                        if (string.IsNullOrEmpty(_version))
                        {
                            foreach (PSResourceResult searchResult in _httpFindPSResource.FindNameGlobbing(pkgName, repository, _prerelease, _type))
                            {
                                if (!String.IsNullOrEmpty(searchResult.errorMsg))
                                {
                                    PSResourceInfo foundPkg = searchResult.returnedObject;
                                    parentPkgs.Add(foundPkg);
                                    yield return foundPkg;
                                }
                                else
                                {
                                    _cmdletPassedIn.WriteError(new ErrorRecord(new PSInvalidOperationException(searchResult.errorMsg), "findNameGlobbingFail", ErrorCategory.NotSpecified, null));
                                }
                            }
                        }
                    }
                    else if (nugetVersion != null)
                    {
                        // TODO: User should not use -Prerelease parameter with a specific version.  Write out some kind of messaging (warning, error, verbose) to inform user of this
                        // if they attempt this combination
                        foreach (PSResourceResult searchResult in _httpFindPSResource.FindVersion(pkgName, nugetVersion.ToNormalizedString(), repository, _type))
                        {
                            if (!String.IsNullOrEmpty(searchResult.errorMsg))
                            {
                                PSResourceInfo foundPkg = searchResult.returnedObject;
                                parentPkgs.Add(foundPkg);

                                if (!_repositoryNameContainsWildcard)
                                {
                                    _pkgsLeftToFind.Remove(pkgName);
                                }

                                yield return foundPkg;
                            }
                            else
                            {
                                _cmdletPassedIn.WriteError(new ErrorRecord(new PSInvalidOperationException(searchResult.errorMsg), "findNameGlobbingFail", ErrorCategory.NotSpecified, null));
                            }
                        }

                        // TODO:  think about how the parent/dependency packages are being streamed, especially when there's multiple parents
                    }
                    else {
                        // If no version is specified, just retrieve the latest version
                        foreach (PSResourceResult searchResult in _httpFindPSResource.FindName(pkgName, repository, _prerelease, _type))
                        {
                            if (!String.IsNullOrEmpty(searchResult.errorMsg))
                            {
                                PSResourceInfo foundPkg = searchResult.returnedObject;
                                parentPkgs.Add(foundPkg);

                                if (!_repositoryNameContainsWildcard)
                                {
                                    _pkgsLeftToFind.Remove(pkgName);
                                }

                                yield return foundPkg;
                            }
                            else
                            {
                                _cmdletPassedIn.WriteError(new ErrorRecord(new PSInvalidOperationException(searchResult.errorMsg), "findNameGlobbingFail", ErrorCategory.NotSpecified, null));
                            }
                        }
                    }
                }
            }
            else
            {
                // Note: we have 1 or more names, but no name globbing if it's a version range
                foreach (string pkgName in _pkgsLeftToFind.ToArray())
                {
                    if (String.IsNullOrWhiteSpace(pkgName))
                    {
                        _cmdletPassedIn.WriteVerbose(String.Format("Package name: {0} provided was null or whitespace, so name was skipped in search.",
                            pkgName?? "null string"));
                        continue;
                    }

                    // TODO: Check if this is handled in parameter binding
                    if (pkgName.Contains('*'))
                    {
                        var exMessage = "Cannot have wildcard in name when using version range";
                        var ex = new ArgumentException(exMessage);
                        var WildcardError = new ErrorRecord(ex, "InvalidWildCardUsage", ErrorCategory.InvalidOperation, null);
                        _cmdletPassedIn.WriteError(WildcardError);
                    }
                    else
                    {
                        // TODO: deal with errRecord and making FindVesionGlobbing yield single packages instead of returning an array.
                        foreach (PSResourceResult searchResult in _httpFindPSResource.FindVersionGlobbing(pkgName, versionRange, repository, _prerelease, _type, getOnlyLatest: false))
                        {
                            if (!String.IsNullOrEmpty(searchResult.errorMsg))
                            {
                                PSResourceInfo foundPkg = searchResult.returnedObject;
                                parentPkgs.Add(foundPkg);
                                yield return foundPkg;
                            }
                            else
                            {
                                _cmdletPassedIn.WriteError(new ErrorRecord(new PSInvalidOperationException(searchResult.errorMsg), "findVersionGlobbingFail", ErrorCategory.NotSpecified, null));
                            }
                        }
                    }
                }
            }

            // After retrieving all packages
            if (_includeDependencies)
            {
                foreach (PSResourceInfo currentPkg in parentPkgs)
                {
                    // Actually find and return the dependency packages
                    foreach (PSResourceInfo pkgDep in HttpFindDependencyPackages(currentPkg, repository))
                    {
                        yield return pkgDep;
                    }
                }
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
                if (_isADOFeedRepository)
                {
                    _cmdletPassedIn.WriteError(new ErrorRecord(
                        new ArgumentException(String.Format("Searching through ADOFeed with wildcard in Name is not supported, so {0} repository will be skipped.", repositoryName)),
                        "CannotSearchADOFeedWithWildcardName",
                        ErrorCategory.InvalidArgument,
                        this));
                    yield break;
                }

                // Case: searching for name containing wildcard i.e "Carbon.*".
                List<IPackageSearchMetadata> wildcardPkgs;
                try
                {
                    // SearchAsync() API returns the latest version only for all packages that match the wild-card name
                    wildcardPkgs = pkgSearchResource.SearchAsync(
                        searchTerm: pkgName,
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
                            searchTerm: pkgName,
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

                // if (_type != ResourceType.None)
                // {
                //     if (_type == ResourceType.Command && !currentPkg.Type.HasFlag(ResourceType.Command))
                //     {
                //         continue;
                //     }
                //     if (_type == ResourceType.DscResource && !currentPkg.Type.HasFlag(ResourceType.DscResource))
                //     {
                //         continue;
                //     }
                // }

                // Only going to go in here for the main package, resolve Type and Tag requirements if any, and then find dependencies
                if (_tag == null || (_tag != null && IsTagMatch(currentPkg)))
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

        private bool IsTagMatch(PSResourceInfo pkg)
        {
            List<string> matchedTags = _tag.Intersect(pkg.Tags, StringComparer.InvariantCultureIgnoreCase).ToList();

            foreach (string tag in matchedTags)
            {
                _tagsLeftToFind.Remove(tag);
            }

            return matchedTags.Count > 0;
        }

        private IEnumerable<PSResourceInfo> HttpFindTags(PSRepositoryInfo repository, ResourceType type)
        {
            foreach (PSResourceResult searchResult in _httpFindPSResource.FindTags(_tag, repository, _prerelease, type))
            {
                if (!String.IsNullOrEmpty(searchResult.errorMsg))
                {
                    PSResourceInfo foundPkg = searchResult.returnedObject;
                    yield return foundPkg;
                }
                else
                {
                    _cmdletPassedIn.WriteError(new ErrorRecord(new PSInvalidOperationException(searchResult.errorMsg), "FindTagsFail", ErrorCategory.NotSpecified, null));
                }
            }
        }

        private IEnumerable<PSCommandResourceInfo> HttpFindCmdOrDsc(PSRepositoryInfo repository, bool isSearchingForCommands)
        {
            yield return _httpFindPSResource.FindCommandOrDscResource(_tag, repository, _prerelease, isSearchingForCommands).First();
            // TODO:  write out error
        }

        private IEnumerable<PSResourceInfo> HttpFindDependencyPackages(PSResourceInfo currentPkg, PSRepositoryInfo repository)
        {
            List<string> errStrings = new List<string>();
            //errors = errStrings.ToArray();

            foreach (PSResourceInfo pkg in HttpFindDependencyPackagesHelper(currentPkg, repository)) {
                yield return pkg;
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

        private IEnumerable<PSResourceInfo> HttpFindDependencyPackagesHelper(
            PSResourceInfo currentPkg,
            PSRepositoryInfo repository)
        {
            if (currentPkg != null)
            {
                foreach (var dep in currentPkg.Dependencies)
                { 
                    if (dep.VersionRange == VersionRange.All)
                    {
                        foreach (PSResourceResult searchResult in _httpFindPSResource.FindName(dep.Name, repository, _prerelease, ResourceType.None))
                        {
                            if (String.IsNullOrEmpty(searchResult.errorMsg))
                            {
                                PSResourceInfo depPkg = searchResult.returnedObject;
                                // should we make recursive call here?
                                yield return searchResult.returnedObject;
                                HttpFindDependencyPackagesHelper(depPkg, repository); // TODO: does this need to be before yield return?
                            }
                            else
                            {
                                _cmdletPassedIn.WriteError(new ErrorRecord(new PSInvalidOperationException(searchResult.errorMsg), "FindNameForDepsFail", ErrorCategory.NotSpecified, null));
                            }
                        }
                    }
                    else
                    {
                        foreach (PSResourceResult searchResult in _httpFindPSResource.FindVersionGlobbing(dep.Name, dep.VersionRange, repository, _prerelease, ResourceType.None, getOnlyLatest: true))
                        {
                            if (String.IsNullOrEmpty(searchResult.errorMsg))
                            {
                                PSResourceInfo depPkg = searchResult.returnedObject;
                                // should we make recursive call here?
                                yield return searchResult.returnedObject;
                                HttpFindDependencyPackagesHelper(depPkg, repository); // TODO: does this need to be before yield return?
                            }
                            else
                            {
                                _cmdletPassedIn.WriteError(new ErrorRecord(new PSInvalidOperationException(searchResult.errorMsg), "FindVersionGlobbingForDepsFail", ErrorCategory.NotSpecified, null));
                            }
                        }
                    }
                }
            }
        }

        // TODO: create HTTP version of FindDependencyPackagesHelper
        private void FindDependencyPackagesHelper(
            PSResourceInfo currentPkg,
            List<PSResourceInfo> thoseToAdd,
            PackageMetadataResource packageMetadataResource,
            SourceCacheContext sourceCacheContext
        )
        {
            foreach(var dep in currentPkg.Dependencies)
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
