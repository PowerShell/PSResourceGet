// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Microsoft.PowerShell.PowerShellGet.UtilClasses
{
    /// <summary>
    /// The class contains basic information of a repository path settings as well as methods to
    /// perform Create/Read/Update/Delete operations on the repository store file.
    /// </summary>
    internal static class RepositorySettings
    {
        #region Members

        // File name for a user's repository store file is 'PSResourceRepository.xml'
        // The repository store file's location is currently only at '%LOCALAPPDATA%\PowerShellGet' for the user account.
        private const string PSGalleryRepoName = "PSGallery";
        private const string PSGalleryRepoUri = "https://www.powershellgallery.com/api/v2";
        private const int DefaultPriority = 50;
        private const bool DefaultTrusted = false;
        private const string RepositoryFileName = "PSResourceRepository.xml";
        private static readonly string RepositoryPath = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData), "PowerShellGet");
        private static readonly string FullRepositoryPath = Path.Combine(RepositoryPath, RepositoryFileName);

        #endregion

        #region Public methods

        /// <summary>
        /// Check if repository store xml file exists, if not then create
        /// </summary>
        public static void CheckRepositoryStore()
        {
            if (!File.Exists(FullRepositoryPath))
            {
                try
                {
                    if (!Directory.Exists(RepositoryPath))
                    {
                        Directory.CreateDirectory(RepositoryPath);
                    }

                    XDocument newRepoXML = new XDocument(
                        new XElement("configuration"));
                    newRepoXML.Save(FullRepositoryPath);
                }
                catch (Exception e)
                {
                    throw new PSInvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Repository store creation failed with error: {0}.", e.Message));
                }

                // Add PSGallery to the newly created store
                Uri psGalleryUri = new Uri(PSGalleryRepoUri);
                Add(PSGalleryRepoName, psGalleryUri, DefaultPriority, DefaultTrusted, repoCredentialInfo: null, PSRepositoryInfo.APIVersion.v2, force: false);
            }

            // Open file (which should exist now), if cannot/is corrupted then throw error
            try
            {
                LoadXDocument(FullRepositoryPath);
            }
            catch (Exception e)
            {
                throw new PSInvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Repository store may be corrupted, file reading failed with error: {0}.", e.Message));
            }
        }

        public static PSRepositoryInfo AddRepository(string repoName, Uri repoUri, int repoPriority, bool repoTrusted, PSCredentialInfo repoCredentialInfo, bool force, PSCmdlet cmdletPassedIn, out string errorMsg)
        {
            errorMsg = String.Empty;
            if (repoName.Equals("PSGallery", StringComparison.OrdinalIgnoreCase))
            {
                errorMsg = "Cannot register PSGallery with -Name parameter. Try: Register-PSResourceRepository -PSGallery";
                return null;
            }

            return AddToRepositoryStore(repoName, repoUri, repoPriority, repoTrusted, repoCredentialInfo, force, cmdletPassedIn, out errorMsg);
        }


        public static PSRepositoryInfo AddToRepositoryStore(string repoName, Uri repoUri, int repoPriority, bool repoTrusted, PSCredentialInfo repoCredentialInfo, bool force, PSCmdlet cmdletPassedIn, out string errorMsg)
        {
            errorMsg = string.Empty;
            // remove trailing and leading whitespaces, and if Name is just whitespace Name should become null now and be caught by following condition
            repoName = repoName.Trim(' ');
            if (String.IsNullOrEmpty(repoName) || repoName.Contains("*"))
            {
                throw new ArgumentException("Name cannot be null/empty, contain asterisk or be just whitespace");
            }

            if (repoUri == null || !(repoUri.Scheme == System.Uri.UriSchemeHttp || repoUri.Scheme == System.Uri.UriSchemeHttps || repoUri.Scheme == System.Uri.UriSchemeFtp || repoUri.Scheme == System.Uri.UriSchemeFile))
            {
                errorMsg = "Invalid Uri, must be one of the following Uri schemes: HTTPS, HTTP, FTP, File Based";
                return null;
            }

            PSRepositoryInfo.APIVersion apiVersion = GetRepoAPIVersion(repoUri);

            if (repoCredentialInfo != null)
            {
                bool isSecretManagementModuleAvailable = Utils.IsSecretManagementModuleAvailable(repoName, cmdletPassedIn);

                if (repoCredentialInfo.Credential != null)
                {
                    if (!isSecretManagementModuleAvailable)
                    {
                        errorMsg = $"Microsoft.PowerShell.SecretManagement module is not found, but is required for saving PSResourceRepository {repoName}'s Credential in a vault.";
                        return null;
                    }
                    else
                    {
                        Utils.SaveRepositoryCredentialToSecretManagementVault(repoName, repoCredentialInfo, cmdletPassedIn);
                    }
                }

                if (!isSecretManagementModuleAvailable)
                {
                    cmdletPassedIn.WriteWarning($"Microsoft.PowerShell.SecretManagement module cannot be found. Make sure it is installed before performing PSResource operations in order to successfully authenticate to PSResourceRepository \"{repoName}\" with its CredentialInfo.");
                }
            }

            if (!cmdletPassedIn.ShouldProcess(repoName, "Register repository to repository store"))
            {
                return null;
            }

            if (!string.IsNullOrEmpty(errorMsg))
            {
                return null;
            }

            var repo = RepositorySettings.Add(repoName, repoUri, repoPriority, repoTrusted, repoCredentialInfo, apiVersion, force);

            return repo;
        }


        public static PSRepositoryInfo UpdateRepositoryStore(string repoName, Uri repoUri, int repoPriority, bool repoTrusted, bool isSet, int defaultPriority, PSCredentialInfo repoCredentialInfo, PSCmdlet cmdletPassedIn, out string errorMsg)
        {
            errorMsg = string.Empty;
            if (repoUri != null && !(repoUri.Scheme == System.Uri.UriSchemeHttp || repoUri.Scheme == System.Uri.UriSchemeHttps || repoUri.Scheme == System.Uri.UriSchemeFtp || repoUri.Scheme == System.Uri.UriSchemeFile))
            {
                errorMsg = "Invalid Uri, Uri must be one of the following schemes: HTTPS, HTTP, FTP, File Based";
                return null;
            }

            // check repoName can't contain * or just be whitespace
            // remove trailing and leading whitespaces, and if Name is just whitespace Name should become null now and be caught by following condition
            repoName = repoName.Trim();
            if (String.IsNullOrEmpty(repoName) || repoName.Contains("*"))
            {
                errorMsg = "Name cannot be null or empty, or contain wildcards";
                return null;
            }

            // check PSGallery Uri is not trying to be set
            if (repoName.Equals("PSGallery", StringComparison.OrdinalIgnoreCase) && repoUri != null)
            {
                errorMsg = "The PSGallery repository has a predefined Uri. Setting the -Uri parameter for this repository is not allowed. Please run 'Register-PSResourceRepository -PSGallery' to register the PowerShell Gallery.";
                return null;
            }

            // check PSGallery CredentialInfo is not trying to be set
            if (repoName.Equals("PSGallery", StringComparison.OrdinalIgnoreCase) && repoCredentialInfo != null)
            {
                errorMsg = "Setting the -CredentialInfo parameter for PSGallery is not allowed. Run 'Register-PSResourceRepository -PSGallery' to register the PowerShell Gallery.";
                return null;
            }

            // determine trusted value to pass in (true/false if set, null otherwise, hence the nullable bool variable)
            bool? _trustedNullable = isSet ? new bool?(repoTrusted) : new bool?();

            if (repoCredentialInfo != null)
            {
                bool isSecretManagementModuleAvailable = Utils.IsSecretManagementModuleAvailable(repoName, cmdletPassedIn);

                if (repoCredentialInfo.Credential != null)
                {
                    if (!isSecretManagementModuleAvailable)
                    {
                        errorMsg = $"Microsoft.PowerShell.SecretManagement module is not found, but is required for saving PSResourceRepository {repoName}'s Credential in a vault.";
                        return null;
                    }
                    else
                    {
                        Utils.SaveRepositoryCredentialToSecretManagementVault(repoName, repoCredentialInfo, cmdletPassedIn);
                    }
                }

                if (!isSecretManagementModuleAvailable)
                {
                    cmdletPassedIn.WriteWarning($"Microsoft.PowerShell.SecretManagement module cannot be found. Make sure it is installed before performing PSResource operations in order to successfully authenticate to PSResourceRepository \"{repoName}\" with its CredentialInfo.");
                }
            }

            // determine if either 1 of 4 values are attempting to be set: Uri, Priority, Trusted, CredentialInfo.
            // if none are (i.e only Name parameter was provided, write error)
            if (repoUri == null && repoPriority == defaultPriority && _trustedNullable == null && repoCredentialInfo == null)
            {
                errorMsg = "Must set Uri, Priority, Trusted or CredentialInfo parameter";
                return null;
            }

            if (!cmdletPassedIn.ShouldProcess(repoName, "Set repository's value(s) in repository store"))
            {
                return null;
            }

            if (!string.IsNullOrEmpty(errorMsg))
            {
                return null;
            }

            return Update(repoName, repoUri, repoPriority, _trustedNullable, repoCredentialInfo, cmdletPassedIn, out errorMsg);
        }

        /// <summary>
        /// Add a repository to the store
        /// Returns: PSRepositoryInfo containing information about the repository just added to the repository store
        /// </summary>
        /// <param name="sectionName"></param>
        public static PSRepositoryInfo Add(string repoName, Uri repoUri, int repoPriority, bool repoTrusted, PSCredentialInfo repoCredentialInfo, PSRepositoryInfo.APIVersion apiVersion, bool force)
        {
            try
            {
                // Open file
                XDocument doc = LoadXDocument(FullRepositoryPath);
                if (FindRepositoryElement(doc, repoName) != null)
                {
                    if (!force)
                    {
                        throw new PSInvalidOperationException(String.Format("The PSResource Repository '{0}' already exists.", repoName));
                    }

                    // Delete the existing repository before overwriting it (otherwire multiple repos with the same name will be added)
                    List<PSRepositoryInfo> removedRepositories = RepositorySettings.Remove(new string[] { repoName }, out string[] errorList);

                    // Need to load the document again because of changes after removing
                    doc = LoadXDocument(FullRepositoryPath);

                    if (errorList.Count() > 0)
                    {
                        throw new PSInvalidOperationException($"The PSResource Repository '{repoName}' cannot be overwritten: ${errorList.FirstOrDefault()}");
                    }

                }

                // Else, keep going
                // Get root of XDocument (XElement)
                var root = doc.Root;

                // Create new element
                XElement newElement = new XElement(
                    "Repository",
                    new XAttribute("Name", repoName),
                    new XAttribute("Url", repoUri),
                    new XAttribute("APIVersion", apiVersion),
                    new XAttribute("Priority", repoPriority),
                    new XAttribute("Trusted", repoTrusted)
                    );

                if (repoCredentialInfo != null)
                {
                    newElement.Add(new XAttribute(PSCredentialInfo.VaultNameAttribute, repoCredentialInfo.VaultName));
                    newElement.Add(new XAttribute(PSCredentialInfo.SecretNameAttribute, repoCredentialInfo.SecretName));
                }

                root.Add(newElement);

                // Close the file
                root.Save(FullRepositoryPath);
            }
            catch (Exception e)
            {
                throw new PSInvalidOperationException(String.Format("Adding to repository store failed: {0}", e.Message));
            }

            return new PSRepositoryInfo(repoName, repoUri, repoPriority, repoTrusted, repoCredentialInfo, apiVersion);
        }

        /// <summary>
        /// Updates a repository name, Uri, priority, installation policy, or credential information
        /// Returns:  void
        /// </summary>
        public static PSRepositoryInfo Update(string repoName, Uri repoUri, int repoPriority, bool? repoTrusted, PSCredentialInfo repoCredentialInfo, PSCmdlet cmdletPassedIn, out string errorMsg)
        {
            errorMsg = string.Empty;
            PSRepositoryInfo updatedRepo;
            try
            {
                // Open file
                XDocument doc = LoadXDocument(FullRepositoryPath);
                XElement node = FindRepositoryElement(doc, repoName);
                if (node == null)
                {
                    bool repoIsTrusted = !(repoTrusted == null || repoTrusted == false);
                    repoPriority = repoPriority < 0 ? DefaultPriority : repoPriority;
                    return AddToRepositoryStore(repoName, repoUri, repoPriority, repoIsTrusted, repoCredentialInfo, force:true, cmdletPassedIn, out errorMsg);
                }

                // Check that repository node we are attempting to update has all required attributes: Name, Url (or Uri), Priority, Trusted.
                // Name attribute is already checked for in FindRepositoryElement()

                if (node.Attribute("Priority") == null)
                {
                    errorMsg = $"Repository element does not contain neccessary 'Priority' attribute, in file located at path: {FullRepositoryPath}. Fix this in your file and run again.";
                    return null;
                }
                
                if (node.Attribute("Trusted") == null)
                {
                    errorMsg = $"Repository element does not contain neccessary 'Trusted' attribute, in file located at path: {FullRepositoryPath}. Fix this in your file and run again.";
                    return null;
                }    

                if (node.Attribute("APIVersion") == null)
                {
                    errorMsg = $"Repository element does not contain neccessary 'APIVersion' attribute, in file located at path: {FullRepositoryPath}. Fix this in your file and run again.";
                    return null;
                }

                bool urlAttributeExists = node.Attribute("Url") != null;
                bool uriAttributeExists = node.Attribute("Uri") != null;
                if (!urlAttributeExists && !uriAttributeExists)
                {
                    errorMsg = $"Repository element does not contain neccessary 'Url' attribute (or alternatively 'Uri' attribute), in file located at path: {FullRepositoryPath}. Fix this in your file and run again.";
                    return null;
                }

                // Else, keep going
                // Get root of XDocument (XElement)
                var root = doc.Root;

                // A null Uri (or Url) value passed in signifies the Uri was not attempted to be set.
                // So only set Uri attribute if non-null value passed in for repoUri

                // determine if existing repository node (which we wish to update) had Url or Uri attribute
                Uri thisUrl = null;
                PSRepositoryInfo.APIVersion apiVersion = (PSRepositoryInfo.APIVersion)Enum.Parse(typeof(PSRepositoryInfo.APIVersion), node.Attribute("APIVersion").Value);
                if (repoUri != null) 
                {
                    if (!Uri.TryCreate(repoUri.AbsoluteUri, UriKind.Absolute, out thisUrl))
                    {
                        throw new PSInvalidOperationException(String.Format("Unable to read incorrectly formatted Url for repo {0}", repoName));
                    }

                    if (urlAttributeExists)
                    {
                        node.Attribute("Url").Value = thisUrl.AbsoluteUri;
                    }
                    else
                    {
                        node.Attribute("Uri").Value = thisUrl.AbsoluteUri;
                    }

                    apiVersion = GetRepoAPIVersion(repoUri);
                }
                else 
                {
                    if (urlAttributeExists)
                    {
                        if(!Uri.TryCreate(node.Attribute("Url").Value, UriKind.Absolute, out thisUrl))
                        {
                            throw new PSInvalidOperationException(String.Format("The 'Url' for repository {0} is invalid and the repository cannot be used. Please update the Url field or remove the repository entry.", repoName));
                        }
                    }
                    else
                    {
                        if(!Uri.TryCreate(node.Attribute("Uri").Value, UriKind.Absolute, out thisUrl))
                        {
                            throw new PSInvalidOperationException(String.Format("The 'Url' for repository {0} is invalid and the repository cannot be used. Please update the Url field or remove the repository entry.", repoName));
                        }
                    }
                }

                // A negative Priority value passed in signifies the Priority value was not attempted to be set.
                // So only set Priority attribute if non-null value passed in for repoPriority
                if (repoPriority >= 0)
                {
                    node.Attribute("Priority").Value = repoPriority.ToString();
                }

                // A null Trusted value passed in signifies the Trusted value was not attempted to be set.
                // So only set Trusted attribute if non-null value passed in for repoTrusted.
                if (repoTrusted != null)
                {
                    node.Attribute("Trusted").Value = repoTrusted.ToString();
                }

                // A null CredentialInfo value passed in signifies that CredentialInfo was not attempted to be set.
                // Set VaultName and SecretName attributes if non-null value passed in for repoCredentialInfo
                if (repoCredentialInfo != null)
                {
                    if (node.Attribute(PSCredentialInfo.VaultNameAttribute) == null)
                    {
                        node.Add(new XAttribute(PSCredentialInfo.VaultNameAttribute, repoCredentialInfo.VaultName));
                    }
                    else
                    {
                        node.Attribute(PSCredentialInfo.VaultNameAttribute).Value = repoCredentialInfo.VaultName;
                    }

                    if (node.Attribute(PSCredentialInfo.SecretNameAttribute) == null)
                    {
                        node.Add(new XAttribute(PSCredentialInfo.SecretNameAttribute, repoCredentialInfo.SecretName));
                    }
                    else
                    {
                        node.Attribute(PSCredentialInfo.SecretNameAttribute).Value = repoCredentialInfo.SecretName;
                    }
                }

                // Create CredentialInfo based on new values or whether it was empty to begin with
                PSCredentialInfo thisCredentialInfo = null;
                if (node.Attribute(PSCredentialInfo.VaultNameAttribute)?.Value != null &&
                    node.Attribute(PSCredentialInfo.SecretNameAttribute)?.Value != null)
                {
                    thisCredentialInfo = new PSCredentialInfo(
                        node.Attribute(PSCredentialInfo.VaultNameAttribute).Value,
                        node.Attribute(PSCredentialInfo.SecretNameAttribute).Value);
                }

                updatedRepo = new PSRepositoryInfo(repoName,
                    thisUrl,
                    Int32.Parse(node.Attribute("Priority").Value),
                    Boolean.Parse(node.Attribute("Trusted").Value),
                    thisCredentialInfo,
                    apiVersion);

                // Close the file
                root.Save(FullRepositoryPath);
            }
            catch (Exception e)
            {
                throw new PSInvalidOperationException(String.Format("Updating to repository store failed: {0}", e.Message));
            }

            return updatedRepo;
        }

        /// <summary>
        /// Removes a repository from the XML
        /// Returns: void
        /// </summary>
        /// <param name="sectionName"></param>
        public static List<PSRepositoryInfo> Remove(string[] repoNames, out string[] errorList)
        {
            List<PSRepositoryInfo> removedRepos = new List<PSRepositoryInfo>();
            List<string> tempErrorList = new List<string>();
            XDocument doc;
            try
            {
                // Open file
                doc = LoadXDocument(FullRepositoryPath);
            }
            catch (Exception e)
            {
                throw new PSInvalidOperationException(String.Format("Loading repository store failed: {0}", e.Message));
            }

            // Get root of XDocument (XElement)
            var root = doc.Root;

            foreach (string repo in repoNames)
            {
                XElement node = FindRepositoryElement(doc, repo);
                if (node == null)
                {
                    tempErrorList.Add(String.Format("Unable to find repository '{0}'.  Use Get-PSResourceRepository to see all available repositories.", repo));
                    continue;
                }

                PSCredentialInfo repoCredentialInfo = null;
                if (node.Attribute("VaultName") != null & node.Attribute("SecretName") != null)
                {
                    repoCredentialInfo = new PSCredentialInfo(node.Attribute("VaultName").Value, node.Attribute("SecretName").Value);
                }

                if (node.Attribute("Priority") == null)
                {
                    tempErrorList.Add(String.Format("Repository element does not contain neccessary 'Priority' attribute, in file located at path: {0}. Fix this in your file and run again.", FullRepositoryPath));
                    continue;
                }
                
                if (node.Attribute("Trusted") == null)
                {
                    tempErrorList.Add(String.Format("Repository element does not contain neccessary 'Trusted' attribute, in file located at path: {0}. Fix this in your file and run again.", FullRepositoryPath));
                    continue;
                }

                if (node.Attribute("APIVersion") == null)
                {
                    tempErrorList.Add(String.Format("Repository element does not contain neccessary 'APIVersion' attribute, in file located at path: {0}. Fix this in your file and run again.", FullRepositoryPath));
                    continue;
                }

                // determine if repo had Url or Uri (less likely) attribute
                bool urlAttributeExists = node.Attribute("Url") != null;
                bool uriAttributeExists = node.Attribute("Uri") != null;
                if (!urlAttributeExists && !uriAttributeExists)
                {
                    tempErrorList.Add(String.Format("Repository element does not contain neccessary 'Url' or equivalent 'Uri' attribute (it must contain one per Repository), in file located at path: {0}. Fix this in your file and run again.", FullRepositoryPath));
                    continue;  
                }

                string attributeUrlUriName = urlAttributeExists ? "Url" : "Uri";
                removedRepos.Add(
                    new PSRepositoryInfo(repo,
                        new Uri(node.Attribute(attributeUrlUriName).Value),
                        Int32.Parse(node.Attribute("Priority").Value),
                        Boolean.Parse(node.Attribute("Trusted").Value),
                        repoCredentialInfo,
                        (PSRepositoryInfo.APIVersion)Enum.Parse(typeof(PSRepositoryInfo.APIVersion), node.Attribute("APIVersion").Value)));

                // Remove item from file
                node.Remove();
            }

            // Close the file
            root.Save(FullRepositoryPath);
            errorList = tempErrorList.ToArray();

            return removedRepos;
        }

        public static List<PSRepositoryInfo> Read(string[] repoNames, out string[] errorList)
        {
            List<string> tempErrorList = new List<string>();
            var foundRepos = new List<PSRepositoryInfo>();

            XDocument doc;
            try
            {
                // Open file
                doc = LoadXDocument(FullRepositoryPath);
            }
            catch (Exception e)
            {
                throw new PSInvalidOperationException(String.Format("Loading repository store failed: {0}", e.Message));
            }

            if (repoNames == null || repoNames.Length == 0 || string.Equals(repoNames[0], "*") || repoNames[0] == null)
            {
                // Name array or single value is null so we will list all repositories registered
                // iterate through the doc
                foreach (XElement repo in doc.Descendants("Repository"))
                {
                    if (repo.Attribute("Name") == null)
                    {
                        tempErrorList.Add(String.Format("Repository element does not contain neccessary 'Name' attribute, in file located at path: {0}. Fix this in your file and run again.", FullRepositoryPath));
                        continue;
                    }

                    if (repo.Attribute("Priority") == null)
                    {
                        tempErrorList.Add(String.Format("Repository element does not contain neccessary 'Priority' attribute, in file located at path: {0}. Fix this in your file and run again.", FullRepositoryPath));
                        continue;
                    }
                    
                    if (repo.Attribute("Trusted") == null)
                    {
                        tempErrorList.Add(String.Format("Repository element does not contain neccessary 'Trusted' attribute, in file located at path: {0}. Fix this in your file and run again.", FullRepositoryPath));
                        continue;
                    }

                    bool urlAttributeExists = repo.Attribute("Url") != null;
                    bool uriAttributeExists = repo.Attribute("Uri") != null;
                    // case: neither Url nor Uri attributes exist
                    if (!urlAttributeExists && !uriAttributeExists)
                    {
                        tempErrorList.Add(String.Format("Repository element does not contain neccessary 'Url' or equivalent 'Uri' attribute (it must contain one), in file located at path: {0}. Fix this in your file and run again.", FullRepositoryPath));
                        continue;   
                    }

                    Uri thisUrl = null;
                    // case: either attribute Uri or Url exists
                    // TODO: do we only allow both to exist, across repositories? (i.e if a file has Uri attribute for one repo and Url attribute for another --> is that invalid?)
                    if (urlAttributeExists)
                    {
                        if (!Uri.TryCreate(repo.Attribute("Url").Value, UriKind.Absolute, out thisUrl))
                        {
                            tempErrorList.Add(String.Format("Unable to read incorrectly formatted Url for repo {0}", repo.Attribute("Name").Value));
                            continue;
                        }

                    }
                    else if (uriAttributeExists)
                    {
                        if (!Uri.TryCreate(repo.Attribute("Uri").Value, UriKind.Absolute, out thisUrl))
                        {
                            tempErrorList.Add(String.Format("Unable to read incorrectly formatted Uri for repo {0}", repo.Attribute("Name").Value));
                            continue;
                        }
                    }

                    if (repo.Attribute("APIVersion") == null)
                    {
                        PSRepositoryInfo.APIVersion apiVersion = GetRepoAPIVersion(thisUrl);

                        XElement repoXElem = FindRepositoryElement(doc, repo.Attribute("Name").Value);
                        repoXElem.SetAttributeValue("APIVersion", apiVersion.ToString());
                        doc.Save(FullRepositoryPath);
                    }

                    PSCredentialInfo thisCredentialInfo;
                    string credentialInfoErrorMessage = $"Repository {repo.Attribute("Name").Value} has invalid CredentialInfo. {PSCredentialInfo.VaultNameAttribute} and {PSCredentialInfo.SecretNameAttribute} should both be present and non-empty";
                    // both keys are present
                    if (repo.Attribute(PSCredentialInfo.VaultNameAttribute) != null && repo.Attribute(PSCredentialInfo.SecretNameAttribute) != null)
                    {
                        try
                        {
                            // both values are non-empty
                            // = valid credentialInfo
                            thisCredentialInfo = new PSCredentialInfo(repo.Attribute(PSCredentialInfo.VaultNameAttribute).Value, repo.Attribute(PSCredentialInfo.SecretNameAttribute).Value);
                        }
                        catch (Exception)
                        {
                            thisCredentialInfo = null;
                            tempErrorList.Add(credentialInfoErrorMessage);
                            continue;
                        }
                    }
                    // both keys are missing
                    else if (repo.Attribute(PSCredentialInfo.VaultNameAttribute) == null && repo.Attribute(PSCredentialInfo.SecretNameAttribute) == null)
                    {
                        // = valid credentialInfo
                        thisCredentialInfo = null;
                    }
                    // one of the keys is missing
                    else
                    {
                        thisCredentialInfo = null;
                        tempErrorList.Add(credentialInfoErrorMessage);
                        continue;
                    }

                    PSRepositoryInfo currentRepoItem = new PSRepositoryInfo(repo.Attribute("Name").Value,
                        thisUrl,
                        Int32.Parse(repo.Attribute("Priority").Value),
                        Boolean.Parse(repo.Attribute("Trusted").Value),
                        thisCredentialInfo,
                        (PSRepositoryInfo.APIVersion)Enum.Parse(typeof(PSRepositoryInfo.APIVersion), repo.Attribute("APIVersion").Value));

                    foundRepos.Add(currentRepoItem);
                }
            }
            else
            {
                foreach (string repo in repoNames)
                {
                    bool repoMatch = false;
                    WildcardPattern nameWildCardPattern = new WildcardPattern(repo, WildcardOptions.IgnoreCase);

                    foreach (var node in doc.Descendants("Repository").Where(e => e.Attribute("Name") != null && nameWildCardPattern.IsMatch(e.Attribute("Name").Value)))
                    {
                        if (node.Attribute("Priority") == null)
                        {
                            tempErrorList.Add(String.Format("Repository element does not contain neccessary 'Priority' attribute, in file located at path: {0}. Fix this in your file and run again.", FullRepositoryPath));
                            continue;
                        }
                        
                        if (node.Attribute("Trusted") == null)
                        {
                            tempErrorList.Add(String.Format("Repository element does not contain neccessary 'Trusted' attribute, in file located at path: {0}. Fix this in your file and run again.", FullRepositoryPath));
                            continue;
                        }

                        repoMatch = true;
                        bool urlAttributeExists = node.Attribute("Url") != null;
                        bool uriAttributeExists = node.Attribute("Uri") != null;

                        // case: neither Url nor Uri attributes exist
                        if (!urlAttributeExists && !uriAttributeExists)
                        {
                            tempErrorList.Add(String.Format("Repository element does not contain neccessary 'Url' or equivalent 'Uri' attribute (it must contain one), in file located at path: {0}. Fix this in your file and run again.", FullRepositoryPath));
                            continue;   
                        }

                        Uri thisUrl = null;
                        // case: either attribute Uri or Url exists
                        // TODO: do we only allow both to exist, across repositories? (i.e if a file has Uri attribute for one repo and Url attribute for another --> is that invalid?)
                        if (urlAttributeExists)
                        {
                            if (!Uri.TryCreate(node.Attribute("Url").Value, UriKind.Absolute, out thisUrl))
                            {
                                tempErrorList.Add(String.Format("Unable to read incorrectly formatted Url for repo {0}", node.Attribute("Name").Value));
                                continue;
                            }
                        }
                        else if (uriAttributeExists)
                        {
                            if (!Uri.TryCreate(node.Attribute("Uri").Value, UriKind.Absolute, out thisUrl))
                            {
                                tempErrorList.Add(String.Format("Unable to read incorrectly formatted Uri for repo {0}", node.Attribute("Name").Value));
                                continue;
                            }
                        }

                        if (node.Attribute("APIVersion") == null)
                        {
                            PSRepositoryInfo.APIVersion apiVersion = GetRepoAPIVersion(thisUrl);

                            XElement repoXElem = FindRepositoryElement(doc, node.Attribute("Name").Value);
                            repoXElem.SetAttributeValue("APIVersion", apiVersion.ToString());
                            doc.Save(FullRepositoryPath);
                        }

                        PSCredentialInfo thisCredentialInfo;
                        string credentialInfoErrorMessage = $"Repository {node.Attribute("Name").Value} has invalid CredentialInfo. {PSCredentialInfo.VaultNameAttribute} and {PSCredentialInfo.SecretNameAttribute} should both be present and non-empty";
                        // both keys are present
                        if (node.Attribute(PSCredentialInfo.VaultNameAttribute) != null && node.Attribute(PSCredentialInfo.SecretNameAttribute) != null)
                        {
                            try
                            {
                                // both values are non-empty
                                // = valid credentialInfo
                                thisCredentialInfo = new PSCredentialInfo(node.Attribute(PSCredentialInfo.VaultNameAttribute).Value, node.Attribute(PSCredentialInfo.SecretNameAttribute).Value);
                            }
                            catch (Exception)
                            {
                                thisCredentialInfo = null;
                                tempErrorList.Add(credentialInfoErrorMessage);
                                continue;
                            }
                        }
                        // both keys are missing
                        else if (node.Attribute(PSCredentialInfo.VaultNameAttribute) == null && node.Attribute(PSCredentialInfo.SecretNameAttribute) == null)
                        {
                            // = valid credentialInfo
                            thisCredentialInfo = null;
                        }
                        // one of the keys is missing
                        else
                        {
                            thisCredentialInfo = null;
                            tempErrorList.Add(credentialInfoErrorMessage);
                            continue;
                        }

                        PSRepositoryInfo currentRepoItem = new PSRepositoryInfo(node.Attribute("Name").Value,
                            thisUrl,
                            Int32.Parse(node.Attribute("Priority").Value),
                            Boolean.Parse(node.Attribute("Trusted").Value),
                            thisCredentialInfo,
                            (PSRepositoryInfo.APIVersion)Enum.Parse(typeof(PSRepositoryInfo.APIVersion), node.Attribute("APIVersion").Value));

                        foundRepos.Add(currentRepoItem);
                    }

                    if (!repo.Contains("*") && !repoMatch)
                    {
                        tempErrorList.Add(String.Format("Unable to find repository with Name '{0}'.  Use Get-PSResourceRepository to see all available repositories.", repo));
                    }
                }
            }

            errorList = tempErrorList.ToArray();
            // Sort by priority, then by repo name
            var reposToReturn = foundRepos.OrderBy(x => x.Priority).ThenBy(x => x.Name);
            return reposToReturn.ToList();
        }

        #endregion

        #region Private methods

        private static XElement FindRepositoryElement(XDocument doc, string name)
        {
            return doc.Descendants("Repository").Where(
                e => e.Attribute("Name") != null && 
                    string.Equals(
                    e.Attribute("Name").Value,
                    name,
                    StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
        }

        private static readonly XmlReaderSettings XDocReaderSettings = new XmlReaderSettings()
        {
            DtdProcessing = DtdProcessing.Prohibit,     // Disallow any DTD elements
            XmlResolver = null,                         // Do not resolve external links
            CheckCharacters = true,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            IgnoreWhitespace = true,
            MaxCharactersFromEntities = 1024,
            MaxCharactersInDocument = 512 * 1024 * 1024, // 512M characters = 1GB
            ValidationFlags = System.Xml.Schema.XmlSchemaValidationFlags.None,
            ValidationType = ValidationType.None
        };

        private static XDocument LoadXDocument(string filePath)
        {
            using var xmlReader = XmlReader.Create(filePath, XDocReaderSettings);
            return XDocument.Load(xmlReader);
        }

        private static PSRepositoryInfo.APIVersion GetRepoAPIVersion(Uri repoUri) {

            if (repoUri.AbsoluteUri.EndsWith("api/v2"))
            {
                return PSRepositoryInfo.APIVersion.v2;
            }
            else if (repoUri.AbsoluteUri.EndsWith("v3/index.json"))
            {
                return PSRepositoryInfo.APIVersion.v3;
            }
            else if (repoUri.Scheme == Uri.UriSchemeFile)
            {
                return PSRepositoryInfo.APIVersion.local;
            }
            else
            {
                return PSRepositoryInfo.APIVersion.Unknown;
            }
        }

        #endregion
    }
}
