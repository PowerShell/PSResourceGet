// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PSResourceGet.UtilClasses;
using NuGet.Versioning;
using System;
using System.Management.Automation;
using System.Net;
using System.Threading;

namespace Microsoft.PowerShell.PSResourceGet.Cmdlets
{
    /// <summary>
    /// The Find-PSResource cmdlet combines the Find-Module, Find-Script, Find-DscResource, Find-Command cmdlets from V2.
    /// It performs a search on a repository (local or remote) based on the -Name parameter argument.
    /// It returns PSResourceInfo objects which describe each resource item found.
    /// Other parameters allow the returned results to be filtered by item Type and Tag.
    /// </summary>
    [Cmdlet(VerbsCommon.Find,
        "PSResource",
        DefaultParameterSetName = NameParameterSet)]
    [Alias("fdres")]
    [OutputType(typeof(PSResourceInfo), typeof(PSCommandResourceInfo))]
    public sealed class FindPSResource : PSCmdlet
    {
        #region Members

        private const string NameParameterSet = "NameParameterSet";
        private const string CommandNameParameterSet = "CommandNameParameterSet";
        private const string DscResourceNameParameterSet = "DscResourceNameParameterSet";
        private CancellationTokenSource _cancellationTokenSource;
        private FindHelper _findHelper;

        #endregion

        #region Parameters

        /// <summary>
        /// Specifies name of a resource or resources to find. Accepts wild card characters.
        /// </summary>
        [SupportsWildcards]
        [Parameter(Position = 0,
                   ValueFromPipeline = true,
                   ValueFromPipelineByPropertyName = true,
                   ParameterSetName = NameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string[] Name { get; set; }

        /// <summary>
        /// Specifies one or more resource types to find.
        /// Resource types supported are: Module, Script
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        public ResourceType Type { get; set; }

        /// <summary>
        /// Specifies the version of the resource to be found and returned. Wildcards are supported.
        /// </summary>
        [SupportsWildcards]
        [Parameter(ParameterSetName = NameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string Version { get; set; }

        /// <summary>
        /// When specified, includes prerelease versions in search.
        /// </summary>
        [Parameter()]
        public SwitchParameter Prerelease { get; set; }

        /// <summary>
        /// Specifies a list of command names that searched module packages will provide. Wildcards are supported.
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = CommandNameParameterSet, HelpMessage = "Command name(s) to search for in packages.")]
        [ValidateNotNullOrEmpty]
        public string[] CommandName { get; set; }

        /// <summary>
        /// Specifies a list of dsc resource names that searched module packages will provide.
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = DscResourceNameParameterSet, HelpMessage = "DSC Resource name(s) to search for in packages.")]
        [ValidateNotNullOrEmpty]
        public string[] DscResourceName { get; set; }

        /// <summary>
        /// Filters search results for resources that include one or more of the specified tags.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        [ValidateNotNull]
        public string[] Tag { get; set; }

        /// <summary>
        /// Specifies one or more repository names to search. If not specified, search will include all currently registered repositories.
        /// </summary>
        [SupportsWildcards]
        [Parameter(ValueFromPipelineByPropertyName = true)]
        [ArgumentCompleter(typeof(RepositoryNameCompleter))]
        [ValidateNotNullOrEmpty]
        public string[] Repository { get; set; }

        /// <summary>
        /// Specifies optional credentials to be used when accessing a repository.
        /// </summary>
        [Parameter()]
        public PSCredential Credential { get; set; }

        /// <summary>
        /// When specified, search will return all matched resources along with any resources the matched resources depends on.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        public SwitchParameter IncludeDependencies { get; set; }

        #endregion

        #region Method Overrides

        protected override void BeginProcessing()
        {
            _cancellationTokenSource = new CancellationTokenSource();

            var networkCred = Credential != null ? new NetworkCredential(Credential.UserName, Credential.Password) : null;

            _findHelper = new FindHelper(
                cancellationToken: _cancellationTokenSource.Token,
                cmdletPassedIn: this,
                networkCredential: networkCred);

            // Create a repository story (the PSResourceRepository.xml file) if it does not already exist
            // This is to create a better experience for those who have just installed v3 and want to get up and running quickly
            RepositorySettings.CheckRepositoryStore();
        }

        protected override void StopProcessing()
        {
            _cancellationTokenSource?.Cancel();
        }

        protected override void EndProcessing()
        {
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }

        protected override void ProcessRecord()
        {
            switch (ParameterSetName)
            {
                case NameParameterSet:
                    ProcessResourceNameParameterSet();
                    break;

                case CommandNameParameterSet:
                    ProcessCommandOrDscParameterSet(isSearchingForCommands: true);
                    break;

                case DscResourceNameParameterSet:
                    ProcessCommandOrDscParameterSet(isSearchingForCommands: false);
                    break;

                default:
                    break;
            }
        }

        #endregion

        #region Private Methods

        private void ProcessResourceNameParameterSet()
        {
            if (Name[0].Equals("graphql"))
            {
                // string xmlString = "<Root>\r\n  <packageByName>\r\n    <version>26.0.0</version>\r\n    <description>PowerShell module with commands for discovering, installing, updating and publishing the PowerShell artifacts like Modules, DSC Resources, Role Capabilities and Scripts.</description>\r\n    <isLatest>true</isLatest>\r\n  </packageByName>\r\n</Root>";
                //                 string xmlString = @"<Root>
                // 	<packageByName>
                // 		<copyright>(c) Microsoft Corporation. All rights reserved.</copyright>
                // 		<description>PowerShell module with commands for discovering, installing, updating and publishing the PowerShell artifacts like Modules, DSC Resources, Role Capabilities and Scripts.</description>
                // 		<iconUrl />
                // 		<licenseUrl>https://go.microsoft.com/fwlink/?LinkId=829061</licenseUrl>
                // 		<published>2020-08-10T21:22:07.4Z</published>
                // 		<projectUrl>https://go.microsoft.com/fwlink/?LinkId=828955</projectUrl>
                // 		<tags>PackageManagement PSEdition_Desktop PSEdition_Core Linux Mac Windows PSModule</tags>
                // 		<title />
                // 		<version>26.0.0</version>
                // 		<flattenedAuthors>Microsoft Corporation</flattenedAuthors>
                // 		<flattenedDependencies></flattenedDependencies>
                // 		<isPrerelease>false</isPrerelease>
                // 		<releaseNotes>### 3.0.0-beta8
                // </releaseNotes>
                // 		<normalizedVersion>26.0.0</normalizedVersion>
                // 		<companyName>Microsoft Corporation</companyName>
                // 		<cmdlets>Find-PSResource Get-PSResourceRepository Get-PSResource Install-PSResource Register-PSResourceRepository Save-PSResource Set-PSResourceRepository Publish-PSResource Uninstall-PSResource Unregister-PSResourceRepository Update-PSResource</cmdlets>
                // 		<functions></functions>
                // 		<dscResources>PSModule PSRepository</dscResources>
                // 		<roleCapabilities></roleCapabilities>
                // 	</packageByName>
                // </Root>";

                                string xmlString = @"<Root>
                  <packageByName>
                    <copyright>Microsoft Corporation. All rights reserved.</copyright>
                    <description>Microsoft Azure PowerShell - Storage service data plane and management cmdlets for Azure Resource Manager in Windows PowerShell and PowerShell Core.  Creates and manages storage accounts in Azure Resource Manager.
                For more information on Storage, please visit the following: https://docs.microsoft.com/azure/storage/</description>
                    <iconUrl />
                    <licenseUrl>https://aka.ms/azps-license</licenseUrl>
                    <published>2022-04-01T02:13:27.757Z</published>
                    <projectUrl>https://github.com/Azure/azure-powershell</projectUrl>
                    <tags>Azure ResourceManager ARM Storage StorageAccount PSModule PSEdition_Core PSEdition_Desktop</tags>
                    <title />
                    <version>4.4.0</version>
                    <flattenedAuthors>Microsoft Corporation</flattenedAuthors>
                    <flattenedDependencies>Az.Accounts:[2.7.5, ):</flattenedDependencies>
                    <isPrerelease>false</isPrerelease>
                    <releaseNotes>* Updated examples in reference documentation for 'Close-AzStorageFileHandle'</releaseNotes>
                    <normalizedVersion>4.4.0</normalizedVersion>
                    <companyName>Microsoft Corporation</companyName>
                    <cmdlets>Get-AzStorageAccount Get-AzStorageAccountKey New-AzStorageAccount New-AzStorageAccountKey Remove-AzStorageAccount Set-AzCurrentStorageAccount Set-AzStorageAccount Get-AzStorageAccountNameAvailability Get-AzStorageUsage Update-AzStorageAccountNetworkRuleSet Get-AzStorageAccountNetworkRuleSet Add-AzStorageAccountNetworkRule Remove-AzStorageAccountNetworkRule Get-AzStorageTable New-AzStorageTableSASToken New-AzStorageTableStoredAccessPolicy New-AzStorageTable Remove-AzStorageTableStoredAccessPolicy Remove-AzStorageTable Get-AzStorageTableStoredAccessPolicy Set-AzStorageTableStoredAccessPolicy Get-AzStorageQueue New-AzStorageQueue Remove-AzStorageQueue Get-AzStorageQueueStoredAccessPolicy New-AzStorageQueueSASToken New-AzStorageQueueStoredAccessPolicy Remove-AzStorageQueueStoredAccessPolicy Set-AzStorageQueueStoredAccessPolicy Get-AzStorageFile Get-AzStorageFileContent Get-AzStorageFileCopyState Get-AzStorageShare Get-AzStorageShareStoredAccessPolicy New-AzStorageDirectory New-AzStorageFileSASToken New-AzStorageShare New-AzStorageShareSASToken New-AzStorageShareStoredAccessPolicy Remove-AzStorageDirectory Remove-AzStorageFile Remove-AzStorageShare Remove-AzStorageShareStoredAccessPolicy Set-AzStorageFileContent Set-AzStorageShareQuota Set-AzStorageShareStoredAccessPolicy Start-AzStorageFileCopy Stop-AzStorageFileCopy New-AzStorageAccountSASToken Set-AzStorageCORSRule Get-AzStorageCORSRule Get-AzStorageServiceLoggingProperty Get-AzStorageServiceMetricsProperty Remove-AzStorageCORSRule Set-AzStorageServiceLoggingProperty Set-AzStorageServiceMetricsProperty New-AzStorageContext Set-AzStorageContainerAcl Remove-AzStorageBlob Set-AzStorageBlobContent Get-AzStorageBlob Get-AzStorageBlobContent Get-AzStorageBlobCopyState Get-AzStorageContainer Get-AzStorageContainerStoredAccessPolicy New-AzStorageBlobSASToken New-AzStorageContainer New-AzStorageContainerSASToken New-AzStorageContainerStoredAccessPolicy Remove-AzStorageContainer Remove-AzStorageContainerStoredAccessPolicy Set-AzStorageContainerStoredAccessPolicy Start-AzStorageBlobCopy Start-AzStorageBlobIncrementalCopy Stop-AzStorageBlobCopy Update-AzStorageServiceProperty Get-AzStorageServiceProperty Enable-AzStorageDeleteRetentionPolicy Disable-AzStorageDeleteRetentionPolicy Enable-AzStorageStaticWebsite Disable-AzStorageStaticWebsite Get-AzRmStorageContainer Update-AzRmStorageContainer New-AzRmStorageContainer Remove-AzRmStorageContainer Add-AzRmStorageContainerLegalHold Remove-AzRmStorageContainerLegalHold Set-AzRmStorageContainerImmutabilityPolicy Get-AzRmStorageContainerImmutabilityPolicy Remove-AzRmStorageContainerImmutabilityPolicy Lock-AzRmStorageContainerImmutabilityPolicy Set-AzStorageAccountManagementPolicy Get-AzStorageAccountManagementPolicy Remove-AzStorageAccountManagementPolicy New-AzStorageAccountManagementPolicyFilter New-AzStorageAccountManagementPolicyRule Add-AzStorageAccountManagementPolicyAction Update-AzStorageBlobServiceProperty Get-AzStorageBlobServiceProperty Enable-AzStorageBlobDeleteRetentionPolicy Disable-AzStorageBlobDeleteRetentionPolicy Revoke-AzStorageAccountUserDelegationKeys Get-AzStorageFileHandle Close-AzStorageFileHandle New-AzRmStorageShare Remove-AzRmStorageShare Get-AzRmStorageShare Update-AzRmStorageShare Update-AzStorageFileServiceProperty Get-AzStorageFileServiceProperty Restore-AzRmStorageShare Get-AzDataLakeGen2ChildItem Get-AzDataLakeGen2Item New-AzDataLakeGen2Item Move-AzDataLakeGen2Item Remove-AzDataLakeGen2Item Update-AzDataLakeGen2Item Set-AzDataLakeGen2ItemAclObject Get-AzDataLakeGen2ItemContent Invoke-AzStorageAccountFailover Get-AzStorageBlobQueryResult New-AzStorageBlobQueryConfig New-AzStorageObjectReplicationPolicyRule Set-AzStorageObjectReplicationPolicy Get-AzStorageObjectReplicationPolicy Remove-AzStorageObjectReplicationPolicy Enable-AzStorageBlobRestorePolicy Disable-AzStorageBlobRestorePolicy New-AzStorageBlobRangeToRestore Restore-AzStorageBlobRange Set-AzDataLakeGen2AclRecursive Update-AzDataLakeGen2AclRecursive Remove-AzDataLakeGen2AclRecursive New-AzStorageEncryptionScope Update-AzStorageEncryptionScope Get-AzStorageEncryptionScope Copy-AzStorageBlob Set-AzStorageBlobInventoryPolicy New-AzStorageBlobInventoryPolicyRule Get-AzStorageBlobInventoryPolicy Remove-AzStorageBlobInventoryPolicy Enable-AzStorageContainerDeleteRetentionPolicy Disable-AzStorageContainerDeleteRetentionPolicy Restore-AzStorageContainer Enable-AzStorageBlobLastAccessTimeTracking Disable-AzStorageBlobLastAccessTimeTracking Set-AzStorageBlobTag Get-AzStorageBlobTag Get-AzStorageBlobByTag Invoke-AzStorageAccountHierarchicalNamespaceUpgrade Stop-AzStorageAccountHierarchicalNamespaceUpgrade Set-AzStorageBlobImmutabilityPolicy Remove-AzStorageBlobImmutabilityPolicy Set-AzStorageBlobLegalHold Invoke-AzRmStorageContainerImmutableStorageWithVersioningMigration</cmdlets>
                    <functions></functions>
                    <dscResources></dscResources>
                    <roleCapabilities></roleCapabilities>
                  </packageByName>
                </Root>";

//                 string xmlString = @"<Root>
//   <packageByName>
//     <copyright>(c) 2020 Contoso Corporation. All rights reserved.</copyright>
//     <description>this is a test module without including any categories</description>
//     <iconUrl />
//     <licenseUrl />
//     <published>2020-09-21T15:46:13.317Z</published>
//     <projectUrl />
//     <tags>Test CommandsAndResource Tag2 PSModule</tags>
//     <title />
//     <version>5.0.0</version>
//     <flattenedAuthors>americks</flattenedAuthors>
//     <flattenedDependencies>RequiredModule1:(, ):|RequiredModule2:[2.0.0, ):|RequiredModule3:[2.5.0, 2.5.0]:|RequiredModule4:[1.1.0, 2.0.0]:|RequiredModule5:(, 1.5.0]:</flattenedDependencies>
//     <isPrerelease>false</isPrerelease>
//     <releaseNotes />
//     <normalizedVersion>5.0.0</normalizedVersion>
//     <companyName>Me</companyName>
//     <cmdlets></cmdlets>
//     <functions></functions>
//     <dscResources></dscResources>
//     <roleCapabilities></roleCapabilities>
//   </packageByName>
// </Root>";
                PSResourceInfo.TryConvertXmlFromGraphQL(xmlString, out PSResourceInfo obj, out string errMsg);
                if (!String.IsNullOrEmpty(errMsg))
                {
                    WriteError(new ErrorRecord(
                        new PSInvalidOperationException(errMsg),
                        "ErrorFilteringNamesForUnsupportedWildcards",
                        ErrorCategory.InvalidArgument,
                        this));
                }

                WriteObject(obj);
                return;
            }

            // only cases where Name is allowed to not be specified is if Type or Tag parameters are
            if (!MyInvocation.BoundParameters.ContainsKey(nameof(Name)))
            {
                if (MyInvocation.BoundParameters.ContainsKey(nameof(Tag)))
                {
                    ProcessTags();
                    return;
                }
                else if (MyInvocation.BoundParameters.ContainsKey(nameof(Type)))
                {
                    Name = new string[] { "*" };
                }
                else
                {
                    ThrowTerminatingError(new ErrorRecord(
                        new PSInvalidOperationException("Name parameter must be provided, unless Tag or Type parameters are used."),
                        "NameParameterNotProvided",
                        ErrorCategory.InvalidOperation,
                        this));
                }
            }

            WriteDebug("Filtering package name(s) on wildcards");
            Name = Utils.ProcessNameWildcards(Name, removeWildcardEntries: false, out string[] errorMsgs, out bool nameContainsWildcard);

            foreach (string error in errorMsgs)
            {
                WriteError(new ErrorRecord(
                    new PSInvalidOperationException(error),
                    "ErrorFilteringNamesForUnsupportedWildcards",
                    ErrorCategory.InvalidArgument,
                    this));
            }

            // this catches the case where Name wasn't passed in as null or empty,
            // but after filtering out unsupported wildcard names there are no elements left in namesToSearch
            if (Name.Length == 0)
            {
                WriteDebug("Package name(s) could not be resolved");
                return;
            }

            // determine/parse out Version param
            VersionType versionType = VersionType.VersionRange;
            NuGetVersion nugetVersion = null;
            VersionRange versionRange = null;

            if (Version != null)
            {
                WriteDebug("Parsing package version");
                if (!NuGetVersion.TryParse(Version, out nugetVersion))
                {
                    if (Version.Trim().Equals("*"))
                    {
                        versionRange = VersionRange.All;
                        versionType = VersionType.VersionRange;
                    }
                    else if (!VersionRange.TryParse(Version, out versionRange))
                    {
                        WriteError(new ErrorRecord(
                            new ArgumentException("Argument for -Version parameter is not in the proper format"),
                            "IncorrectVersionFormat",
                            ErrorCategory.InvalidArgument,
                            this));

                        return;
                    }
                }
                else
                {
                    versionType = VersionType.SpecificVersion;
                }
            }
            else
            {
                versionType = VersionType.NoVersion;
            }

            foreach (PSResourceInfo pkg in _findHelper.FindByResourceName(
                name: Name,
                type: Type,
                versionRange: versionRange,
                nugetVersion: nugetVersion,
                versionType: versionType,
                version: Version,
                prerelease: Prerelease,
                tag: Tag,
                repository: Repository,
                includeDependencies: IncludeDependencies,
                suppressErrors: false))
            {
                WriteObject(pkg);
            }
        }

        private void ProcessCommandOrDscParameterSet(bool isSearchingForCommands)
        {
            WriteDebug("In FindPSResource::ProcessCommandOrDscParameterSet()");
            var commandOrDSCNamesToSearch = Utils.ProcessNameWildcards(
                pkgNames: isSearchingForCommands ? CommandName : DscResourceName,
                removeWildcardEntries: true,
                errorMsgs: out string[] errorMsgs,
                isContainWildcard: out bool _);

            var paramName = isSearchingForCommands ? "CommandName" : "DscResourceName";
            foreach (string error in errorMsgs)
            {
                WriteError(new ErrorRecord(
                    new PSInvalidOperationException($"Wildcards are not supported for -{paramName}: {error}"),
                    "WildcardsUnsupportedForCommandNameorDSCResourceName",
                    ErrorCategory.InvalidArgument,
                    this));
            }

            // this catches the case where Name wasn't passed in as null or empty,
            // but after filtering out unsupported wildcard names there are no elements left in commandOrDSCNamesToSearch
            if (commandOrDSCNamesToSearch.Length == 0)
            {
                WriteDebug("Command or DSCResource name(s) could not be resolved");
                return;
            }

            foreach (PSCommandResourceInfo cmdPkg in _findHelper.FindByCommandOrDscResource(
                isSearchingForCommands: isSearchingForCommands,
                prerelease: Prerelease,
                tag: commandOrDSCNamesToSearch,
                repository: Repository))
            {
                WriteObject(cmdPkg);
            }
        }

        private void ProcessTags()
        {
            WriteDebug("In FindPSResource::ProcessTags()");
            var tagsToSearch = Utils.ProcessNameWildcards(
                pkgNames: Tag,
                removeWildcardEntries: true,
                errorMsgs: out string[] errorMsgs,
                isContainWildcard: out bool _);

            foreach (string error in errorMsgs)
            {
                WriteError(new ErrorRecord(
                    new PSInvalidOperationException($"Wildcards are not supported for -Tag: {error}"),
                    "WildcardsUnsupportedForTag",
                    ErrorCategory.InvalidArgument,
                    this));
            }

            // this catches the case where Name wasn't passed in as null or empty,
            // but after filtering out unsupported wildcard names there are no elements left in tagsToSearch
            if (tagsToSearch.Length == 0)
            {
                WriteDebug("Tags(s) could not be resolved");
                return;
            }

            foreach (PSResourceInfo tagPkg in _findHelper.FindByTag(
                type: Type,
                prerelease: Prerelease,
                tag: tagsToSearch,
                repository: Repository))
            {
                WriteObject(tagPkg);
            }
        }

        #endregion
    }
}
