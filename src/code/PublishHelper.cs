using Microsoft.PowerShell.PSResourceGet.UtilClasses;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Versioning;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Xml;

namespace Microsoft.PowerShell.PSResourceGet.Cmdlets
{
	internal class PublishHelper
    {
        #region Enums
        internal enum CallerCmdlet
        {
            PublishPSResource,
            CompressPSResource
        }

        #endregion

        #region Members

        private readonly CallerCmdlet _callerCmdlet;
        private readonly PSCmdlet _cmdletPassedIn;
        private readonly string _cmdOperation;
        private readonly string Path;
        private string DestinationPath;
        private string resolvedPath;
        private CancellationToken _cancellationToken;
        private NuGetVersion _pkgVersion;
        private string _pkgName;
        private static char[] _PathSeparators = new[] { System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar };
        public const string PSDataFileExt = ".psd1";
        public const string PSScriptFileExt = ".ps1";
        public const string NupkgFileExt = ".nupkg";
        private const string PSScriptInfoCommentString = "<#PSScriptInfo";
        private string pathToScriptFileToPublish = string.Empty;
        private string pathToModuleManifestToPublish = string.Empty;
        private string pathToModuleDirToPublish = string.Empty;
        private string pathToNupkgToPublish = string.Empty;
        private ResourceType resourceType = ResourceType.None;
        private NetworkCredential _networkCredential;
        string userAgentString = UserAgentInfo.UserAgentString();
        private bool _isNupkgPathSpecified = false;
        private Hashtable dependencies;
        private Hashtable parsedMetadata;
        private PSCredential Credential;
        private string outputNupkgDir;
        private string ApiKey;
        private bool SkipModuleManifestValidate = false;
        private string outputDir = string.Empty;
        internal bool ScriptError = false;
        internal bool ShouldProcess = true;
        internal bool PassThru = false;

        #endregion

        #region Constructors

        internal PublishHelper(PSCmdlet cmdlet, string path, string destinationPath, bool passThru, bool skipModuleManifestValidate)
        {
            _callerCmdlet = CallerCmdlet.CompressPSResource;
            _cmdOperation = "Compress";
            _cmdletPassedIn = cmdlet;
            Path = path;
            DestinationPath = destinationPath;
            PassThru = passThru;
            SkipModuleManifestValidate = skipModuleManifestValidate;
            outputDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
            outputNupkgDir = destinationPath;
        }

        internal PublishHelper(PSCmdlet cmdlet,
            PSCredential credential,
            string apiKey,
            string path,
            string destinationPath,
            bool skipModuleManifestValidate,
            CancellationToken cancellationToken,
            bool isNupkgPathSpecified)
        {
            _callerCmdlet = CallerCmdlet.PublishPSResource;
            _cmdOperation = "Publish";
            _cmdletPassedIn = cmdlet;
            Credential = credential;
            ApiKey = apiKey;
            Path = path;
            DestinationPath = destinationPath;
            SkipModuleManifestValidate = skipModuleManifestValidate;
            _cancellationToken = cancellationToken;
            _isNupkgPathSpecified = isNupkgPathSpecified;
            outputDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
            outputNupkgDir = System.IO.Path.Combine(outputDir, "nupkg");
        }

        #endregion

        #region Internal Methods

        internal void PackResource()
        {
            // Returns the name of the file or the name of the directory, depending on path
            if (!_cmdletPassedIn.ShouldProcess(string.Format("'{0}' from the machine", resolvedPath)))
            {
                _cmdletPassedIn.WriteVerbose("ShouldProcess is set to false.");
                ShouldProcess = false;
                return;
            }

            parsedMetadata = new Hashtable(StringComparer.OrdinalIgnoreCase);
            if (resourceType == ResourceType.Script)
                {
                    if (!PSScriptFileInfo.TryTestPSScriptFileInfo(
                        scriptFileInfoPath: pathToScriptFileToPublish,
                        parsedScript: out PSScriptFileInfo scriptToPublish,
                        out ErrorRecord[] errors,
                        out string[] _
                    ))
                    {
                        foreach (ErrorRecord error in errors)
                        {
                            _cmdletPassedIn.WriteError(error);
                        }

                        ScriptError = true;

                        return;
                    }

                    parsedMetadata = scriptToPublish.ToHashtable();

                    _pkgName = System.IO.Path.GetFileNameWithoutExtension(pathToScriptFileToPublish);
            }
            else
            {
                if (!string.IsNullOrEmpty(pathToModuleManifestToPublish))
                {
                    _pkgName = System.IO.Path.GetFileNameWithoutExtension(pathToModuleManifestToPublish);
                }
                else
                {
                    // Search for module manifest
                    foreach (FileInfo file in new DirectoryInfo(pathToModuleDirToPublish).EnumerateFiles())
                    {
                        if (file.Name.EndsWith(PSDataFileExt, StringComparison.OrdinalIgnoreCase))
                        {
                            pathToModuleManifestToPublish = file.FullName;
                            _pkgName = System.IO.Path.GetFileNameWithoutExtension(file.Name);

                            break;
                        }
                    }
                }

                // Validate that there's a module manifest
                if (!File.Exists(pathToModuleManifestToPublish))
                {
                    _cmdletPassedIn.WriteError(new ErrorRecord(
                        new ArgumentException($"No file with a .psd1 extension was found in '{pathToModuleManifestToPublish}'. Please specify a path to a valid module manifest."),
                        "moduleManifestNotFound",
                        ErrorCategory.ObjectNotFound,
                        this));

                    return;
                }

                // The Test-ModuleManifest currently cannot process UNC paths. Disabling verification for now.
                if ((new Uri(pathToModuleManifestToPublish)).IsUnc)
                    SkipModuleManifestValidate = true;

                // Validate that the module manifest has correct data
                if (!SkipModuleManifestValidate &&
                    !Utils.ValidateModuleManifest(pathToModuleManifestToPublish, out string errorMsg))
                {
                    _cmdletPassedIn.ThrowTerminatingError(new ErrorRecord(
                        new PSInvalidOperationException(errorMsg),
                        "InvalidModuleManifest",
                        ErrorCategory.InvalidOperation,
                        this));
                }

                if (!Utils.TryReadManifestFile(
                    manifestFilePath: pathToModuleManifestToPublish,
                    manifestInfo: out parsedMetadata,
                    error: out Exception manifestReadError))
                {
                    _cmdletPassedIn.WriteError(new ErrorRecord(
                        manifestReadError,
                        "ManifestFileReadParseForContainerRegistryPublishError",
                        ErrorCategory.ReadError,
                        this));

                    return;
                }

            }

            // Create a temp folder to push the nupkg to and delete it later
            try
            {
                Directory.CreateDirectory(outputDir);
            }
            catch (Exception e)
            {
                _cmdletPassedIn.WriteError(new ErrorRecord(
                    new ArgumentException(e.Message),
                    "ErrorCreatingTempDir",
                    ErrorCategory.InvalidData,
                    this));

                return;
            }

            try
            {
                string nuspec = string.Empty;

                // Create a nuspec
                try
                {
                    nuspec = CreateNuspec(
                        outputDir: outputDir,
                        filePath: (resourceType == ResourceType.Script) ? pathToScriptFileToPublish : pathToModuleManifestToPublish,
                        parsedMetadataHash: parsedMetadata,
                        requiredModules: out dependencies);
                }
                catch (Exception e)
                {
                    _cmdletPassedIn.WriteError(new ErrorRecord(
                        new ArgumentException($"Nuspec creation failed: {e.Message}"),
                        "NuspecCreationFailed",
                        ErrorCategory.ObjectNotFound,
                        this));

                    return;
                }

                if (string.IsNullOrEmpty(nuspec))
                {
                    // nuspec creation failed.
                    _cmdletPassedIn.WriteVerbose("Nuspec creation failed.");
                    return;
                }

                if (resourceType == ResourceType.Script)
                {
                    // copy the script file to the temp directory
                    File.Copy(pathToScriptFileToPublish, System.IO.Path.Combine(outputDir, _pkgName + PSScriptFileExt), true);
                }
                else
                {
                    try
                    {
                        // If path is pointing to a file, get the parent directory, otherwise assumption is that path is pointing to the root directory
                        string rootModuleDir = !string.IsNullOrEmpty(pathToModuleManifestToPublish) ? System.IO.Path.GetDirectoryName(pathToModuleManifestToPublish) : pathToModuleDirToPublish;

                        // Create subdirectory structure in temp folder
                        foreach (string dir in Directory.GetDirectories(rootModuleDir, "*", SearchOption.AllDirectories))
                        {
                            var dirName = dir.Substring(rootModuleDir.Length).Trim(_PathSeparators);
                            Directory.CreateDirectory(System.IO.Path.Combine(outputDir, dirName));
                        }

                        // Copy files over to temp folder
                        foreach (string fileNamePath in Directory.GetFiles(rootModuleDir, "*", SearchOption.AllDirectories))
                        {
                            var fileName = fileNamePath.Substring(rootModuleDir.Length).Trim(_PathSeparators);
                            var newFilePath = System.IO.Path.Combine(outputDir, fileName);

                            // The user may have a .nuspec defined in the module directory
                            // If that's the case, we will not use that file and use the .nuspec that is generated via PSGet
                            // The .nuspec that is already in in the output directory is the one that was generated via the CreateNuspec method
                            if (!File.Exists(newFilePath))
                            {
                                File.Copy(fileNamePath, newFilePath);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        _cmdletPassedIn.ThrowTerminatingError(new ErrorRecord(
                            new ArgumentException("Error occured while creating directory to publish: " + e.Message),
                            "ErrorCreatingDirectoryToPublish",
                            ErrorCategory.InvalidOperation,
                            this));
                    }
                }

                // pack into .nupkg
                if (!PackNupkg(outputDir, outputNupkgDir, nuspec, out ErrorRecord packNupkgError))
                {
                    _cmdletPassedIn.WriteError(packNupkgError);
                    // exit out of processing
                    return;
                }
            }
            catch (Exception e)
            {
                _cmdletPassedIn.ThrowTerminatingError(new ErrorRecord(
                    e,
                    $"{this.GetType()}Error",
                    ErrorCategory.NotSpecified,
                    this));
            }
            finally
            {
                if(_callerCmdlet == CallerCmdlet.CompressPSResource)
                {
                    _cmdletPassedIn.WriteVerbose(string.Format("Deleting temporary directory '{0}'", outputDir));
                    Utils.DeleteDirectory(outputDir);
                }
            }
        }

        internal void PushResource(string Repository, string modulePrefix, bool SkipDependenciesCheck, NetworkCredential _networkCrendential)
        {
            try
            {
                PSRepositoryInfo repository = RepositorySettings.Read(new[] { Repository }, out _).FirstOrDefault();
                // Find repository
                if (repository == null)
                {
                    _cmdletPassedIn.WriteError(new ErrorRecord(
                        new ArgumentException($"The resource repository '{Repository}' is not a registered. Please run 'Register-PSResourceRepository' in order to publish to this repository."),
                        "RepositoryNotFound",
                        ErrorCategory.ObjectNotFound,
                        this));

                    return;
                }
                else if (repository.Uri.Scheme == Uri.UriSchemeFile && !repository.Uri.IsUnc && !Directory.Exists(repository.Uri.LocalPath))
                {
                    // this check to ensure valid local path is not for UNC paths (which are server based, instead of Drive based)
                    _cmdletPassedIn.WriteError(new ErrorRecord(
                        new ArgumentException($"The repository '{repository.Name}' with uri: '{repository.Uri.AbsoluteUri}' is not a valid folder path which exists. If providing a file based repository, provide a repository with a path that exists."),
                        "repositoryPathDoesNotExist",
                        ErrorCategory.ObjectNotFound,
                        this));

                    return;
                }

                _networkCredential = Utils.SetNetworkCredential(repository, _networkCredential, _cmdletPassedIn);

                // Check if dependencies already exist within the repo if:
                // 1) the resource to publish has dependencies and
                // 2) the -SkipDependenciesCheck flag is not passed in
                if (dependencies != null && !SkipDependenciesCheck)
                {
                    // If error gets thrown, exit process record
                    if (!CheckDependenciesExist(dependencies, repository.Name))
                    {
                        return;
                    }
                }

                // If -DestinationPath is specified then also publish the .nupkg there
                if (!string.IsNullOrWhiteSpace(DestinationPath))
                {
                    if (!Directory.Exists(DestinationPath))
                    {
                        _cmdletPassedIn.WriteError(new ErrorRecord(
                            new ArgumentException($"Destination path does not exist: '{DestinationPath}'"),
                            "InvalidDestinationPath",
                            ErrorCategory.InvalidArgument,
                            this));

                        return;
                    }

                    if (!_isNupkgPathSpecified)
                    {
                        try
                        {
                            var nupkgName = _pkgName + "." + _pkgVersion.ToNormalizedString() + ".nupkg";
                            var sourceFilePath = System.IO.Path.Combine(outputNupkgDir, nupkgName);
                            var destinationFilePath = System.IO.Path.Combine(DestinationPath, nupkgName);

                            if (!File.Exists(destinationFilePath))
                            {
                                File.Copy(sourceFilePath, destinationFilePath);
                            }
                        }
                        catch (Exception e)
                        {
                            _cmdletPassedIn.WriteError(new ErrorRecord(
                                new ArgumentException($"Error moving .nupkg into destination path '{DestinationPath}' due to: '{e.Message}'."),
                                "ErrorMovingNupkg",
                                ErrorCategory.NotSpecified,
                                this));

                            // exit process record
                            return;
                        }
                    }
                }

                string repositoryUri = repository.Uri.AbsoluteUri;

                if (repository.ApiVersion == PSRepositoryInfo.APIVersion.ContainerRegistry)
                {
                    ContainerRegistryServerAPICalls containerRegistryServer = new ContainerRegistryServerAPICalls(repository, _cmdletPassedIn, _networkCredential, userAgentString);

                    var pkgMetadataFile = (resourceType == ResourceType.Script) ? pathToScriptFileToPublish : pathToModuleManifestToPublish;

                    if (!containerRegistryServer.PushNupkgContainerRegistry(pkgMetadataFile, outputNupkgDir, _pkgName, modulePrefix, _pkgVersion, resourceType, parsedMetadata, dependencies, out ErrorRecord pushNupkgContainerRegistryError))
                    {
                        _cmdletPassedIn.WriteError(pushNupkgContainerRegistryError);
                        // exit out of processing
                        return;
                    }
                }
                else
                {
                    if(_isNupkgPathSpecified)
                    {
                        outputNupkgDir = pathToNupkgToPublish;
                    }
                    // This call does not throw any exceptions, but it will write unsuccessful responses to the console
                    if (!PushNupkg(outputNupkgDir, repository.Name, repository.Uri.ToString(), out ErrorRecord pushNupkgError))
                    {
                        _cmdletPassedIn.WriteError(pushNupkgError);
                        // exit out of processing
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                _cmdletPassedIn.ThrowTerminatingError(new ErrorRecord(
                            e,
                            "PublishPSResourceError",
                            ErrorCategory.NotSpecified,
                            this));
            }
            finally
            {
                if (!_isNupkgPathSpecified)
                {
                    _cmdletPassedIn.WriteVerbose(string.Format("Deleting temporary directory '{0}'", outputDir));
                    Utils.DeleteDirectory(outputDir);
                }
            }
        }

        internal void CheckAllParameterPaths()
        {
            try
            {
                resolvedPath = _cmdletPassedIn.GetResolvedProviderPathFromPSPath(Path, out ProviderInfo provider).First();
            }
            catch (MethodInvocationException)
            {
                // path does not exist
                _cmdletPassedIn.ThrowTerminatingError(new ErrorRecord(
                    new ArgumentException($"The path to the resource to {_cmdOperation.ToLower()} does not exist, point to an existing path or file of the module or script to {_cmdOperation.ToLower()}."),
                    "SourcePathDoesNotExist",
                    ErrorCategory.InvalidArgument,
                    this));
            }

            // Condition 1: path is to the root directory of the module to be published
            // Condition 2: path is to the .psd1 or .ps1 of the module/script to be published
            if (string.IsNullOrEmpty(resolvedPath))
            {
                // unsupported file path
                _cmdletPassedIn.ThrowTerminatingError(new ErrorRecord(
                    new ArgumentException($"The path to the resource to {_cmdOperation.ToLower()} is not in the correct format or does not exist. Please provide the path of the root module " +
                        $"(i.e. './<ModuleTo{_cmdOperation}>/') or the path to the .psd1 (i.e. './<ModuleTo{_cmdOperation}>/<ModuleTo{_cmdOperation}>.psd1')."),
                    $"Invalid{_cmdOperation}Path",
                    ErrorCategory.InvalidArgument,
                    this));
            }
            else if (Directory.Exists(resolvedPath))
            {
                pathToModuleDirToPublish = resolvedPath;
                resourceType = ResourceType.Module;
            }
            else if (resolvedPath.EndsWith(PSDataFileExt, StringComparison.OrdinalIgnoreCase))
            {
                pathToModuleManifestToPublish = resolvedPath;
                resourceType = ResourceType.Module;
            }
            else if (resolvedPath.EndsWith(PSScriptFileExt, StringComparison.OrdinalIgnoreCase))
            {
                pathToScriptFileToPublish = resolvedPath;
                resourceType = ResourceType.Script;
            }
            else if (resolvedPath.EndsWith(NupkgFileExt, StringComparison.OrdinalIgnoreCase) && _isNupkgPathSpecified)
            {
                pathToNupkgToPublish = resolvedPath;
                resourceType = ResourceType.Nupkg;
            }
            else
            {
                _cmdletPassedIn.ThrowTerminatingError(new ErrorRecord(
                    new ArgumentException($"The {_cmdOperation.ToLower()} path provided, '{resolvedPath}', is not a valid. Please provide a path to the root module " +
                        $"(i.e. './<ModuleTo{_cmdOperation}>/') or path to the .psd1 (i.e. './<ModuleTo{_cmdOperation}>/<ModuleTo{_cmdOperation}>.psd1')."),
                    $"Invalid{_cmdOperation}Path",
                    ErrorCategory.InvalidArgument,
                    this));
            }

            if (!String.IsNullOrEmpty(DestinationPath))
            {
                string resolvedDestinationPath = _cmdletPassedIn.GetResolvedProviderPathFromPSPath(DestinationPath, out ProviderInfo provider).First();

                if (Directory.Exists(resolvedDestinationPath))
                {
                    DestinationPath = resolvedDestinationPath;
                }
                else
                {
                    try
                    {
                        Directory.CreateDirectory(resolvedDestinationPath);
                    }
                    catch (Exception e)
                    {
                        _cmdletPassedIn.ThrowTerminatingError(new ErrorRecord(
                            new ArgumentException($"Destination path does not exist and cannot be created: {e.Message}"),
                            "InvalidDestinationPath",
                            ErrorCategory.InvalidArgument,
                            this));
                    }
                }
            }
        }

        #endregion

        #region Private Methods

        private bool PackNupkg(string outputDir, string outputNupkgDir, string nuspecFile, out ErrorRecord error)
        {
            _cmdletPassedIn.WriteDebug("In PublishHelper::PackNupkg()");
            // Pack the module or script into a nupkg given a nuspec.
            var builder = new PackageBuilder();
            try
            {
                var runner = new PackCommandRunner(
                    new PackArgs
                    {
                        CurrentDirectory = outputDir,
                        OutputDirectory = outputNupkgDir,
                        Path = nuspecFile,
                        Exclude = System.Array.Empty<string>(),
                        Symbols = false,
                        Logger = NullLogger.Instance
                    },
                    MSBuildProjectFactory.ProjectCreator,
                    builder);
                bool success = runner.RunPackageBuild();

                if (success)
                {
                    if (PassThru)
                    {
                        _cmdletPassedIn.WriteObject(System.IO.Path.Combine(outputNupkgDir, _pkgName + "." + _pkgVersion.ToNormalizedString() + ".nupkg"));
                    }
                    _cmdletPassedIn.WriteVerbose("Successfully packed the resource into a .nupkg");
                }
                else
                {
                    error = new ErrorRecord(
                        new InvalidOperationException("Not able to successfully pack the resource into a .nupkg"),
                            "failedToPackIntoNupkg",
                            ErrorCategory.ObjectNotFound,
                            this);

                    return false;
                }
            }
            catch (Exception e)
            {
                error = new ErrorRecord(
                    new ArgumentException($"Unexpected error packing into .nupkg: '{e.Message}'."),
                    "ErrorPackingIntoNupkg",
                    ErrorCategory.NotSpecified,
                    this);

                // exit process record
                return false;
            }

            error = null;
            return true;
        }

        private bool PushNupkg(string outputNupkgDir, string repoName, string repoUri, out ErrorRecord error)
        {
            _cmdletPassedIn.WriteDebug("In PublishPSResource::PushNupkg()");

            string fullNupkgFile;
            if (_isNupkgPathSpecified)
            {
                fullNupkgFile = outputNupkgDir;
            }
            else
            {
                // Push the nupkg to the appropriate repository
                // Pkg version is parsed from .ps1 file or .psd1 file
                fullNupkgFile = System.IO.Path.Combine(outputNupkgDir, _pkgName + "." + _pkgVersion.ToNormalizedString() + ".nupkg");
            }

            // The PSGallery uses the v2 protocol still and publishes to a slightly different endpoint:
            // "https://www.powershellgallery.com/api/v2/package"
            // Until the PSGallery is moved onto the NuGet v3 server protocol, we'll modify the repository uri
            // to accommodate for the approprate publish location.
            string publishLocation = repoUri.EndsWith("/v2", StringComparison.OrdinalIgnoreCase) ? repoUri + "/package" : repoUri;

            var settings = NuGet.Configuration.Settings.LoadDefaultSettings(null, null, null);
            var success = false;

            var sourceProvider = new PackageSourceProvider(settings);
            if (Credential != null || _networkCredential != null)
            {
                InjectCredentialsToSettings(settings, sourceProvider, publishLocation);
            }


            try
            {
                PushRunner.Run(
                        settings: Settings.LoadDefaultSettings(root: null, configFileName: null, machineWideSettings: null),
                        sourceProvider: sourceProvider,
                        packagePaths: new List<string> { fullNupkgFile },
                        source: publishLocation,
                        apiKey: ApiKey,
                        symbolSource: null,
                        symbolApiKey: null,
                        timeoutSeconds: 0,
                        disableBuffering: false,
                        noSymbols: false,
                        noServiceEndpoint: false,  // enable server endpoint
                        skipDuplicate: false, // if true-- if a package and version already exists, skip it and continue with the next package in the push, if any.
                        logger: NullLogger.Instance // nuget logger
                        ).GetAwaiter().GetResult();
            }
            catch (HttpRequestException e)
            {
                _cmdletPassedIn.WriteVerbose(string.Format("Not able to publish resource to '{0}'", repoUri));
                //  look in PS repo for how httpRequestExceptions are handled

                // Unfortunately there is no response message  are no status codes provided with the exception and no
                var ex = new ArgumentException(String.Format("Repository '{0}': {1}", repoName, e.Message));
                if (e.Message.Contains("400"))
                {
                    if (e.Message.Contains("Api"))
                    {
                        // For ADO repositories, public and private, when ApiKey is not provided.
                        error = new ErrorRecord(
                            new ArgumentException($"Repository '{repoName}': Please try running again with the -ApiKey parameter and specific API key for the repository specified. For Azure Devops repository, set this to an arbitrary value, for example '-ApiKey AzureDevOps'"),
                            "400ApiKeyError",
                            ErrorCategory.AuthenticationError,
                            this);
                    }
                    else
                    {
                        error = new ErrorRecord(
                            ex,
                            "400Error",
                            ErrorCategory.PermissionDenied,
                            this);
                    }
                }
                else if (e.Message.Contains("401"))
                {
                    if (e.Message.Contains("API"))
                    {
                        // For PSGallery when ApiKey is not provided.
                        error = new ErrorRecord(
                            new ArgumentException($"Could not publish to repository '{repoName}'. Please try running again with the -ApiKey parameter and the API key for the repository specified. Exception: '{e.Message}'"),
                            "401ApiKeyError",
                            ErrorCategory.AuthenticationError,
                            this);
                    }
                    else
                    {
                        // For ADO repository feeds that are public feeds, when the credentials are incorrect.
                        error = new ErrorRecord(new ArgumentException($"Could not publish to repository '{repoName}'. The Credential provided was incorrect. Exception: '{e.Message}'"),
                            "401Error",
                            ErrorCategory.PermissionDenied,
                            this); ;
                    }
                }
                else if (e.Message.Contains("403"))
                {
                    if (repoUri.Contains("myget.org"))
                    {
                        // For myGet.org repository feeds when the ApiKey is missing or incorrect.
                        error = new ErrorRecord(
                            new ArgumentException($"Could not publish to repository '{repoName}'. The ApiKey provided is incorrect or missing. Please try running again with the -ApiKey parameter and correct API key value for the repository. Exception: '{e.Message}'"),
                            "403Error",
                            ErrorCategory.PermissionDenied,
                            this);
                    }
                    else if (repoUri.Contains(".jfrog.io"))
                    {
                        // For JFrog Artifactory repository feeds when the ApiKey is provided, whether correct or incorrect, as JFrog does not require -ApiKey (but does require ApiKey to be present as password to -Credential).
                        error = new ErrorRecord(
                            new ArgumentException($"Could not publish to repository '{repoName}'. The ApiKey provided is not needed for JFrog Artifactory. Please try running again without the -ApiKey parameter but ensure that -Credential is provided with ApiKey as password. Exception: '{e.Message}'"),
                            "403Error",
                            ErrorCategory.PermissionDenied,
                            this);
                    }
                    else
                    {
                        error = new ErrorRecord(
                            ex,
                            "403Error",
                            ErrorCategory.PermissionDenied,
                            this);
                    }
                }
                else if (e.Message.Contains("409"))
                {
                    error = new ErrorRecord(
                        ex,
                        "409Error",
                        ErrorCategory.PermissionDenied, this);
                }
                else
                {
                    error = new ErrorRecord(
                        ex,
                        "HTTPRequestError",
                        ErrorCategory.PermissionDenied,
                        this);
                }

                return success;
            }
            catch (NuGet.Protocol.Core.Types.FatalProtocolException e)
            {
                //  for ADO repository feeds that are private feeds the error thrown is different and the 401 is in the inner exception message
                if (e.InnerException.Message.Contains("401"))
                {
                    error = new ErrorRecord(
                        new ArgumentException($"Could not publish to repository '{repoName}'. The Credential provided was incorrect. Exception '{e.InnerException.Message}'"),
                        "401FatalProtocolError",
                        ErrorCategory.AuthenticationError,
                        this);
                }
                else
                {
                    error = new ErrorRecord(
                        new ArgumentException($"Repository '{repoName}': {e.InnerException.Message}"),
                        "ProtocolFailError",
                        ErrorCategory.ProtocolError,
                        this);
                }

                return success;
            }
            catch (Exception e)
            {
                _cmdletPassedIn.WriteVerbose($"Not able to publish resource to '{repoUri}'");
                error = new ErrorRecord(
                    new ArgumentException(e.Message),
                    "PushNupkgError",
                    ErrorCategory.InvalidResult,
                    this);

                return success;
            }

            _cmdletPassedIn.WriteVerbose(string.Format("Successfully published the resource to '{0}'", repoUri));
            error = null;
            success = true;

            return success;
        }

        private void InjectCredentialsToSettings(ISettings settings, IPackageSourceProvider sourceProvider, string source)
        {
            _cmdletPassedIn.WriteDebug("In PublishPSResource::InjectCredentialsToSettings()");
            if (Credential == null && _networkCredential == null)
            {
                return;
            }

            var packageSource = sourceProvider.LoadPackageSources().FirstOrDefault(s => s.Source == source);
            if (packageSource != null)
            {
                if (!packageSource.IsEnabled)
                {
                    packageSource.IsEnabled = true;
                }
            }


            var networkCred = Credential == null ? _networkCredential : Credential.GetNetworkCredential();
            string key;

            if (packageSource == null)

            {
                key = "_" + Guid.NewGuid().ToString().Replace("-", "");
                settings.AddOrUpdate(
                    ConfigurationConstants.PackageSources,
                    new SourceItem(key, source));
            }
            else
            {
                key = packageSource.Name;
            }

            settings.AddOrUpdate(
            ConfigurationConstants.CredentialsSectionName,
            new CredentialsItem(
                key,
                networkCred.UserName,
                networkCred.Password,
                isPasswordClearText: true,
                String.Empty));
        }

        private string CreateNuspec(
            string outputDir,
            string filePath,
            Hashtable parsedMetadataHash,
            out Hashtable requiredModules)
        {
            _cmdletPassedIn.WriteDebug("In PublishHelper::CreateNuspec()");

            bool isModule = resourceType != ResourceType.Script;
            requiredModules = new Hashtable();

            if (parsedMetadataHash == null || parsedMetadataHash.Count == 0)
            {
                _cmdletPassedIn.WriteError(new ErrorRecord(new ArgumentException("Hashtable provided with package metadata was null or empty"),
                    "PackageMetadataHashtableNullOrEmptyError",
                    ErrorCategory.ReadError,
                    this));

                return string.Empty;
            }

            // now we have parsedMetadatahash to fill out the nuspec information
            var nameSpaceUri = "http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd";
            var doc = new XmlDocument();

            // xml declaration is recommended, but not mandatory
            XmlDeclaration xmlDeclaration = doc.CreateXmlDeclaration("1.0", "utf-8", null);
            XmlElement root = doc.DocumentElement;
            doc.InsertBefore(xmlDeclaration, root);

            // create top-level elements
            XmlElement packageElement = doc.CreateElement("package", nameSpaceUri);
            XmlElement metadataElement = doc.CreateElement("metadata", nameSpaceUri);

            Dictionary<string, string> metadataElementsDictionary = new Dictionary<string, string>();

            // id is mandatory
            metadataElementsDictionary.Add("id", _pkgName);

            string version;
            if (parsedMetadataHash.ContainsKey("moduleversion"))
            {
                version = parsedMetadataHash["moduleversion"].ToString();
            }
            else if (parsedMetadataHash.ContainsKey("version"))
            {
                version = parsedMetadataHash["version"].ToString();
            }
            else
            {
                // no version is specified for the nuspec
                _cmdletPassedIn.WriteError(new ErrorRecord(
                    new ArgumentException("There is no package version specified. Please specify a version before publishing."),
                    "NoVersionFound",
                    ErrorCategory.InvalidArgument,
                    this));

                return string.Empty;
            }

            // Look for Prerelease tag and then process any Tags in PrivateData > PSData
            if (isModule)
            {
                if (parsedMetadataHash.ContainsKey("PrivateData"))
                {
                    if (parsedMetadataHash["PrivateData"] is Hashtable privateData &&
                        privateData.ContainsKey("PSData"))
                    {
                        if (privateData["PSData"] is Hashtable psData)
                        {
                            if (psData.ContainsKey("prerelease") && psData["prerelease"] is string preReleaseVersion)
                            {
                                if (!string.IsNullOrEmpty(preReleaseVersion))
                                {
                                    version = string.Format(@"{0}-{1}", version, preReleaseVersion);
                                }
                            }

                            if (psData.ContainsKey("licenseuri") && psData["licenseuri"] is string licenseUri)

                            {
                                metadataElementsDictionary.Add("licenseUrl", licenseUri.Trim());
                            }

                            if (psData.ContainsKey("projecturi") && psData["projecturi"] is string projectUri)
                            {
                                metadataElementsDictionary.Add("projectUrl", projectUri.Trim());
                            }

                            if (psData.ContainsKey("iconuri") && psData["iconuri"] is string iconUri)
                            {
                                metadataElementsDictionary.Add("iconUrl", iconUri.Trim());
                            }

                            if (psData.ContainsKey("releasenotes"))
                            {
                                if (psData["releasenotes"] is string releaseNotes)
                                {
                                    metadataElementsDictionary.Add("releaseNotes", releaseNotes.Trim());
                                }
                                else if (psData["releasenotes"] is string[] releaseNotesArr)
                                {
                                    metadataElementsDictionary.Add("releaseNotes", string.Join("\n", releaseNotesArr));
                                }
                            }

                            // defaults to false
                            // Value for requireAcceptLicense key needs to be a lowercase string representation of the boolean for it to be correctly parsed from psData file.

                            string requireLicenseAcceptance = psData.ContainsKey("requirelicenseacceptance") ? psData["requirelicenseacceptance"].ToString().ToLower() : "false";

                            metadataElementsDictionary.Add("requireLicenseAcceptance", requireLicenseAcceptance);


                            if (psData.ContainsKey("Tags") && psData["Tags"] is Array manifestTags)
                            {
                                var tagArr = new List<string>();
                                foreach (string tag in manifestTags)
                                {
                                    tagArr.Add(tag);
                                }
                                parsedMetadataHash["tags"] = string.Join(" ", tagArr.ToArray());
                            }
                        }
                    }
                }
            }
            else
            {
                if (parsedMetadataHash.ContainsKey("licenseuri") && parsedMetadataHash["licenseuri"] is Uri licenseUri)

                {
                    metadataElementsDictionary.Add("licenseUrl", licenseUri.ToString().Trim());
                }

                if (parsedMetadataHash.ContainsKey("projecturi") && parsedMetadataHash["projecturi"] is Uri projectUri)
                {
                    metadataElementsDictionary.Add("projectUrl", projectUri.ToString().Trim());
                }

                if (parsedMetadataHash.ContainsKey("iconuri") && parsedMetadataHash["iconuri"] is Uri iconUri)
                {
                    metadataElementsDictionary.Add("iconUrl", iconUri.ToString().Trim());
                }

                if (parsedMetadataHash.ContainsKey("releaseNotes"))
                {
                    if (parsedMetadataHash["releasenotes"] is string releaseNotes)
                    {
                        metadataElementsDictionary.Add("releaseNotes", releaseNotes.Trim());
                    }
                    else if (parsedMetadataHash["releasenotes"] is string[] releaseNotesArr)
                    {
                        metadataElementsDictionary.Add("releaseNotes", string.Join("\n", releaseNotesArr));
                    }
                }
            }


            if (NuGetVersion.TryParse(version, out _pkgVersion))
            {
                metadataElementsDictionary.Add("version", _pkgVersion.ToNormalizedString());
            }

            if (parsedMetadataHash.ContainsKey("author"))
            {
                metadataElementsDictionary.Add("authors", parsedMetadataHash["author"].ToString().Trim());
            }

            if (parsedMetadataHash.ContainsKey("companyname"))
            {
                metadataElementsDictionary.Add("owners", parsedMetadataHash["companyname"].ToString().Trim());
            }

            if (parsedMetadataHash.ContainsKey("description"))
            {
                metadataElementsDictionary.Add("description", parsedMetadataHash["description"].ToString().Trim());
            }

            if (parsedMetadataHash.ContainsKey("copyright"))
            {
                metadataElementsDictionary.Add("copyright", parsedMetadataHash["copyright"].ToString().Trim());
            }

            string tags = (resourceType == ResourceType.Script) ? "PSScript" : "PSModule";
            if (parsedMetadataHash.ContainsKey("tags") && parsedMetadataHash["tags"] != null)
            {
                if (parsedMetadataHash["tags"] is string[])
                {
                    string[] tagsArr = parsedMetadataHash["tags"] as string[];
                    tags += " " + String.Join(" ", tagsArr);
                }
                else if (parsedMetadataHash["tags"] is string)
                {
                    tags += " " + parsedMetadataHash["tags"].ToString().Trim();
                }
            }

            metadataElementsDictionary.Add("tags", tags);


            // Example nuspec:
            /*
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <id>System.Management.Automation</id>
                <version>1.0.0</version>
                <authors>Microsoft</authors>
                <owners>Microsoft,PowerShell</owners>
                <requireLicenseAcceptance>false</requireLicenseAcceptance>
                <license type="expression">MIT</license>
                <license>https://licenses.nuget.org/MIT</license>
                <icon>Powershell_black_64.png</icon>
                <projectUrl>https://github.com/PowerShell/PowerShell</projectUrl>
                <description>Example description here</description>
                <copyright>Copyright (c) Microsoft Corporation. All rights reserved.</copyright>
                <language>en-US</language>
                <tags>PowerShell</tags>
                <dependencies>
                  <group targetFramework="net5.0">
                    <dependency id="Microsoft.PowerShell.CoreCLR.Eventing" version="7.1.3" />
                    <dependency id="Microsoft.PowerShell.Native" version="7.1.0" />
                  </group>
                </dependencies>
              </ metadata >
            </ package >
            */

            foreach (var key in metadataElementsDictionary.Keys)
            {
                if (metadataElementsDictionary.TryGetValue(key, out string elementInnerText))
                {
                    XmlElement element = doc.CreateElement(key, nameSpaceUri);
                    element.InnerText = elementInnerText;
                    metadataElement.AppendChild(element);
                }
                else
                {
                    _cmdletPassedIn.WriteVerbose(string.Format("Creating XML element failed. Unable to get value from key '{0}'.", key));
                }
            }

            requiredModules = ParseRequiredModules(parsedMetadataHash);
            if (requiredModules != null)
            {
                XmlElement dependenciesElement = doc.CreateElement("dependencies", nameSpaceUri);

                foreach (string dependencyName in requiredModules.Keys)
                {
                    XmlElement element = doc.CreateElement("dependency", nameSpaceUri);

                    element.SetAttribute("id", dependencyName);
                    string dependencyVersion = requiredModules[dependencyName].ToString();
                    if (!string.IsNullOrEmpty(dependencyVersion))
                    {
                        element.SetAttribute("version", requiredModules[dependencyName].ToString());
                    }

                    dependenciesElement.AppendChild(element);
                }
                metadataElement.AppendChild(dependenciesElement);
            }

            packageElement.AppendChild(metadataElement);
            doc.AppendChild(packageElement);

            var nuspecFullName = System.IO.Path.Combine(outputDir, _pkgName + ".nuspec");
            doc.Save(nuspecFullName);

            _cmdletPassedIn.WriteVerbose("The newly created nuspec is: " + nuspecFullName);

            return nuspecFullName;
        }

        private Hashtable ParseRequiredModules(Hashtable parsedMetadataHash)
        {
            _cmdletPassedIn.WriteDebug("In PublishHelper::ParseRequiredModules()");

            if (!parsedMetadataHash.ContainsKey("requiredmodules"))
            {
                return null;
            }

            LanguagePrimitives.TryConvertTo<object[]>(parsedMetadataHash["requiredmodules"], out object[] requiredModules);

            // Required modules can be:
            //  a. An array of hash tables of module name and version
            //  b. A single hash table of module name and version
            //  c. A string array of module names
            //  d. A single string module name

            var dependenciesHash = new Hashtable();
            foreach (var reqModule in requiredModules)
            {
                if (LanguagePrimitives.TryConvertTo<Hashtable>(reqModule, out Hashtable moduleHash))
                {
                    string moduleName = moduleHash["ModuleName"] as string;

                    if (moduleHash.ContainsKey("ModuleVersion"))
                    {
                        dependenciesHash.Add(moduleName, moduleHash["ModuleVersion"]);
                    }
                    else if (moduleHash.ContainsKey("RequiredVersion"))
                    {
                        dependenciesHash.Add(moduleName, moduleHash["RequiredVersion"]);
                    }
                    else
                    {
                        dependenciesHash.Add(moduleName, string.Empty);
                    }
                }
                else if (LanguagePrimitives.TryConvertTo<string>(reqModule, out string moduleName))
                {
                    dependenciesHash.Add(moduleName, string.Empty);
                }
            }

            var externalModuleDeps = parsedMetadataHash.ContainsKey("ExternalModuleDependencies") ?
                        parsedMetadataHash["ExternalModuleDependencies"] : null;

            if (externalModuleDeps != null && LanguagePrimitives.TryConvertTo<string[]>(externalModuleDeps, out string[] externalModuleNames))
            {
                foreach (var extModName in externalModuleNames)
                {
                    if (dependenciesHash.ContainsKey(extModName))
                    {
                        dependenciesHash.Remove(extModName);
                    }
                }
            }

            return dependenciesHash;
        }

        private bool CheckDependenciesExist(Hashtable dependencies, string repositoryName)
        {
            _cmdletPassedIn.WriteDebug("In PublishHelper::CheckDependenciesExist()");

            // Check to see that all dependencies are in the repository
            // Searches for each dependency in the repository the pkg is being pushed to,
            // If the dependency is not there, error
            foreach (DictionaryEntry dependency in dependencies)
            {
                // Need to make individual calls since we're look for exact version numbers or ranges.
                var depName = dependency.Key as string;
                // test version
                string depVersion = dependencies[depName] as string;
                depVersion = string.IsNullOrWhiteSpace(depVersion) ? "*" : depVersion;

                if (!Utils.TryGetVersionType(
                    version: depVersion,
                    nugetVersion: out NuGetVersion nugetVersion,
                    versionRange: out VersionRange versionRange,
                    versionType: out VersionType versionType,
                    error: out string error))
                {
                    _cmdletPassedIn.ThrowTerminatingError(new ErrorRecord(
                        new ArgumentException(error),
                        "IncorrectVersionFormat",
                        ErrorCategory.InvalidArgument,
                        this));
                }

                // Search for and return the dependency if it's in the repository.
                FindHelper findHelper = new FindHelper(_cancellationToken, _cmdletPassedIn, _networkCredential);

                var repository = new[] { repositoryName };
                // Note: we set prerelease argument for FindByResourceName() to true because if no version is specified we want latest version (including prerelease).
                // If version is specified it will get that one. There is also no way to specify a prerelease flag with RequiredModules hashtable of dependency so always try to get latest version.
                var dependencyFound = findHelper.FindByResourceName(new string[] { depName }, ResourceType.Module, versionRange, nugetVersion, versionType, depVersion, prerelease: true, tag: null, repository, includeDependencies: false, suppressErrors: true);
                if (dependencyFound == null || !dependencyFound.Any())
                {
                    _cmdletPassedIn.WriteError(new ErrorRecord(
                        new ArgumentException($"Dependency '{depName}' was not found in repository '{repositoryName}'.  Make sure the dependency is published to the repository before {_cmdOperation.ToLower()} this module."),
                        "DependencyNotFound",
                        ErrorCategory.ObjectNotFound,
                        this));

                    return false;
                }

            }

            return true;
        }

        #endregion
    }
}
