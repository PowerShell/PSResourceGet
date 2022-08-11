﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Xml;
using System.Xml.Linq;

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
                Add(PSGalleryRepoName, psGalleryUri, DefaultPriority, DefaultTrusted, repoCredentialInfo: null);
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

        /// <summary>
        /// Add a repository to the store
        /// Returns: PSRepositoryInfo containing information about the repository just added to the repository store
        /// </summary>
        /// <param name="sectionName"></param>
        public static PSRepositoryInfo Add(string repoName, Uri repoUri, int repoPriority, bool repoTrusted, PSCredentialInfo repoCredentialInfo)
        {
            try
            {
                // Open file
                XDocument doc = LoadXDocument(FullRepositoryPath);
                if (FindRepositoryElement(doc, repoName) != null)
                {
                    throw new PSInvalidOperationException(String.Format("The PSResource Repository '{0}' already exists.", repoName));
                }

                // Else, keep going
                // Get root of XDocument (XElement)
                var root = doc.Root;

                // Create new element
                XElement newElement = new XElement(
                    "Repository",
                    new XAttribute("Name", repoName),
                    new XAttribute("Url", repoUri),
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

            return new PSRepositoryInfo(repoName, repoUri, repoPriority, repoTrusted, repoCredentialInfo);
        }

        /// <summary>
        /// Updates a repository name, Uri, priority, installation policy, or credential information
        /// Returns:  void
        /// </summary>
        public static PSRepositoryInfo Update(string repoName, Uri repoUri, int repoPriority, bool? repoTrusted, PSCredentialInfo repoCredentialInfo)
        {
            PSRepositoryInfo updatedRepo;
            try
            {
                // Open file
                XDocument doc = LoadXDocument(FullRepositoryPath);
                XElement node = FindRepositoryElement(doc, repoName);
                if (node == null)
                {
                    throw new ArgumentException("Cannot find the repository because it does not exist. Try registering the repository using 'Register-PSResourceRepository'");
                }

                // Check that repository node we are attempting to update has all required attributes: Name, Url (or Uri), Priority, Trusted.
                // Name attribute is already checked for in FindRepositoryElement()

                if (node.Attribute("Priority") == null)
                {
                    throw new ArgumentException("Repository element does not contain neccessary 'Priority' attribute, in file located at path: {0}. Fix this in your file and run again.", FullRepositoryPath);
                }
                
                if (node.Attribute("Trusted") == null)
                {
                    throw new ArgumentException("Repository element does not contain neccessary 'Trusted' attribute, in file located at path: {0}. Fix this in your file and run again.", FullRepositoryPath);
                }                

                bool urlAttributeExists = node.Attribute("Url") != null;
                bool uriAttributeExists = node.Attribute("Uri") != null;
                if (!urlAttributeExists && !uriAttributeExists)
                {
                    throw new ArgumentException("Repository element does not contain neccessary 'Url' attribute (or alternatively 'Uri' attribute), in file located at path: {0}. Fix this in your file and run again.", FullRepositoryPath); 
                }

                // Else, keep going
                // Get root of XDocument (XElement)
                var root = doc.Root;

                // A null Uri (or Url) value passed in signifies the Uri was not attempted to be set.
                // So only set Uri attribute if non-null value passed in for repoUri

                // determine if existing repository node (which we wish to update) had Url to Uri attribute
                Uri thisUrl = null;
                if (repoUri != null)
                {
                    if (urlAttributeExists)
                    {
                        node.Attribute("Url").Value = repoUri.AbsoluteUri;
                        // Create Uri from node Uri attribute to create PSRepositoryInfo item to return.
                        if (!Uri.TryCreate(node.Attribute("Url").Value, UriKind.Absolute, out thisUrl))
                        {
                            throw new PSInvalidOperationException(String.Format("Unable to read incorrectly formatted Url for repo {0}", repoName));
                        }
                    }
                    else
                    {
                        // Uri attribute exists
                        node.Attribute("Uri").Value = repoUri.AbsoluteUri;
                        if (!Uri.TryCreate(node.Attribute("Uri").Value, UriKind.Absolute, out thisUrl))
                        {
                            throw new PSInvalidOperationException(String.Format("Unable to read incorrectly formatted Uri for repo {0}", repoName));
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
                    thisCredentialInfo);

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
                        repoCredentialInfo));
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
                        thisCredentialInfo);

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
                            thisCredentialInfo);

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

        #endregion
    }
}
